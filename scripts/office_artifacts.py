"""Artifact-first Office integration. Standard-library only; never invokes a compiler."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import zipfile

PRODUCTS = {
    "launcher": ("Broiler", "Broiler.Launcher.Windows.exe"),
    "boss": ("Broiler", "Broiler.Office.Server.exe"),
    "browser": ("Broiler.Browser", "Broiler.Browser.Windows.exe"),
    "writer": ("Broiler.Writer", "Broiler.Writer.Windows.exe"),
    "cell": ("Broiler.Cells", "Broiler.Cells.Windows.exe"),
    "plate": ("Broiler.Plate", "Broiler.Plate.Windows.exe"),
}
SEMVER = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\Z")
HEX = re.compile(r"[a-f0-9]{64}\Z")
SHA = re.compile(r"[a-f0-9]{40}\Z")
ASSET = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,199}\Z")


def version(value):
    if not isinstance(value, str) or len(value) > 100 or not SEMVER.fullmatch(value):
        raise ValueError("Invalid semantic version")
    if "-" in value.split("+")[0]:
        for part in value.split("+")[0].split("-", 1)[1].split("."):
            if part.isdigit() and len(part) > 1 and part[0] == "0":
                raise ValueError("Numeric prerelease identifiers cannot have leading zeroes")
    return value


def compare_versions(left, right):
    def parts(value):
        core, *pre = version(value).split("+")[0].split("-", 1)
        return tuple(map(int, core.split("."))), pre[0].split(".") if pre else None
    left_core, left_pre = parts(left)
    right_core, right_pre = parts(right)
    if left_core != right_core:
        return (left_core > right_core) - (left_core < right_core)
    if left_pre is None or right_pre is None:
        return (left_pre is None) - (right_pre is None)
    for a, b in zip(left_pre, right_pre):
        if a == b:
            continue
        if a.isdigit() and b.isdigit():
            return (int(a) > int(b)) - (int(a) < int(b))
        if a.isdigit() != b.isdigit():
            return -1 if a.isdigit() else 1
        return (a > b) - (a < b)
    return (len(left_pre) > len(right_pre)) - (len(left_pre) < len(right_pre))


def read_json(path, limit=1024 * 1024):
    path = Path(path)
    if path.stat().st_size > limit:
        raise ValueError("Metadata exceeds size limit")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def digest(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def validate_pin(pin, suite_channel):
    product = pin["id"]
    if product not in PRODUCTS or pin["repository"] != "Broiler-Platform/" + PRODUCTS[product][0]:
        raise ValueError("Unexpected product repository")
    version(pin["version"])
    if not SHA.fullmatch(pin["commit"]) or not HEX.fullmatch(pin["sha256"]):
        raise ValueError("A pin requires full commit and archive SHA-256")
    if not ASSET.fullmatch(pin["asset"]) or not pin["asset"].endswith(".zip"):
        raise ValueError("Unsafe archive filename")
    if not isinstance(pin["size"], int) or isinstance(pin["size"], bool) or not 0 < pin["size"] <= 2 * 1024**3:
        raise ValueError("Invalid archive size")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._+-]{0,127}", pin["tag"]):
        raise ValueError("Unsafe release tag")
    for key in ("releaseId", "assetId"):
        if not isinstance(pin[key], int) or isinstance(pin[key], bool) or pin[key] <= 0:
            raise ValueError("Pins require numeric GitHub release and asset IDs")
    if pin["rid"] != "win-x64" or pin["archive"] != "zip" or pin["invocation"] != "native" or pin["entryPoint"] != PRODUCTS[product][1]:
        raise ValueError("Unsupported Office component platform/entry point")
    if pin["publishMode"] not in ("self-contained", "native-aot", "framework-dependent"):
        raise ValueError("Unsupported publishing mode")
    if suite_channel == "stable" and (pin.get("prerelease", True) or "-" in pin["version"].split("+")[0]):
        raise ValueError("Stable suites cannot approve preview component releases")
    return pin


def validate_lock(lock, allow_incomplete=False):
    if lock["schemaVersion"] != 1 or lock["channel"] not in ("stable", "preview"):
        raise ValueError("Unsupported Office lock schema/channel")
    version(lock["suite"])
    version(lock["minimumLauncherVersion"])
    if lock["channel"] == "stable" and "-" in lock["suite"].split("+")[0]:
        raise ValueError("Stable suite version cannot be a prerelease")
    pins = lock["components"]
    if not isinstance(pins, list) or len(pins) > len(PRODUCTS):
        raise ValueError("Invalid Office component set")
    ids = [validate_pin(p, lock["channel"])["id"] for p in pins]
    if len(set(ids)) != len(ids):
        raise ValueError("Duplicate component pin")
    if len({p["asset"].casefold() for p in pins}) != len(pins):
        raise ValueError("Component asset filenames must be unique across the suite")
    missing = sorted(set(PRODUCTS) - set(ids))
    if missing and not allow_incomplete:
        raise ValueError("Publish component releases and pin them first; missing: " + ", ".join(missing))
    launcher = next((p for p in pins if p["id"] == "launcher"), None)
    if launcher and compare_versions(launcher["version"], lock["minimumLauncherVersion"]) < 0:
        raise ValueError("Pinned launcher is older than the minimum required launcher")
    # Dependencies such as the BOSS Writer web bundle are pinned by its producer,
    # not rebuilt or substituted by the suite assembler.
    return lock


def api(path):
    request = urllib.request.Request("https://api.github.com/repos/" + path, headers={
        "Accept": "application/vnd.github+json", "User-Agent": "Broiler-Office-Integrator/1.0",
        "X-GitHub-Api-Version": "2026-03-10",
    })
    # Public release reads need no token. Never forward credentials to binary hosts.
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read(4 * 1024 * 1024 + 1)
    if len(data) > 4 * 1024 * 1024:
        raise ValueError("GitHub metadata exceeds limits")
    return json.loads(data)


def resolve_commit(repository, tag):
    ref = api(repository + "/git/ref/tags/" + urllib.parse.quote(tag, safe=""))["object"]
    for _ in range(5):
        if ref["type"] == "commit" and SHA.fullmatch(ref["sha"]):
            return ref["sha"]
        if ref["type"] != "tag" or not SHA.fullmatch(ref["sha"]):
            break
        ref = api(repository + "/git/tags/" + ref["sha"])["object"]
    raise ValueError("Release tag does not resolve to a commit")


def release_asset(pin):
    release = api(pin["repository"] + "/releases/" + str(pin["releaseId"]))
    if release["draft"] or release["tag_name"] != pin["tag"] or bool(release["prerelease"]) != bool(pin["prerelease"]):
        raise ValueError("Draft or changed component release")
    if resolve_commit(pin["repository"], pin["tag"]) != pin["commit"]:
        raise ValueError("Component tag moved after lock approval")
    matches = [a for a in release["assets"] if a["id"] == pin["assetId"]]
    if len(matches) != 1:
        raise ValueError("Pinned asset was removed/replaced")
    asset = matches[0]
    if asset["name"] != pin["asset"] or asset["size"] != pin["size"] or asset.get("digest") != "sha256:" + pin["sha256"]:
        raise ValueError("GitHub release asset differs from approved pin")
    if pin.get("componentManifest"):
        metadata = pin["componentManifest"]
        found = next((a for a in release["assets"] if a["id"] == metadata["assetId"]), None)
        if found is None or found["name"] != "component-release.json" or found["size"] != metadata["size"] or found.get("digest") != "sha256:" + metadata["sha256"]:
            raise ValueError("Pinned component manifest was replaced")
    url = urllib.parse.urlsplit(asset["browser_download_url"])
    prefix = "/" + pin["repository"] + "/releases/download/" + pin["tag"] + "/"
    if url.scheme != "https" or url.netloc != "github.com" or urllib.parse.unquote(url.path) != prefix + pin["asset"] or url.query or url.fragment:
        raise ValueError("Unexpected artifact origin")
    return asset["browser_download_url"]


def verify_archive(path, pin):
    if Path(path).is_symlink() or Path(path).stat().st_size != pin["size"] or digest(path) != pin["sha256"]:
        raise ValueError("Archive bytes differ from lock: " + pin["id"])


def acquire(pin, cache, offline=False):
    cache = Path(cache)
    cache.mkdir(parents=True, exist_ok=True)
    target = cache / (pin["sha256"] + ".zip")
    # Even a cached online build rechecks origin/IDs/tag. Offline mode is an
    # explicit rehearsal and is branded as such in BUILD-INFO.json.
    url = None if offline else release_asset(pin)
    if target.exists():
        verify_archive(target, pin)
        return target
    if offline:
        raise ValueError("Pinned archive missing from offline cache: " + pin["id"])
    temporary = cache / (pin["sha256"] + "." + str(time.time_ns()) + ".part")
    try:
        request = urllib.request.Request(url, headers={"User-Agent": "Broiler-Office-Integrator/1.0"})
        deadline = time.monotonic() + 600
        with urllib.request.urlopen(request, timeout=30) as source, temporary.open("xb") as output:
            total = 0
            while block := source.read(1024 * 1024):
                total += len(block)
                if total > pin["size"] or time.monotonic() > deadline:
                    raise ValueError("Artifact download exceeds size/time limit")
                output.write(block)
        verify_archive(temporary, pin)
        os.replace(temporary, target)
        return target
    finally:
        temporary.unlink(missing_ok=True)


def extract(archive, destination):
    """Validate every entry before extracting; Windows-aware even on Linux CI."""
    destination = Path(destination)
    if destination.exists():
        raise ValueError("Extraction destination must be new")
    seen, required_directories, expanded = {}, set(), 0
    with zipfile.ZipFile(archive) as source:
        entries = source.infolist()
        if len(entries) > 100_000:
            raise ValueError("Archive entry count exceeds limit")
        for entry in entries:
            name = entry.orig_filename  # Windows zipfile normalizes '\\' in .filename.
            parts = PurePosixPath(name).parts
            if not parts or name.startswith("/") or "\\" in name or any(
                part in (".", "..") or any(c in part for c in ':<>"|?*') or part.endswith((".", " ")) or
                re.match(r"(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|\Z)", part, re.I) or
                any(ord(c) < 32 for c in part) for part in parts):
                raise ValueError("Unsafe archive entry")
            # PurePosixPath normalizes '.', so explicitly reject the raw segments too.
            if any(p in ("", ".", "..") for p in name.rstrip("/").split("/")):
                raise ValueError("Unsafe archive entry")
            if any(p.lower().startswith(".office-") for p in parts):
                raise ValueError("Archive attempts to supply installation receipts")
            mode = entry.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR) or entry.external_attr & 0x400:
                raise ValueError("Links and special files are forbidden")
            key = name.rstrip("/").casefold()
            if key in seen:
                raise ValueError("Duplicate/case-colliding archive entries")
            if not entry.is_dir() and key in required_directories:
                raise ValueError("File/directory collision")
            for parent in PurePosixPath(key).parents:
                if str(parent) in seen and not seen[str(parent)]:
                    raise ValueError("File/directory collision")
                required_directories.add(str(parent))
            seen[key] = entry.is_dir()
            expanded += entry.file_size
            if expanded > 4 * 1024**3:
                raise ValueError("Expanded archive exceeds limit")
        destination.mkdir(parents=True)
        for entry in entries:
            path = destination.joinpath(*PurePosixPath(entry.filename).parts)
            if entry.is_dir():
                path.mkdir(parents=True, exist_ok=True)
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                with source.open(entry) as input_stream, path.open("xb") as output:
                    shutil.copyfileobj(input_stream, output, 1024 * 1024)


def assemble(lock_path, output, cache, commit, offline=False):
    lock = validate_lock(read_json(lock_path))
    # Signed production assembly is still a separate final release gate.
    if lock["channel"] != "preview" or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+-demo\.[1-9][0-9]*", lock["suite"]):
        raise ValueError("Unsigned assembly requires an explicit demo suite version")
    if not SHA.fullmatch(commit):
        raise ValueError("Full integration source commit required")
    output = Path(output).resolve()
    if output.exists():
        raise ValueError("Office output must be new")
    archives = {p["id"]: acquire(p, cache, offline) for p in lock["components"]}
    assets = output / "assets"
    bundle = output / "bundle" / ("Broiler-Office-" + lock["suite"] + "-win-x64")
    (bundle / "packages").mkdir(parents=True)
    assets.mkdir()
    components = []
    for pin in lock["components"]:
        archive = archives[pin["id"]]
        # Preserve original producer ZIP bytes and original filenames verbatim.
        for target in (assets / pin["asset"], bundle / "packages" / pin["asset"]):
            if target.exists():
                raise ValueError("Two pins use the same asset filename")
            shutil.copyfile(archive, target)
            verify_archive(target, pin)
        destination = bundle / ("launcher" if pin["id"] == "launcher" else "products/" + pin["id"])
        extract(assets / pin["asset"], destination)
        if not (destination / pin["entryPoint"]).is_file():
            raise ValueError("Component entry point missing: " + pin["id"])
        if pin["id"] == "boss" and not (destination / "wwwroot/index.html").is_file():
            raise ValueError("BOSS must include the Writer web bundle approved by its producer")
        components.append({key: pin[key] for key in ("id", "version", "rid", "asset", "size", "sha256", "archive", "entryPoint", "invocation", "publishMode")})
    manifest = dict(schemaVersion=1, officeVersion=lock["suite"], channel=lock["channel"],
                    tag="office-v" + lock["suite"], commit=commit,
                    minimumLauncherVersion=lock["minimumLauncherVersion"], demo=True,
                    components=components)
    write_json(bundle / "office-release.json", manifest)
    # Approval is the reviewed lock plus successful suite tests. Launcher release
    # discovery still consumes only Office manifests, never arbitrary product releases.
    shutil.copyfile(lock_path, bundle / "office.lock.json")
    write_json(bundle / "BUILD-INFO.json", dict(version=lock["suite"], commit=commit,
        demo=True, rid="win-x64", artifactFirst=True, offlineRehearsal=offline,
        lockSha256=digest(lock_path), sources=lock["components"]))
    write_json(bundle / "SBOM.cdx.json", dict(bomFormat="CycloneDX", specVersion="1.6", version=1,
        components=[dict(type="application", name=p["id"], version=p["version"],
                         hashes=[dict(alg="SHA-256", content=p["sha256"])],
                         externalReferences=[dict(type="distribution", url="https://github.com/" + p["repository"] + "/releases/tag/" + p["tag"])]) for p in lock["components"]]))
    root = Path(__file__).resolve().parent.parent
    shutil.copyfile(root / "Broiler.Launcher/packaging/README-demo.md", bundle / "README.md")
    (bundle / "licenses").mkdir()
    shutil.copyfile(root / "LICENSE", bundle / "licenses/LICENSE")
    (bundle / "Start-Broiler-Office.cmd").write_text('@echo off\nstart "Broiler Office Demo" "%~dp0launcher\\Broiler.Launcher.Windows.exe"\n', encoding="ascii")
    for name in ("office-release.json", "office.lock.json", "BUILD-INFO.json", "SBOM.cdx.json", "README.md"):
        shutil.copyfile(bundle / name, assets / name)
    # Reconstruct the suite envelope deterministically; original component ZIPs
    # inside it are copied verbatim, including producer timestamps/signatures.
    with zipfile.ZipFile(assets / (bundle.name + ".zip"), "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(bundle.rglob("*")):
            if path.is_file():
                info = zipfile.ZipInfo(path.relative_to(bundle.parent).as_posix(), (1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = (stat.S_IFREG | 0o644) << 16
                with path.open("rb") as source, archive.open(info, "w") as target:
                    shutil.copyfileobj(source, target, 1024 * 1024)
    base_url = "https://github.com/Broiler-Platform/Broiler/releases/download/" + urllib.parse.quote(manifest["tag"], safe="") + "/"
    suite_zip = assets / (bundle.name + ".zip")
    write_json(assets / "downloads.json", dict(schemaVersion=1, officeVersion=lock["suite"],
        channel=lock["channel"], compatibilityPolicy="office-approved-lock", releaseTag=manifest["tag"],
        updateManifest=base_url + "office-release.json", lockfile=base_url + "office.lock.json",
        suite=dict(asset=suite_zip.name, size=suite_zip.stat().st_size, sha256=digest(suite_zip), url=base_url + suite_zip.name),
        components=[dict(id=p["id"], version=p["version"], rid=p["rid"], asset=p["asset"],
            size=p["size"], sha256=p["sha256"], url=base_url + urllib.parse.quote(p["asset"], safe=""),
            sourceRepository=p["repository"], sourceTag=p["tag"], sourceCommit=p["commit"]) for p in lock["components"]]))
    lines = [digest(path) + "  " + path.name for path in sorted(assets.iterdir())]
    (assets / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="ascii")
    return assets / (bundle.name + ".zip")


def pin_release(lock_path, product, tag, asset_name, publish_mode, suite_version=None):
    lock = validate_lock(read_json(lock_path), allow_incomplete=True)
    if suite_version:
        lock["suite"] = version(suite_version)
    repository = "Broiler-Platform/" + PRODUCTS[product][0]
    release = api(repository + "/releases/tags/" + urllib.parse.quote(tag, safe=""))
    if release["draft"]:
        raise ValueError("Publish the reviewed component release before proposing an Office pin")
    asset = next((a for a in release["assets"] if a["name"] == asset_name), None)
    if asset is None or not re.fullmatch(r"sha256:[a-f0-9]{64}", asset.get("digest") or ""):
        raise ValueError("Release asset with GitHub SHA-256 digest required")
    prefix = {"launcher": "launcher-v", "boss": "boss-v", "browser": "browser-v", "writer": "writer-v", "cell": "cells-v", "plate": "plate-v"}[product]
    if not tag.startswith(prefix):
        raise ValueError("Unexpected product tag")
    pin = dict(id=product, version=version(tag[len(prefix):]), repository=repository,
        tag=tag, commit=resolve_commit(repository, tag), releaseId=release["id"], assetId=asset["id"],
        asset=asset_name, size=asset["size"], sha256=asset["digest"][7:], rid="win-x64", archive="zip",
        entryPoint=PRODUCTS[product][1], invocation="native", publishMode=publish_mode, prerelease=release["prerelease"])
    validate_pin(pin, lock["channel"])
    metadata = next((a for a in release["assets"] if a["name"] == "component-release.json"), None)
    if metadata is not None:
        manifest = component_metadata(repository, tag, metadata)
        if manifest["repository"] != repository or manifest["tag"] != tag or manifest["commit"] != pin["commit"] or manifest["version"] != pin["version"]:
            raise ValueError("Component manifest provenance mismatch")
        selected = [a for a in manifest["artifacts"] if a["asset"] == asset_name]
        if len(selected) != 1 or any(selected[0].get(key) != pin[key] for key in ("id", "version", "rid", "size", "sha256", "archive", "entryPoint", "invocation", "publishMode")):
            raise ValueError("Component manifest disagrees with selected artifact")
        pin["componentManifest"] = dict(assetId=metadata["id"], size=metadata["size"], sha256=metadata["digest"][7:])
        pin["dependencies"] = manifest.get("dependencies", [])
    lock["components"] = sorted([p for p in lock["components"] if p["id"] != product] + [pin], key=lambda p: p["id"])
    lock["pendingComponents"] = sorted(set(PRODUCTS) - {p["id"] for p in lock["components"]})
    validate_lock(lock, allow_incomplete=True)
    write_json(lock_path, lock)
    return pin


def component_metadata(repository, tag, asset):
    if not isinstance(asset["size"], int) or not 0 < asset["size"] <= 1024 * 1024 or not re.fullmatch(r"sha256:[a-f0-9]{64}", asset.get("digest") or ""):
        raise ValueError("Invalid component metadata asset")
    url = "https://github.com/" + repository + "/releases/download/" + urllib.parse.quote(tag, safe="") + "/component-release.json"
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Broiler-Office-Integrator/1.0"}), timeout=30) as response:
        raw = response.read(1024 * 1024 + 1)
    if len(raw) != asset["size"] or hashlib.sha256(raw).hexdigest() != asset["digest"][7:]:
        raise ValueError("Component metadata digest mismatch")
    manifest = json.loads(raw)
    if manifest["schemaVersion"] != 1:
        raise ValueError("Unsupported component metadata schema")
    return manifest


def validate_web_pin(pin):
    if pin["id"] != "writer-web" or pin["repository"] != "Broiler-Platform/Broiler.Writer" or pin["rid"] != "browser-wasm" or pin["entryPoint"] != "wwwroot/index.html":
        raise ValueError("Expected a pinned Writer WebAssembly artifact")
    if pin["archive"] != "zip" or pin["invocation"] != "web" or pin["publishMode"] != "web":
        raise ValueError("Unexpected Writer web artifact format")
    version(pin["version"])
    if not SHA.fullmatch(pin["commit"]) or not HEX.fullmatch(pin["sha256"]) or not ASSET.fullmatch(pin["asset"]) or pin["tag"] != "writer-v" + pin["version"]:
        raise ValueError("Invalid Writer web provenance")
    if not isinstance(pin["size"], int) or not 0 < pin["size"] <= 2 * 1024**3 or pin["releaseId"] <= 0 or pin["assetId"] <= 0:
        raise ValueError("Invalid Writer web asset identity/size")
    return pin


def pin_web(tag, name):
    repository = "Broiler-Platform/Broiler.Writer"
    release = api(repository + "/releases/tags/" + urllib.parse.quote(tag, safe=""))
    if release["draft"] or not tag.startswith("writer-v"):
        raise ValueError("A published Writer release is required")
    asset = next((a for a in release["assets"] if a["name"] == name), None)
    if asset is None:
        raise ValueError("Writer release has no selected web archive; publish its reusable WebAssembly artifact first")
    if not re.fullmatch(r"sha256:[a-f0-9]{64}", asset.get("digest") or ""):
        raise ValueError("GitHub asset SHA-256 required")
    return validate_web_pin(dict(id="writer-web", repository=repository, version=tag[8:], tag=tag,
        commit=resolve_commit(repository, tag), releaseId=release["id"], assetId=asset["id"],
        asset=name, size=asset["size"], sha256=asset["digest"][7:], rid="browser-wasm",
        entryPoint="wwwroot/index.html", archive="zip", invocation="web", publishMode="web", prerelease=release["prerelease"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    check = sub.add_parser("validate-lock"); check.add_argument("lock"); check.add_argument("--allow-incomplete", action="store_true")
    build = sub.add_parser("assemble"); build.add_argument("lock"); build.add_argument("--output", required=True); build.add_argument("--cache", required=True); build.add_argument("--commit", required=True); build.add_argument("--offline", action="store_true")
    pin = sub.add_parser("pin"); pin.add_argument("lock"); pin.add_argument("--product", choices=PRODUCTS, required=True); pin.add_argument("--tag", required=True); pin.add_argument("--asset", required=True); pin.add_argument("--publish-mode", choices=("self-contained", "framework-dependent", "native-aot"), required=True); pin.add_argument("--suite-version")
    web = sub.add_parser("pin-web"); web.add_argument("--tag", required=True); web.add_argument("--asset", required=True); web.add_argument("--output", required=True)
    fetch = sub.add_parser("fetch-web"); fetch.add_argument("lock"); fetch.add_argument("--cache", required=True); fetch.add_argument("--output", required=True); fetch.add_argument("--offline", action="store_true")
    args = parser.parse_args()
    if args.command == "validate-lock":
        validate_lock(read_json(args.lock), args.allow_incomplete); print("Office lock validated")
    elif args.command == "assemble":
        print(assemble(args.lock, args.output, args.cache, args.commit, args.offline))
    elif args.command == "pin":
        print(json.dumps(pin_release(args.lock, args.product, args.tag, args.asset, args.publish_mode, args.suite_version), indent=2))
    elif args.command == "pin-web":
        write_json(args.output, pin_web(args.tag, args.asset)); print(args.output)
    else:
        pin = validate_web_pin(read_json(args.lock)); extract(acquire(pin, args.cache, args.offline), args.output)
        if not (Path(args.output) / pin["entryPoint"]).is_file():
            raise ValueError("Writer web entry point missing")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, zipfile.BadZipFile) as error:
        print("Office integration failed: " + str(error), file=sys.stderr)
        sys.exit(1)
