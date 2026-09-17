import hashlib
import json
import struct
import xml.etree.ElementTree as ET
from pathlib import Path


root = Path(__file__).resolve().parents[1] / 'Assets/Resources/VanillaStory'
manifest = json.loads((root / 'import-manifest.json').read_text(encoding='utf-8'))
levels = json.loads((root / 'levels.json').read_text(encoding='utf-8'))
assert hashlib.sha256((root / 'levels.json').read_bytes()).hexdigest() == manifest['levelsSha256']
for name, expected in {**manifest['releaseFiles'], **manifest['bitmapFont']}.items():
    path = root / name
    if name.startswith('data/'):
        continue
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, name
assert [level['id'] for level in levels] == ['tutorial', 'week1', 'week2', 'week3', 'week4', 'week5', 'week6', 'week7', 'weekend1', 'sserafim']
animations = 0
rotations = 0
for level in levels:
    assert len(level['songs']) == len(level['songNames'])
    assert set(level['difficulties']).issubset({'easy', 'normal', 'hard'})
    assert (root / (level['titleAsset'] + '.png')).is_file()
    for prop in level['props']:
        path = root / prop['assetPath']
        width, height = struct.unpack('>II', path.with_suffix('.png').read_bytes()[16:24])
        frames = list(ET.parse(path.with_suffix('.xml')).getroot())
        for frame in frames:
            assert 0 <= int(frame.get('x')) <= width - int(frame.get('width')), path
            assert 0 <= int(frame.get('y')) <= height - int(frame.get('height')), path
            rotations += frame.get('rotated') == 'true'
        for animation in prop['animations']:
            matching = [frame.get('name') for frame in frames if frame.get('name').startswith(animation['prefix'])]
            assert matching, (path, animation['name'])
            if animation.get('frameIndices'):
                numbers = {int(name[len(animation['prefix']):]) for name in matching}
                assert any(index in numbers for index in animation['frameIndices']), (path, animation['name'])
            animations += 1
font = json.loads((root / 'vcr32.json').read_text(encoding='utf-8'))
assert font['advance'] == 19 and font['lineHeight'] == 29
assert [glyph['code'] for glyph in font['glyphs']] == list(range(32, 127))
assert rotations > 0
print(f'STORY ASSETS PASSED: {len(levels)} levels, {animations} animations, original hashes, rotated frame bounds, and font metrics.')
