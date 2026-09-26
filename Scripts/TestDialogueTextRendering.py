import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
args = parser.parse_args()
reference = args.directory / 'reference'
actual = args.directory / 'unity'
cases = json.loads((reference / 'cases.json').read_text())
metadata = json.loads((actual / 'cases.json').read_text())
results = []
passed = all(item['wrapMatches'] for item in metadata)
for height in [720, 1080]:
    blank = np.asarray(Image.open(actual / f'blank-{height}p.png').convert('RGB'), dtype=np.int16)
    for name in ['label'] + [case['id'] for case in cases]:
        filename = f'{name}-{height}p.png'
        expected = np.asarray(Image.open(reference / filename).convert('RGB'), dtype=np.int16)
        captured = np.asarray(Image.open(actual / filename).convert('RGB'), dtype=np.int16)
        foreground = np.any(expected != blank, axis=2) | np.any(captured != blank, axis=2)
        error = np.abs(expected - captured)
        mean = float(error[foreground].mean()) if foreground.any() else 255
        maximum = int(error.max())
        blank_error = float(np.abs(expected - blank)[foreground].mean()) if foreground.any() else 0
        shifted_error = float(np.abs(expected - np.roll(captured, 2, axis=1))[foreground].mean()) if foreground.any() else 0
        valid = bool(foreground.any() and maximum <= 3 and mean <= .5 and blank_error > 5 and shifted_error > 5)
        passed &= valid
        results.append(dict(name=name, height=height, meanChannelError=mean, maxChannelError=maximum,
                            blankControlError=blank_error, shiftedControlError=shifted_error, passed=valid))
    scale = height / 720
    crops = [('label', (0, 690, 220, 720)), ('roses-pico-censored-0', (195, 460, 1015, 575)),
             ('thorns-2', (195, 460, 1115, 575))]
    rows = []
    for name, bounds in crops:
        bounds = tuple(round(value * scale) for value in bounds)
        for title, directory, suffix in [('0.8.6', reference, ''), ('Before', actual, '-before'), ('Corrected', actual, '')]:
            crop = Image.open(directory / f'{name}{suffix}-{height}p.png').convert('RGB').crop(bounds)
            row = Image.new('RGB', (round(920 * scale), crop.height + 24), '#121824')
            ImageDraw.Draw(row).text((4, 4), title, fill='white')
            row.paste(crop, (0, 24))
            rows.append(row)
    comparison = Image.new('RGB', (max(row.width for row in rows), sum(row.height + 8 for row in rows)), '#121824')
    top = 0
    for row in rows:
        comparison.paste(row, (0, top))
        top += row.height + 8
    comparison.save(args.directory / f'comparison-{height}p.png')
report = dict(passed=bool(passed), cases=len(results), wrappingCases=len(metadata),
              wrapFailures=[item['id'] for item in metadata if not item['wrapMatches']], results=results)
(args.directory / 'comparison.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({key: value for key, value in report.items() if key != 'results'}, indent=2))
print(json.dumps(sorted(results, key=lambda item: item['meanChannelError'], reverse=True)[:5], indent=2))
raise SystemExit(0 if passed else 1)
