import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


project = Path(__file__).resolve().parents[1]
reference = project / 'Builds/CharacterSelectReference'
actual = Path(sys.argv[1]) if len(sys.argv) > 1 else project / 'Builds/CharacterSelectParity'
metrics = {}
for name in ['lockedChill', 'bfChill', 'gfChill', 'picoChill', 'neneChill']:
    frames = [0, 10, 27, 29, 34, 109] if name == 'lockedChill' else [0, 15, 30, 54, 55, 56, 60, 80, 105] if name == 'gfChill' else [0, 7, 15, 29, 30, 38, 46, 47, 51] if name == 'neneChill' else [0, 15, 17, 22, 28, 30, 35]
    for frame in frames:
        filename = f'{name}-{frame}.png'
        expected = np.asarray(Image.open(reference / filename).convert('RGB')).astype(float)
        rendered = np.asarray(Image.open(actual / filename).convert('RGB')).astype(float)
        error = np.abs(expected - rendered)
        flat = np.any(expected != 128, axis=2)
        for y in range(-3, 4):
            for x in range(-3, 4):
                flat &= np.all(expected == np.roll(expected, (y, x), axis=(0, 1)), axis=2)
        mean = float(error.mean())
        flat_error = float(error[flat].mean()) if flat.any() else 0
        metrics[filename] = {'meanChannelError': mean, 'flatColorError': flat_error}
        if name == 'neneChill':
            assert error.max() <= 5, f'{filename}: local filter mismatch {error.max()}'
        assert mean < .1, f'{filename}: render mismatch {mean}'
        assert flat_error < .01, f'{filename}: mask or blend mismatch {flat_error}'
        assert np.abs(expected - 128).mean() > .1, f'{filename}: blank control passed'
        assert np.abs(expected - np.roll(expected, 8, axis=1)).mean() > .1, f'{filename}: shifted control passed'
        print(f'{filename}: mean={mean:.5f}, flat={flat_error:.5f}')
nene = np.asarray(Image.open(actual / 'neneChill-38.png').convert('RGB')).astype(float)
for control in ['visualizer', 'unfiltered']:
    pixels = np.asarray(Image.open(actual / f'neneChill-{control}-control.png').convert('RGB')).astype(float)
    error = np.abs(nene - pixels)
    difference = float(error.mean())
    assert error.max() > 5, f'Nene {control} control passed the local filter comparison'
    metrics[f'nene-{control}-control'] = {'meanDifference': difference, 'maximumDifference': float(error.max())}
for name, frame in [('bfChill', 30), ('gfChill', 56), ('picoChill', 30), ('neneChill', 47)]:
    corrected = np.asarray(Image.open(actual / f'{name}-{frame}.png').convert('RGB'), dtype=float)
    control = np.asarray(Image.open(actual / f'{name}-overlay-control.png').convert('RGB'), dtype=float)
    difference = np.abs(corrected - control)
    black = np.asarray(Image.open(reference / name / f'overlay-{frame}-black.png').convert('RGB'), dtype=float)[:720, :1280]
    white = np.asarray(Image.open(reference / name / f'overlay-{frame}-white.png').convert('RGB'), dtype=float)[:720, :1280]
    face = np.median(white - black, axis=2) < 128
    changed = int(np.count_nonzero(face & (difference.max(axis=2) > 40)))
    assert changed > 100, f'{name}: opaque face overlay control passed'
    metrics[f'{name}-overlay-control'] = {'changedFacePixels': changed}
    if name in ['picoChill', 'neneChill']:
        front_black = np.asarray(Image.open(reference / name / f'front-{frame}-black.png').convert('RGB'), dtype=float)[:720, :1280]
        front_white = np.asarray(Image.open(reference / name / f'front-{frame}-white.png').convert('RGB'), dtype=float)[:720, :1280]
        base_black = np.asarray(Image.open(reference / name / f'base-{frame}-black.png').convert('RGB'), dtype=float)[:720, :1280]
        base_white = np.asarray(Image.open(reference / name / f'base-{frame}-white.png').convert('RGB'), dtype=float)[:720, :1280]
        foreground = face & (np.median(front_white - front_black, axis=2) < 1) & (np.median(base_white - base_black, axis=2) < 1)
        assert foreground.sum() > 100, f'{name}: missing foreground control area'
        assert np.abs(corrected - base_black)[foreground].max() <= 3, f'{name}: shadow tinted foreground details'
        shade = black / np.maximum(255 - np.median(white - black, axis=2)[..., None], 1)
        base = base_black / 255
        wrong_order = 255 * np.where(base < .5, 2 * base * shade, 1 - 2 * (1 - base) * (1 - shade))
        wrong_order = base_black + (wrong_order - base_black) * (1 - np.median(white - black, axis=2)[..., None] / 255)
        front_edges = face & (np.median(front_white - front_black, axis=2) < 230)
        assert np.count_nonzero(front_edges & (np.abs(wrong_order - corrected).max(axis=2) > 10)) > 20, f'{name}: foreground ordering control was ineffective'

(actual / 'reference-comparison.json').write_text(json.dumps(metrics, indent=2) + '\n')
print('Character select rendering passed: 38 frames, mask and blend checks, blank and shifted controls.')
