import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def pixels(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(float)


def run(root):
    errors, mouths, overlaps = [], [], []
    samples = json.loads((root / 'metrics.json').read_text())
    for sample in samples:
        frame = '' if sample.get('poseFrame') is None else '-frame' + str(sample['poseFrame'])
        path = root / 'Unity' / f"{sample['name']}-{sample['animation']}{frame}-{sample['mouth']}.png"
        reference = pixels(root / path.name)
        actual = pixels(path)
        error = np.abs(actual - reference).mean()
        assert error < .15, (path.name, error)
        assert np.abs(np.roll(actual, 8, axis=1) - reference).mean() > .5, path
        errors.append(error)
        if path.name.endswith('-650.png'):
            silent = pixels(root / path.name.replace('-650.png', '-0.png'))
            mouth = np.abs(silent - reference).max(axis=2) > 8
            assert mouth.sum() > 100, path
            error = np.abs(actual - reference)[mouth].mean()
            assert error < 10, (path.name, error)
            assert np.abs(silent - reference)[mouth].mean() > 70, path
            mouths.append(error)
        if sample['name'] == 'yunjin':
            wrong = pixels(root / 'Unity/MouthOnTop' / path.name)
            overlap = np.abs(wrong - actual).max(axis=2) > 8
            if overlap.sum() > 10:
                correct_error = np.abs(actual - reference)[overlap].mean()
                wrong_error = np.abs(wrong - reference)[overlap].mean()
                assert correct_error < wrong_error, (path.name, correct_error, wrong_error)
                overlaps.append(wrong_error - correct_error)
    assert len(errors) == 90 and len(mouths) == 35
    assert overlaps and max(overlaps) > 10, overlaps
    print(f'SPAGHETTI RENDERER PASSED: {len(errors)} Flixel poses, mean error {np.mean(errors):.4f}/255, '
          f'mouth error {np.mean(mouths):.4f}/255, {len(overlaps)} foreground overlaps checked, '
          'mouth-on-top, shifted-character, and frozen-mouth controls rejected.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, default=Path(__file__).resolve().parents[1] / 'Builds/SpaghettiReference')
    run(parser.parse_args().reference)
