import hashlib
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path


root = Path(__file__).resolve().parents[1]
assets = root / 'Assets/Resources/VanillaOptions'
manifest = json.loads((assets / 'import-manifest.json').read_text())
checks = 0
for name, entry in manifest['assets'].items():
    assert hashlib.sha256((assets / name).read_bytes()).hexdigest() == entry['sha256'], name
    checks += 1
for name, digest in manifest['censoredVideos'].items():
    assert hashlib.sha256((root / 'Assets/StreamingAssets/VanillaOptions' / name).read_bytes()).hexdigest() == digest, name
    checks += 1
for name in ['bold', 'default']:
    xml = (assets / (name + '.xml')).read_text(encoding='utf-8-sig')
    xml = re.sub(r'name="([^"]*)"', lambda m: 'name="' + m[1].replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;') + '"', xml)
    glyphs = ET.fromstring(xml)
    assert len(glyphs) >= 200
    assert len({re.sub(r'[0-9]{4}$', '', glyph.attrib['name']) for glyph in glyphs}) >= 60
    checks += 2
controls = (root / 'Assets/Scripts/VanillaControls.cs').read_text()
assert len(re.findall(r'Add\("[A-Z_]+",', controls)) == 25
checks += 1
print(f'Options asset checks passed: {checks}')
