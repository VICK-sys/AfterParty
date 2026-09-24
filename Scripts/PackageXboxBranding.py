import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image


def apply_branding(mapping, work, version=None):
    source = Path(__file__).resolve().parents[1] / 'Assets/Platform/Xbox'
    work = Path(work).resolve()
    assets = work / 'Branding'
    assets.mkdir(parents=True, exist_ok=True)
    hashes = {}
    for target, previous in list(mapping.items()):
        name = target.replace('\\', '/').split('/')[-1]
        if not target.lower().startswith('assets\\') or not name.endswith('.png'):
            continue
        stem = name.split('.')[0]
        original = source / (stem + '.png')
        if not original.is_file():
            raise ValueError(f'Missing branding source: {original}')
        destination = assets / name
        with Image.open(previous) as old:
            size = old.size
        if stem == 'SplashScreen':
            original = source / ('SplashScreen100.png' if size == (620, 300) else 'SplashScreen.png')
        with Image.open(original) as image:
            if image.size == size:
                shutil.copyfile(original, destination)
            else:
                image.convert('RGBA').resize(size, Image.Resampling.LANCZOS).save(destination)
        mapping[target] = destination
        hashes[target.replace('\\', '/')] = hashlib.sha256(destination.read_bytes()).hexdigest()
    if not any('SplashScreen' in name for name in hashes) or not any('Square150x150Logo' in name for name in hashes):
        raise ValueError('Package map must contain the splash screen and library icon.')
    manifest = Path(mapping['AppxManifest.xml']).read_text(encoding='utf-8-sig')
    if version:
        manifest, count = re.subn(r'(<Identity\b[^>]*\bVersion=")[^"]+', lambda match: match[1] + version, manifest, count=1)
        if count != 1:
            raise ValueError('Missing package version.')
    manifest = re.sub(r'(<uap:SplashScreen\b[^>]*\bBackgroundColor=")[^"]+', r'\g<1>#000000', manifest)
    ET.fromstring(manifest)
    manifest_path = work / 'AppxManifest.xml'
    manifest_path.write_text(manifest, encoding='utf-8')
    mapping['AppxManifest.xml'] = manifest_path
    (work / 'branding-hashes.json').write_text(json.dumps(hashes, indent=2) + '\n', encoding='utf-8')
    return hashes


def verify(package, hashes):
    with zipfile.ZipFile(package) as archive:
        for target, expected in hashes.items():
            if hashlib.sha256(archive.read(target)).hexdigest() != expected:
                raise ValueError(f'Packaged branding mismatch: {target}')
        manifest = ET.fromstring(archive.read('AppxManifest.xml'))
        splash = manifest.find('.//{*}SplashScreen')
        if splash.attrib.get('BackgroundColor') != '#000000':
            raise ValueError('Incorrect splash background.')
    print(f'Verified {len(hashes)} packaged branding images and splash background.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--map', type=Path)
    parser.add_argument('--work', required=True, type=Path)
    parser.add_argument('--version')
    parser.add_argument('--verify', type=Path)
    args = parser.parse_args()
    if args.verify:
        verify(args.verify, json.loads((args.work / 'branding-hashes.json').read_text()))
        return
    mapping = {}
    for line in args.map.read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'"([^"]+)" "([^"]+)"', line)
        if match:
            mapping[match[2]] = Path(match[1])
    apply_branding(mapping, args.work, args.version)
    (args.work / 'package.map.txt').write_text('[Files]\n' + '\n'.join(f'"{source}" "{target}"' for target, source in mapping.items()) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
