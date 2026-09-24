import json
from pathlib import Path

import numpy as np
from PIL import Image


root = Path(__file__).resolve().parents[1]
reference = root / 'Builds/DebugDisplayReference'
actual = root / 'Builds/DebugDisplayValidation'


def load(path):
    return np.asarray(Image.open(path).convert('RGB'), dtype=float)


control = load(actual / 'off-control.png')
results = {}
for mode, height in [('advanced', 207), ('simple', 67)]:
    expected = load(reference / (mode + '.png'))[10:10 + height, 10:250]
    rendered = load(actual / (mode + '.png'))[10:10 + height, 10:250]
    blank = control[10:10 + height, 10:250]
    error = float(np.abs(expected - rendered).mean())
    control_error = float(np.abs(expected - blank).mean())
    assert error < 1.5, f'{mode} differs from the reference: {error}'
    assert control_error > error * 10, f'{mode} blank control did not fail'
    results[mode] = {'meanChannelError': error, 'blankControlError': control_error}

transparent = load(actual / 'transparent.png')
large = load(actual / 'large.png')
assert np.array_equal(transparent[:230, :260], large[:230, :260]), 'Desktop resizing scaled the overlay'
assert np.array_equal(transparent[15, 15], control[15, 15]), 'Zero opacity retained the panel'
assert np.abs(transparent[20:65, 20:220] - control[20:65, 20:220]).mean() > 5, 'Zero opacity hid the labels'
advanced = load(actual / 'advanced.png')
for y in (66, 133, 182):
    assert advanced[y - 1:y + 2, 24:236].mean(axis=(1, 2)).max() > 130, f'Graph at {y} is missing'

(actual / 'render-comparison.json').write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
