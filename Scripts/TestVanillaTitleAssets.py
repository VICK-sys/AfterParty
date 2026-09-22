import argparse
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image

from TestVideoCompatibility import check_encoding


def check_assets(root):
    assets = root / 'Assets'
    title = assets / 'Resources/VanillaTitle'
    manifest = json.loads((title / 'import-manifest.json').read_text())
    for entry in manifest['assets'].values():
        path = assets / entry['path']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
        if path.suffix == '.mp4':
            check_encoding(path)
    for name in ['logoBumpin', 'gfDanceTitle', 'fonts/bold']:
        width, height = Image.open(title / f'{name}.png').size
        frames = ET.parse(title / f'{name}.xml').getroot()
        assert len(frames) > 0, name
        for frame in frames:
            assert int(frame.attrib['x']) + int(frame.attrib['width']) <= width, frame.attrib['name']
            assert int(frame.attrib['y']) + int(frame.attrib['height']) <= height, frame.attrib['name']
    assert (title / 'fonts/bold.png').read_bytes() == (assets / 'Resources/FunkinPause/bold.png').read_bytes()
    assert (title / 'fonts/bold.xml').read_bytes() == (assets / 'Resources/FunkinPause/bold.xml').read_bytes()
    assert (title / 'audio/freakyMenu.ogg').read_bytes() == (assets / 'VanillaMenu/freakyMenu.ogg').read_bytes()
    lines = [line for line in (title / 'introText.txt').read_text().splitlines() if line]
    assert len(lines) == 60
    assert all(len(line.split('--')) == 2 for line in lines)
    assert json.loads((title / 'audio/freakyMenu-metadata.json').read_text())['timeChanges'][0]['bpm'] == 102
    assert json.loads((title / 'audio/girlfriendsRingtone-metadata.json').read_text())['timeChanges'][0]['bpm'] == 160
    data = json.loads((title / 'title-screen-text/Animation.json').read_text())
    labels = {frame['N']: (frame['I'], frame['DU']) for layer in data['AN']['TL']['L'] for frame in layer['FR'] if 'N' in frame}
    assert labels == {'Idle': (0, 45), 'Confirm': (45, 8)}, labels
    assert data['MD']['FRT'] == 24
    print(f'Title assets passed: {len(manifest["assets"])} hashes, atlas bounds, shared font/music, 60 splashes, BPM, and prompt labels.')


def compare(reference, actual):
    import numpy as np
    results = {}
    for name in ['credits', 'title', 'confirm']:
        source = np.asarray(Image.open(reference / f'{name}-reference.png').convert('RGB'), dtype=np.int16)
        target = np.asarray(Image.open(actual / f'{name}.png').convert('RGB'), dtype=np.int16)
        assert source.shape == target.shape
        difference = np.abs(source - target)
        results[name] = {'meanChannelError': float(difference.mean()), 'maximumChannelError': int(difference.max()),
                         'pixelsAboveTwo': int((difference.max(axis=2) > 2).sum())}
    print(json.dumps(results, indent=2))
    (actual / 'comparison.json').write_text(json.dumps(results, indent=2) + '\n')
    assert all(result['meanChannelError'] < 0.15 for result in results.values()), results
    blank = np.asarray(Image.open(actual / 'blank-control.png').convert('RGB'))
    assert blank.max() == 0
    hue = np.asarray(Image.open(actual / 'title-hue.png').convert('RGB'))
    normal = np.asarray(Image.open(actual / 'title.png').convert('RGB'))
    assert np.mean(np.abs(hue.astype(float) - normal)) > 5
    print('Reference comparison, blank control, and hue control passed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--actual', type=Path)
    args = parser.parse_args()
    check_assets(args.root)
    if args.reference and args.actual:
        compare(args.reference, args.actual)
