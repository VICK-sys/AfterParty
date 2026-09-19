import json
from pathlib import Path

import numpy as np
from PIL import Image


project = Path(__file__).resolve().parents[1]
unity = project / 'Temp/Results'
reference = project / 'Temp/ResultsReference'
names = [f'{character}-{rank}' for character in ['bf', 'pico'] for rank in range(6)]
names += ['pico-fat', 'pico-cass', 'pico-flash', 'pico-great-flash', 'bf-safe']
metrics = {}


def error(a, b):
    return float(np.abs(a.astype(float) - b.astype(float)).mean())


for name in names:
    expected = np.asarray(Image.open(reference / f'{name}.png').convert('RGB'))
    actual = np.asarray(Image.open(unity / f'{name}-characters.png').convert('RGB'))
    assert actual.shape == expected.shape, name
    metrics[name] = error(actual, expected)
    print(f'{name}: {metrics[name]:.5f}')
    assert metrics[name] < 0.25, f'{name} differs from HaxeFlixel: {metrics[name]}'
    blank = np.full_like(expected, [254, 204, 92])
    assert error(blank, expected) > 0.25, f'{name}: blank control passed'
    assert error(np.roll(expected, 2, axis=1), expected) > 0.25, f'{name}: shifted control passed'

(unity / 'reference-comparison.json').write_text(json.dumps(metrics, indent=2) + '\n')
print('Results rendering passed: 17 reference frames and blank/shifted controls.')
