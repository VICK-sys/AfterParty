import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image


root = Path(__file__).resolve().parent.parent / 'Assets/Resources/FunkinPause'
manifest = json.loads((root / 'import-manifest.json').read_text())
for name, entry in manifest['releaseFiles'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == entry['sha256'], name
for name, expected in manifest['bitmapFonts'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, name
atlas = Image.open(root / 'bold.png')
frames = list(ET.parse(root / 'bold.xml').getroot())
prefixes = {frame.attrib['name'][:-4] for frame in frames}
for frame in frames:
    x, y, width, height = (int(frame.attrib[key]) for key in ['x', 'y', 'width', 'height'])
    assert x >= 0 and y >= 0 and x + width <= atlas.width and y + height <= atlas.height
labels = 'RESUME RESTART SONG CHANGE DIFFICULTY ENABLE PRACTICE MODE EXIT TO MENU EASY NORMAL HARD ERECT NIGHTMARE BACK'
assert set(labels.replace(' ', '')) <= prefixes
for size, advance, height in [(16, 9, 19), (32, 19, 33)]:
    data = json.loads((root / f'vcr{size}.json').read_text())
    assert data['advance'] == advance and len(data['glyphs']) == 95
    assert all(glyph['height'] == height for glyph in data['glyphs'])
assert len(list((root / 'stickers').glob('*.png'))) == 3
assert len(list((root / 'stickerSounds').glob('*.ogg'))) == 8
print(f'PAUSE ASSETS PASSED: {len(manifest["releaseFiles"])} source assets, 190 VCR glyphs, {len(frames)} bold frames.')
