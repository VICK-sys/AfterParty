import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def pixels(path):
    return np.asarray(Image.open(path).convert('RGB'), dtype=np.float32)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--captures', type=Path, required=True)
    parser.add_argument('--reference', type=Path, default=Path('Builds/PhillyBackgroundReference'))
    parser.add_argument('--stage', choices=['phillyStreets', 'phillyStreetsErect'], required=True)
    args = parser.parse_args()
    results = []
    for index in range(3):
        prefix = f'{args.stage}-{index}'
        expected = pixels(args.reference / f'{prefix}-sky.png')
        actual = pixels(args.captures / f'{prefix}-sky.png')
        control = pixels(args.captures / f'{prefix}-scaled-control.png')
        error = np.abs(actual - expected)
        control_error = float(np.abs(control - expected).mean())
        coverage_error = int(np.count_nonzero((expected.max(2) > 10) != (actual.max(2) > 10)))
        assert float(error.mean()) < 1, (prefix, 'sky pixels', float(error.mean()))
        assert coverage_error <= 1280, (prefix, 'sky coverage', coverage_error)
        assert control_error > 20, (prefix, 'scaled sky control', control_error)
        background_error = np.abs(pixels(args.captures / f'{prefix}.png') - pixels(args.reference / f'{prefix}.png'))
        results.append({'capture': prefix, 'skyMeanError': float(error.mean()), 'skyCoverageError': coverage_error,
                        'scaledControlMeanError': control_error, 'backgroundMeanError': float(background_error.mean())})
    result = {'stage': args.stage, 'passed': True, 'comparisons': results}
    (args.captures / f'{args.stage}-comparison.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
