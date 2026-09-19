import json
from pathlib import Path

import numpy as np
from PIL import Image


root = Path(__file__).resolve().parents[1]
reference = root / 'Builds/OptionsReference'
actual = root / 'Builds/OptionsValidation'
results = {}
blank = np.asarray(Image.open(actual / 'blank-control.png').convert('RGB'), dtype=float)
for source, target in [('root', 'root'), ('preferences', 'preferences-top'), ('controls', 'controls-keyboard')]:
    expected = np.asarray(Image.open(reference / (source + '-reference.png')).convert('RGB'), dtype=float)
    captured = np.asarray(Image.open(actual / (target + '.png')).convert('RGB'), dtype=float)
    error = float(np.abs(expected - captured).mean())
    control_error = float(np.abs(expected - blank).mean())
    results[source] = {'meanChannelError': error, 'blankControlError': control_error}
    assert error < 2, f'{source}: source comparison failed: {error}'
    assert control_error > error * 20, f'{source}: blank control did not fail the comparison'
(actual / 'render-comparison.json').write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
