import argparse
import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from PackageXboxBranding import apply_branding


def verify_master_sources(mapping):
    for name in ['UnityPlayer.dll', 'UnityPlayer.winmd', 'baselib.dll', 'GameAssembly.dll', 'FridayNight.exe']:
        source = mapping[name]
        if source.parent.name.lower() != 'master':
            raise ValueError(f'{name} must come from the Master configuration: {source}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--export', required=True, type=Path)
    parser.add_argument('--version', required=True)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--dependency', required=True, type=Path)
    parser.add_argument('--sdk-bin', type=Path, default=Path('C:/Program Files (x86)/Windows Kits/10/bin/10.0.26100.0/x64'))
    args = parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', args.version):
        raise ValueError('Use a four-part Xbox package version.')
    repository = Path(__file__).resolve().parents[1]
    export = args.export.resolve()
    project = export / 'FridayNight'
    output = args.output.resolve()
    work = output.with_name(output.name + '-Packaging')
    output.mkdir(parents=True, exist_ok=True)
    work.mkdir(parents=True, exist_ok=True)
    mapping = {}
    previous = export / 'build/obj/FridayNight/x64/Master/package.map.txt'
    for line in previous.read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'"([^"]+)" "([^"]+)"', line)
        if match and not match[2].lower().startswith('data\\'):
            mapping[match[2]] = Path(match[1])
    items = ET.parse(project / 'Unity Data.vcxitems')
    ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
    for item in items.findall('.//m:None', ns):
        include = item.attrib['Include']
        prefix = '$(MSBuildThisFileDirectory)'
        if include.startswith(prefix + 'Data\\') and item.findtext('m:DeploymentContent', namespaces=ns) == 'true':
            relative = include[len(prefix):]
            mapping[relative] = project / relative
    assert len(mapping) > 2000
    assert mapping['GameAssembly.dll'] == export / 'build/bin/x64/Master/GameAssembly.dll'
    manifest = mapping['AppxManifest.xml'].read_text(encoding='utf-8-sig')
    manifest, count = re.subn(r'(<Identity\b[^>]*\bVersion=")[^"]+("\s*)', lambda match: match[1] + args.version + match[2], manifest, count=1)
    assert count == 1
    identity = ET.fromstring(manifest).find('{*}Identity').attrib
    assert identity['Name'] == 'Template2D' and identity['Publisher'] == 'CN=Rei'
    assert identity['Version'] == args.version and identity['ProcessorArchitecture'] == 'x64'
    manifest = manifest.replace('AfterParty', 'Friday Fight Funkin&apos;')
    manifest_path = work / 'AppxManifest.xml'
    manifest_path.write_text(manifest, encoding='utf-8')
    mapping['AppxManifest.xml'] = manifest_path
    streaming = repository / 'Assets/StreamingAssets'
    videos = [path for path in streaming.rglob('*.mp4') if 'Source' not in path.relative_to(streaming).parts]
    for original in videos:
        relative = str(Path('Data/StreamingAssets') / original.relative_to(streaming))
        exported = mapping[relative]
        assert hashlib.sha256(original.read_bytes()).digest() == hashlib.sha256(exported.read_bytes()).digest(), relative
    for target, source in mapping.items():
        assert source.is_file(), (target, source)
    apply_branding(mapping, work)
    verify_master_sources(mapping)
    map_path = work / 'package.map.txt'
    map_path.write_text('[Files]\n' + '\n'.join(f'"{source}" "{target}"' for target, source in mapping.items()) + '\n', encoding='utf-8')
    print(f'Packaging {len(mapping)} files as version {args.version}. All {len(videos)} runtime videos match the workspace.', flush=True)
    with (work / 'makeappx.log').open('w') as log:
        subprocess.run([str(args.sdk_bin / 'makeappx.exe'),
                        'pack', '/f', str(map_path), '/p', str(output / 'AfterParty-Xbox-x64.appx'), '/o'],
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    dependency = output / 'Dependencies/x64/Microsoft.VCLibs.x64.14.00.appx'
    dependency.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.dependency, dependency)
    with zipfile.ZipFile(output / 'AfterParty-Xbox-x64.appx') as package:
        for name in ['UnityPlayer.dll', 'UnityPlayer.winmd', 'baselib.dll', 'GameAssembly.dll', 'FridayNight.exe']:
            with package.open(name) as packed, mapping[name].open('rb') as current:
                if hashlib.file_digest(packed, 'sha256').digest() != hashlib.file_digest(current, 'sha256').digest():
                    raise ValueError(f'Packaged runtime does not match Master: {name}')
    print('Package verified against the Master runtime. Sign before deployment.', flush=True)


if __name__ == '__main__':
    main()
