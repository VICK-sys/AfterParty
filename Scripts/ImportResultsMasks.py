import hashlib
import json
from pathlib import Path

from PIL import Image


project = Path(__file__).resolve().parents[1]
root = project / 'Assets/Resources/VanillaResults'
folder = root / 'images/results-bf/resultsPERFECT/tickleFight'
reference = project / 'Temp/ResultsReference'
entries = json.loads((reference / 'masks.json').read_text())
animation = json.loads((folder / 'Animation.json').read_text())
if 'RB' in animation['AN']:
    raise RuntimeError('Run ImportVanillaResults.py before packing results masks.')
atlas = json.loads((folder / 'spritemap1.json').read_text(encoding='utf-8-sig'))
base = Image.open(folder / 'spritemap1.png').convert('RGBA')
width = max(4096, base.width)
packed, names = [], []
x, y, row = 2, base.height + 2, 0
for i, file in enumerate(entries['frames']):
    image = Image.open(reference / file).convert('RGBA')
    if x + image.width + 2 > width:
        x, y, row = 2, y + row + 2, 0
    packed.append((image, x, y))
    name = f'results-mask-{i}'
    names.append(name)
    atlas['ATLAS']['SPRITES'].append({'SPRITE': {'name': name, 'x': x, 'y': y, 'w': image.width, 'h': image.height, 'rotated': False}})
    x += image.width + 2
    row = max(row, image.height)
output = Image.new('RGBA', (width, y + row + 2))
output.paste(base, (0, 0))
for image, x, y in packed:
    output.paste(image, (x, y))
output.save(folder / 'spritemap1.png')
animation['AN']['RB'] = {'bounds': entries['bounds'], 'frames': names}
(folder / 'Animation.json').write_text(json.dumps(animation), encoding='utf-8')
(folder / 'spritemap1.json').write_text(json.dumps(atlas), encoding='utf-8')
manifest_path = root / 'import-manifest.json'
manifest = json.loads(manifest_path.read_text())
for name in ['Animation.json', 'spritemap1.json', 'spritemap1.png']:
    path = folder / name
    entry = manifest['files'][path.relative_to(root).as_posix()]
    entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    entry['transform'] = 'HaxeFlixel mask bake'
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(f'Packed {len(names)} masked frames into the BF tickle fight atlas.')
