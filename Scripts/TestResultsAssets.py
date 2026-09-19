import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET


root = Path(__file__).resolve().parents[1] / 'Assets/Resources/VanillaResults'
manifest = json.loads((root / 'import-manifest.json').read_text(encoding='utf-8'))
for name, entry in manifest['files'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == entry['sha256'], name

for path in (root / 'images').rglob('*.xml'):
    width, height = struct.unpack('>II', path.with_suffix('.png').read_bytes()[16:24])
    frames = list(ET.parse(path).getroot())
    assert frames, path
    for frame in frames:
        values = frame.attrib
        assert 0 <= int(values['x']) < width and 0 <= int(values['y']) < height, (path, values)
        assert int(values['x']) + int(values['width']) <= width, (path, values)
        assert int(values['y']) + int(values['height']) <= height, (path, values)

for path in (root / 'images').rglob('spritemap1.json'):
    width, height = struct.unpack('>II', path.with_suffix('.png').read_bytes()[16:24])
    atlas = json.loads(path.read_text(encoding='utf-8-sig'))
    names = set()
    for entry in atlas['ATLAS']['SPRITES']:
        sprite = entry['SPRITE']
        names.add(sprite['name'])
        assert 0 <= sprite['x'] < width and 0 <= sprite['y'] < height, path
        assert sprite['x'] + sprite['w'] <= width and sprite['y'] + sprite['h'] <= height, path
    animation = json.loads((path.parent / 'Animation.json').read_text())
    if 'RB' in animation['AN']:
        assert len(animation['AN']['RB']['frames']) == 78
        assert all(name in names for name in animation['AN']['RB']['frames'])

scripts = {
    'BFBedPerfectResults': 'results-bf/resultsPERFECT/bed',
    'BFShitResults': 'results-bf/resultsSHIT',
    'PicoGoodResults': 'results-pico/resultsGOOD',
    'PicoGreatResults': 'results-pico/resultsGREAT',
    'PicoPerfectResults': 'results-pico/resultsPERFECT',
}
for character in ['bf', 'pico']:
    data = json.loads((root / f'players/{character}.json').read_text())['results']
    assert len(data['music']) == 6
    for music in data['music'].values():
        assert (root / f'music/{music}.ogg').is_file(), music
    for rank in ['loss', 'good', 'great', 'excellent', 'perfect', 'perfectGold']:
        for item in data[rank]:
            path = scripts[item['scriptClass']] if 'scriptClass' in item else item['assetPath'].split('resultScreen/')[1]
            path = root / 'images' / path
            if item['renderType'] == 'sparrow':
                assert path.with_suffix('.xml').is_file()
                continue
            animation = json.loads((path / 'Animation.json').read_text())
            labels = {frame['N'] for layer in animation['AN']['TL']['L'] for frame in layer['FR'] if 'N' in frame}
            for field in ['startFrameLabel', 'loopFrameLabel']:
                if field in item:
                    assert item[field] in labels, (path, field)
            assert (path / 'spritemap1.png').is_file() and (path / 'spritemap1.json').is_file()

assert not (root / 'music/resultsMISSING.ogg').exists()
print(f'Results assets passed: {len(manifest["files"])} hashes, atlas bounds, six ranks for BF and Pico, and missing-asset control.')
