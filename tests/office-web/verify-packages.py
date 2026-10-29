"""Verify package-owned assets and execute the shipped MSBuild rejection gates.

Pass Graphics.WebAssembly and Hosting.WebAssembly .nupkg paths. No NuGet cache is
modified: probes expand copies into a new temporary directory.
"""
import hashlib
import json
import pathlib
import subprocess
import sys
import tempfile
import zipfile
from xml.sax.saxutils import escape


def probe(project, success):
    result = subprocess.run(['dotnet', 'msbuild', str(project), '-t:Probe', '-nologo'], capture_output=True, text=True)
    if (result.returncode == 0) != success:
        raise AssertionError(result.stdout + result.stderr)


with tempfile.TemporaryDirectory(prefix='broiler-web-package-') as directory:
    root = pathlib.Path(directory)
    graphics, hosting = root / 'graphics', root / 'hosting'
    with zipfile.ZipFile(sys.argv[1]) as archive:
        archive.extractall(graphics)
    with zipfile.ZipFile(sys.argv[2]) as archive:
        archive.extractall(hosting)
    manifest = json.loads((graphics / 'browser/replay-manifest.json').read_text(encoding='utf-8-sig'))
    replay = graphics / 'browser/broiler.graphics.webassembly.js'
    original = replay.read_bytes()
    assert manifest['sha256'].lower() == hashlib.sha256(original).hexdigest()
    assert manifest['protocol'] == 'broiler.canvas/1'
    assert b'createCanvasHost' in (hosting / 'browser/broiler.hosting.webassembly.js').read_bytes()
    project = root / 'probe.proj'
    project.write_text(f'''<Project>
      <PropertyGroup><RuntimeIdentifier>browser-wasm</RuntimeIdentifier></PropertyGroup>
      <Import Project="{escape(str(graphics / 'buildTransitive/Broiler.Graphics.WebAssembly.targets'))}" />
      <Import Project="{escape(str(hosting / 'buildTransitive/Broiler.Hosting.WebAssembly.targets'))}" />
      <Target Name="Probe" DependsOnTargets="VerifyBroilerCanvasReplay">
        <Error Condition="'@(Content->Count())' != '3'" Text="Missing packaged browser assets" />
      </Target>
    </Project>''', encoding='utf-8')
    probe(project, True)
    consumer = root / 'consumer'
    consumer.mkdir()
    (consumer / 'Program.cs').write_text('System.Console.WriteLine("package asset probe");', encoding='utf-8')
    (consumer / 'Consumer.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
      <PropertyGroup><TargetFramework>net10.0</TargetFramework><PublishTrimmed>true</PublishTrimmed><RunAOTCompilation>false</RunAOTCompilation></PropertyGroup>
      <Import Project="{escape(str(graphics / 'buildTransitive/Broiler.Graphics.WebAssembly.targets'))}" />
      <Import Project="{escape(str(hosting / 'buildTransitive/Broiler.Hosting.WebAssembly.targets'))}" />
    </Project>''', encoding='utf-8')
    published = root / 'published'
    published_result = subprocess.run(['dotnet', 'publish', str(consumer / 'Consumer.csproj'), '-c', 'Release', '-m:1', '-o', str(published)], capture_output=True, text=True)
    if published_result.returncode:
        raise AssertionError(published_result.stdout + published_result.stderr)
    assert (published / 'wwwroot/broiler.graphics.webassembly.js').read_bytes() == original
    assert (published / 'wwwroot/broiler.hosting.webassembly.js').read_bytes() == (hosting / 'browser/broiler.hosting.webassembly.js').read_bytes()
    assert json.loads((published / 'wwwroot/broiler.graphics.replay.json').read_text()) == manifest
    replay.write_bytes(original + b'\n// corrupt package fixture\n')
    probe(project, False)
    replay.write_bytes(original)
    (root / 'wwwroot').mkdir()
    (root / 'wwwroot/broiler.graphics.webassembly.js').write_text('// stale app fixture', encoding='utf-8')
    probe(project, False)
    print(json.dumps({'packageVersion': manifest['packageVersion'], 'protocol': manifest['protocol'],
                      'checks': ['packaged assets', 'matching replay hash', 'SDK publish includes packaged assets', 'corrupt replay rejected', 'stale app copy rejected'], 'pass': True}))
