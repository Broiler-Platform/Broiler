"""Resolve four immutable Windows product assets and package a small demo bootstrapper."""
import argparse
from functools import cmp_to_key
import json
from pathlib import Path
import re
import shutil
import subprocess
import zipfile

import office_artifacts as artifacts

PRODUCTS = {"browser": "browser-v", "writer": "writer-v", "cell": "cells-v", "plate": "plate-v"}
FIELDS = ("id", "version", "rid", "asset", "size", "sha256", "archive", "entryPoint", "invocation", "publishMode")


def api(path):
    return json.loads(subprocess.check_output(["gh", "api", "repos/" + path]))


def candidate(product, releases, tag=None):
    prefix = PRODUCTS[product]
    found = []
    for release in releases:
        name = release["tag_name"]
        if release["draft"] or not name.startswith(prefix) or (tag and name != tag):
            continue
        version = name[len(prefix):]
        try:
            artifacts.version(version)
        except ValueError:
            continue
        stem = "Broiler." + artifacts.PRODUCTS[product][0].split(".", 1)[1] + "-" + version
        names = [stem + "-win-x64-self-contained.zip"]
        if product == "plate":
            names.append(stem + "-win-x64.zip")
        assets = [a for a in release["assets"] if a["name"] in names]
        if assets:
            if len(assets) != 1:
                raise ValueError("Ambiguous native product asset: " + name)
            found.append((version, release, assets[0]))
    if not found:
        raise ValueError("No published Windows runtime-independent artifact for " + product)
    return max(found, key=cmp_to_key(lambda a, b: artifacts.compare_versions(a[0], b[0])))


def resolve(directory, version, overrides, verification_only=False):
    if not re.fullmatch(r"\d+\.\d+\.\d+-demo\.[1-9]\d*", version):
        raise ValueError("An explicit demo.N suite version is required")
    if set(overrides) - set(PRODUCTS) or not all(isinstance(v, str) for v in overrides.values()):
        raise ValueError("Overrides must map known product IDs to explicit tags")
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=normal"], text=True).strip()
    if dirty and not verification_only:
        raise ValueError("Release sources must be committed; use --verification-only for a local rehearsal")
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=False)
    assets = directory / "release-assets"
    assets.mkdir()
    lock_path = directory / "selection.lock.json"
    lock = dict(schemaVersion=1, suite=version, channel="preview", minimumLauncherVersion=version,
                components=[], pendingComponents=list(artifacts.PRODUCTS))
    artifacts.write_json(lock_path, lock)
    # Existing provenance validation and download code now uses authenticated metadata reads.
    artifacts.api = api
    for product in PRODUCTS:
        repo = "Broiler-Platform/" + artifacts.PRODUCTS[product][0]
        rows = []
        for page in range(1, 21):
            batch = api(repo + f"/releases?per_page=100&page={page}")
            rows.extend(batch)
            if len(batch) < 100:
                break
        else:
            raise ValueError("Release scan exceeds bounds")
        _, release, asset = candidate(product, rows, overrides.get(product))
        mode = "self-contained"
        metadata = next((a for a in release["assets"] if a["name"] == "component-release.json"), None)
        if metadata:
            manifest = artifacts.component_metadata(repo, release["tag_name"], metadata)
            matching = [a for a in manifest["artifacts"] if a["asset"] == asset["name"]]
            if len(matching) != 1:
                raise ValueError("Product descriptor omits the selected native asset")
            mode = matching[0]["publishMode"]
        artifacts.pin_release(lock_path, product, release["tag_name"], asset["name"], mode)
        lock = artifacts.read_json(lock_path)
        pin = next(p for p in lock["components"] if p["id"] == product)
        archive = artifacts.acquire(pin, directory / "cache")
        artifacts.extract(archive, directory / "smoke-products" / product)
        if not (directory / "smoke-products" / product / pin["entryPoint"]).is_file():
            raise ValueError("Product executable is missing")
        shutil.copyfile(archive, assets / pin["asset"])
        print(product, pin["tag"], pin["sha256"])
    lock["profile"] = "desktop-bootstrap-demo"
    lock["verificationOnly"] = verification_only
    lock["pendingComponents"] = []
    artifacts.write_json(lock_path, lock)
    source_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    manifest = dict(schemaVersion=1, officeVersion=version, channel="preview", tag="office-v" + version,
                    commit=source_sha, minimumLauncherVersion=version, demo=True,
                    components=[{key: p[key] for key in FIELDS} for p in lock["components"]])
    artifacts.write_json(assets / "office-release.json", manifest)
    shutil.copyfile(lock_path, assets / lock_path.name)


def package(directory):
    directory = Path(directory)
    assets = directory / "release-assets"
    manifest = artifacts.read_json(assets / "office-release.json")
    lock = artifacts.read_json(directory / "selection.lock.json")
    if {p["id"] for p in manifest["components"]} != set(PRODUCTS) or len(manifest["components"]) != 4:
        raise ValueError("The demo must contain exactly four products")
    for pin in lock["components"]:
        artifacts.verify_archive(assets / pin["asset"], pin)
    exe = directory / "launcher/Broiler.Launcher.Windows.exe"
    if not exe.is_file() or list(exe.parent.glob("*.dll")):
        raise ValueError("Expected a NativeAOT launcher without managed/runtime DLLs")
    readme = ("Broiler Office Windows x64 demo\n\nExtract this ZIP completely and open Broiler.Launcher.Windows.exe.\n"
              "Download & start installs the selected app for your current user and starts it.\n"
              "Internet is required for first installation; installed apps can be started offline.\n"
              "This is an unsigned demonstration. Publisher signing is deferred.\n"
              "Keep office-release.json beside the launcher. No .NET runtime is required.\n")
    if lock.get("verificationOnly"):
        readme = "LOCAL VERIFICATION BUILD — not a published release.\n\n" + readme
    target = assets / ("Broiler-Office-" + manifest["officeVersion"] + "-win-x64-bootstrap.zip")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.write(exe, exe.name)
        archive.write(assets / "office-release.json", "office-release.json")
        archive.writestr("README.txt", readme)
        archive.write(Path(__file__).resolve().parents[1] / "LICENSE", "LICENSE")
    paths = sorted(p for p in assets.iterdir() if p.is_file() and p.name != "SHA256SUMS.txt")
    (assets / "SHA256SUMS.txt").write_text("".join(artifacts.digest(p) + "  " + p.name + "\n" for p in paths), encoding="ascii")
    print(target)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("resolve", "package"))
    parser.add_argument("--directory", required=True)
    parser.add_argument("--version")
    parser.add_argument("--verification-only", action="store_true")
    parser.add_argument("--overrides", default="{}")
    args = parser.parse_args()
    if args.operation == "resolve":
        resolve(args.directory, args.version or "", json.loads(args.overrides or "{}"), args.verification_only)
    else:
        package(args.directory)
