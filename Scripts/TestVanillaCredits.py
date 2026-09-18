import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def run(reference, actual):
    root = Path(__file__).resolve().parents[1]
    assets = root / 'Assets/Resources/VanillaCredits'
    manifest = json.loads((assets / 'import-manifest.json').read_text(encoding='utf-8'))
    for name, expected in manifest['files'].items():
        assert hashlib.sha256((assets / name).read_bytes()).hexdigest() == expected, name
    original = json.loads((assets / 'credits.json').read_text(encoding='utf-8-sig'))
    party = json.loads((assets / 'unity-party.json').read_text(encoding='utf-8'))
    baked = json.loads((assets / 'lines.json').read_text(encoding='utf-8'))
    expected_lines = []
    for entry in original['entries'] + party['entries']:
        if entry.get('header') is not None:
            expected_lines.append((entry['header'], True))
        expected_lines.extend((member['line'], False) for member in entry.get('body', []))
    assert expected_lines == [(line['text'], line['header']) for line in baked['lines']]
    names = {line['text'] for line in baked['lines']}
    assert {'Team Determination', 'Rei the Goat', 'UniBrine', 'St4bility aka Thesnakerox'} <= names
    for line in baked['lines']:
        assert line['variants'][0]['minWidth'] == 1280
        widths = [variant['minWidth'] for variant in line['variants']]
        assert widths == sorted(set(widths)) and widths[-1] <= 1600
        for variant in line['variants']:
            image = Image.open(assets / (variant['atlas'] + '.png'))
            assert variant['x'] >= 0 and variant['y'] >= 0
            assert variant['x'] + variant['width'] <= image.width
            assert variant['y'] + variant['height'] <= image.height
    print(f"Credits data passed: {len(original['entries'])} original sections, {len(expected_lines)} lines, Unity Party developers, and atlas bounds.")
    if actual is None:
        return
    control = np.asarray(Image.open(actual / 'blank-control.png').convert('RGB'))
    assert control.max() == 0, 'Blank render control is not black.'
    results = {}
    for name in ['opening-1280', 'fractional-1280', 'middle-1280', 'later-1280', 'middle-1600', 'later-1600',
                 'party-1600', 'middle-1440', 'later-1440', 'party-1280', 'fade-cover-1280', 'fade-reveal-1280']:
        expected = np.asarray(Image.open(reference / (name + '.png')).convert('RGB'), dtype=np.int16)
        rendered = np.asarray(Image.open(actual / (name + '.png')).convert('RGB'), dtype=np.int16)
        assert expected.shape == rendered.shape, name
        difference = np.abs(expected - rendered)
        results[name] = {'meanChannelError': float(difference.mean()), 'maximumChannelError': int(difference.max()),
                         'pixelsAboveTwo': int((difference.max(axis=2) > 2).sum())}
        assert (expected > 100).sum() > 1000, f'{name}: reference is blank.'
    print(json.dumps(results, indent=2))
    (actual / 'visual-comparison.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
    for name, result in results.items():
        if not name.startswith('fade-'):
            assert result['meanChannelError'] < 0.02, f'{name}: source rendering differs.'
        if name != 'fractional-1280':
            assert result['maximumChannelError'] <= 1, f'{name}: source pixels differ beyond color rounding.'
    print('Source render comparison and blank control passed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, default=Path('Builds/CreditsReference'))
    parser.add_argument('--actual', type=Path)
    args = parser.parse_args()
    run(args.reference, args.actual)
