import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--reference', type=Path, default=root / 'Builds/FreeplayHeaderReference')
parser.add_argument('--actual', type=Path, default=root / 'Builds/FreeplayHeaderValidation')
args = parser.parse_args()
reference = args.reference
actual = args.actual
blank = np.asarray(Image.open(actual / 'blank.png').convert('RGB'), dtype=float)[:64]
results = {}
for key in ['TAB', 'SPACE', 'intro']:
    expected = Image.new('RGBA', (1280, 720), (0, 0, 0, 255))
    for name in ['FREEPLAY', 'OFFICIAL_OST']:
        image = np.array(Image.open(reference / f'reference-{name}.png').convert('RGBA'))
        alpha = image[:, :, 3] != 0
        stroke = np.zeros_like(alpha)
        stroke[:, 2:] |= alpha[:, :-2]
        stroke[:, :-2] |= alpha[:, 2:]
        stroke[2:, :] |= alpha[:-2, :]
        stroke[:-2, :] |= alpha[2:, :]
        if key == 'intro':
            image[stroke & ~alpha] = 255
        expected.alpha_composite(Image.fromarray(image), (8, 8))
    binding = 'TAB' if key == 'intro' else key
    hint = np.array(Image.open(reference / f'reference-Press_[_{binding}_]_to_change_characters.png').convert('RGBA'))
    hint[:, :, :3] = 95
    hint[:, :, 3] = np.round(hint[:, :, 3].astype(float) * .6).astype('uint8')
    expected.alpha_composite(Image.fromarray(hint), (-40, 18))
    expected.save(actual / f'{key}-expected.png')
    expected = np.asarray(expected.convert('RGB'), dtype=float)[:64]
    captured = np.asarray(Image.open(actual / f'{key}.png').convert('RGB'), dtype=float)[:64]
    error = float(np.abs(expected - captured).mean())
    control = float(np.abs(expected - blank).mean())
    shifted = float(np.abs(expected - np.roll(captured, 5, axis=0)).mean())
    results[key] = {'meanChannelError': error, 'blankControlError': control, 'shiftedControlError': shifted}
    assert error < .5, results[key]
    assert control > max(error * 10, 10), results[key]
    assert shifted > max(error * 10, 10), results[key]
(actual / 'render-comparison.json').write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
