import hashlib
import json
from pathlib import Path

from PIL import Image


project = Path(__file__).resolve().parents[1]
root = project / 'Assets/Resources/VanillaResults'
folder = root / 'images/results-pico/resultsGOOD'
reference = project / 'Temp/ResultsReference'
entries = json.loads((reference / 'filters.json').read_text())
assert len(entries) == 24, len(entries)
animation = json.loads((folder / 'Animation.json').read_text())
if '"RF"' in (folder / 'Animation.json').read_text():
    raise RuntimeError('Run ImportVanillaResults.py before packing results filters.')
atlas = json.loads((folder / 'spritemap1.json').read_text(encoding='utf-8-sig'))
base = Image.open(folder / 'spritemap1.png').convert('RGBA')
packed = []
x, y, row = 2, base.height + 2, 0
for i, entry in enumerate(entries):
    image = Image.open(reference / entry['file']).convert('RGBA')
    if x + image.width + 2 > base.width:
        x, y, row = 2, y + row + 2, 0
    packed.append((image, x, y))
    entry['sprite'] = f'results-filter-{i}'
    atlas['ATLAS']['SPRITES'].append({'SPRITE': {'name': entry['sprite'], 'x': x, 'y': y, 'w': image.width, 'h': image.height, 'rotated': False}})
    x += image.width + 2
    row = max(row, image.height)
output = Image.new('RGBA', (base.width, y + row + 2))
output.paste(base, (0, 0))
for image, x, y in packed:
    output.paste(image, (x, y))
output.save(folder / 'spritemap1.png')
lookup = {entry['key']: entry for entry in entries}
for symbol in animation['SD']['S'] + [dict(SN=animation['AN']['SN'], TL=animation['AN']['TL'])]:
    for layer_index, layer in enumerate(symbol['TL']['L']):
        for frame in layer['FR']:
            for element_index, element in enumerate(frame.get('E', [])):
                key = f'{symbol["SN"]}|{layer_index}|{frame["I"]}|{element_index}'
                if key in lookup:
                    item = lookup.pop(key)
                    element['SI']['RF'] = {'N': item['sprite'], 'MX': item['matrix'], 'bounds': item['bounds']}
assert not lookup, lookup
(folder / 'Animation.json').write_text(json.dumps(animation), encoding='utf-8')
(folder / 'spritemap1.json').write_text(json.dumps(atlas), encoding='utf-8')
manifest_path = root / 'import-manifest.json'
manifest = json.loads(manifest_path.read_text())
for name in ['Animation.json', 'spritemap1.json', 'spritemap1.png']:
    path = folder / name
    entry = manifest['files'][path.relative_to(root).as_posix()]
    entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    entry['transform'] = 'HaxeFlixel movie clip filter bake'
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(f'Packed {len(entries)} filtered movie clips into the Pico GOOD atlas.')
