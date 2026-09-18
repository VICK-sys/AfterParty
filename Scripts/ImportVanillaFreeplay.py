import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path


def normalize(value):
    names = {'ANIMATION': 'AN', 'SYMBOL_DICTIONARY': 'SD', 'Symbols': 'S', 'SYMBOL_name': 'SN',
             'TIMELINE': 'TL', 'LAYERS': 'L', 'Layer_name': 'LN', 'Frames': 'FR', 'index': 'I',
             'duration': 'DU', 'elements': 'E', 'SYMBOL_Instance': 'SI', 'ATLAS_SPRITE_instance': 'ASI',
             'name': 'N', 'firstFrame': 'FF', 'Instance_Name': 'IN', 'transformationPoint': 'TRP',
             'metadata': 'MD', 'framerate': 'FRT'}
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key == 'DecomposedMatrix':
            continue
        if key == 'Matrix3D':
            result['M3D'] = [item[f'm{i // 4}{i % 4}'] for i in range(16)]
        elif key == 'loop':
            result['LP'] = {'loop': 'LP', 'playonce': 'PO', 'singleframe': 'SF'}.get(item, item)
        elif key == 'symbolType':
            result['ST'] = {'graphic': 'G', 'movieclip': 'MC'}.get(item, item)
        else:
            result[names.get(key, key)] = normalize(item)
    return result


def extract_font(executable, offset, name, output):
    count = struct.unpack_from('>H', executable, offset + 4)[0]
    tables = [struct.unpack_from('>4sIII', executable, offset + 12 + 16 * i) for i in range(count)]
    size = max(start + length for _, _, start, length in tables)
    if count < 5 or offset + size > len(executable):
        raise ValueError(f'Invalid embedded font: {name}')
    (output / f'{name}.ttf').write_bytes(executable[offset:offset + size])


def run(source, target):
    target.mkdir(parents=True, exist_ok=True)
    files = []
    for path in (source / 'assets/images/freeplay').rglob('*'):
        if path.is_file():
            files.append((path, target / path.relative_to(source / 'assets/images')))
    for name in ['digital_numbers.png', 'digital_numbers.xml', 'fonts/freeplay-clear.png', 'fonts/freeplay-clear.xml']:
        files.append((source / 'assets/images' / name, target / name))
    for name in ['freeplayRandom/freeplayRandom.ogg']:
        files.append((source / 'assets/music' / name, target / 'audio' / Path(name).name))
    for name in ['fav', 'unfav', 'confirmMenu', 'scrollMenu', 'cancelMenu',
                 'ranks/loss', 'ranks/good', 'ranks/great', 'ranks/excellent', 'ranks/perfect',
                 'ranks/rankinbad', 'ranks/rankinnormal', 'ranks/rankinperfect']:
        path = source / f'assets/sounds/{name}.ogg'
        if path.is_file():
            files.append((path, target / f'audio/{name}.ogg'))
    for path, destination in files:
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
    letters = target / 'freeplay/sortedLetters/Animation.json'
    letters.write_text(json.dumps(normalize(json.loads(letters.read_text(encoding='utf-8-sig')))), encoding='utf-8')
    executable = (source / 'Funkin.exe').read_bytes()
    for offset, name in [(71193156, '5by7'), (70769796, '5by7-bold'), (71840740, 'vcr'), (70507348, 'YoureGone')]:
        extract_font(executable, offset, name, target)
    manifest = {
        'source': 'Funkin-0.8.6 source with local Funkin Windows artwork',
        'executableSha256': hashlib.sha256(executable).hexdigest(),
        'files': {str(dest.relative_to(target)).replace('\\', '/'): hashlib.sha256(dest.read_bytes()).hexdigest()
                  for _, dest in files}
    }
    (target / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(files)} assets and four embedded fonts.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', type=Path, default=Path('Assets/Resources/VanillaFreeplay'))
    args = parser.parse_args()
    run(args.source, args.output)
