from pathlib import Path
import argparse
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont


project = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--port', type=Path, default=project / 'Builds/FreeplayScreenVerification')
parser.add_argument('--reference', type=Path, default=project / 'Builds/FreeplayOriginalProbe/captures')
parser.add_argument('--baseline', type=Path)
args = parser.parse_args()
cases = ['bf-bopeebo-hard', 'pico-bopeebo-hard']
cases += ['bf-confirm-' + str(time) for time in [100, 350, 600, 900]]
cases += ['pico-confirm-' + str(time) for time in [100, 350, 600, 900, 1200]]


def metadata(folder, name):
    return json.loads((folder / (name + '.json')).read_text())


def pixels(folder, name):
    image = Image.open(folder / (name + '.png')).convert('RGB')
    if image.size != (1280, 720):
        raise ValueError('Capture resolution differs: ' + str(folder / name))
    return np.asarray(image).astype(float)


def song_row(data, song):
    return next(row for row in data['rows'] if (row['song'] or '').lower().startswith(song.lower()))


def evaluate(folder, require_alpha=True):
    metrics = {}
    position_error = 0
    minimum_alpha = 1
    pico_error = 0
    bf_error = 0
    confirmation_brightness_error = 0
    confirmation_pixel_error = 0
    idle_pixel_error = 0
    for name in cases:
        reference = metadata(args.reference, name)
        port = metadata(folder, name)
        if reference['character'] != port['character'] or reference['difficulty'].lower() != port['difficulty'].lower():
            raise ValueError('Capture selection differs: ' + name)
        for row in reference['rows']:
            if row['song'] is None or not 0 <= row['y'] < 700:
                continue
            other = song_row(port, row['song'])
            if '-confirm-' in name:
                position_error = max(position_error, abs(row['x'] - other['x']))
                minimum_alpha = min(minimum_alpha, other['alpha'] if require_alpha else other.get('alpha', 1))
            elif name.startswith('pico'):
                pico_error = max(pico_error, abs(row['y'] - other['y']))
            else:
                bf_error = max(bf_error, abs(row['y'] - other['y']))
        a = pixels(args.reference, name)[380:470, 862:914]
        b = pixels(folder, name)[380:470, 862:914]
        metrics[name] = {
            'reference_mean_rgb': float(a.mean()), 'port_mean_rgb': float(b.mean()),
            'mean_rgb_error': float(np.abs(a - b).mean()),
        }
        if '-confirm-' in name:
            idle = metrics[name.split('-')[0] + '-bopeebo-hard']
            reference_ratio = float(a.mean()) / idle['reference_mean_rgb']
            port_ratio = float(b.mean()) / idle['port_mean_rgb']
            metrics[name]['reference_ratio_to_idle'] = reference_ratio
            metrics[name]['port_ratio_to_idle'] = port_ratio
            confirmation_brightness_error = max(confirmation_brightness_error, abs(reference_ratio - port_ratio))
            confirmation_pixel_error = max(confirmation_pixel_error, metrics[name]['mean_rgb_error'])
        else:
            idle_pixel_error = max(idle_pixel_error, metrics[name]['mean_rgb_error'])

    original = pixels(args.reference, 'pico-confirm-1200')
    current = pixels(folder, 'pico-confirm-1200')
    template = original[160:232, 240:347].min(2) > 230
    white = current.min(2) > 230
    if template.sum() < 100:
        raise ValueError('Reference bullet outlines are missing.')
    best = (1, 0, 0)
    for dy in range(-40, 41):
        for dx in range(-180, 181):
            candidate = white[160+dy:232+dy, 240+dx:347+dx]
            mismatch = float(np.mean(template != candidate))
            if mismatch < best[0]:
                best = (mismatch, dx, dy)
    reference_frame = metadata(args.reference, 'pico-confirm-1200')['confirmFrame']
    port_frame = metadata(folder, 'pico-confirm-1200')['confirmFrame']
    checks = {
        'other_capsules_stay_visible': {'passed': position_error < 1 and minimum_alpha > .99,
            'maximum_x_error': position_error, 'minimum_detail_alpha': minimum_alpha},
        'confirmation_backdrop_brightness': {'passed': confirmation_brightness_error < .04 and confirmation_pixel_error < 3,
            'maximum_ratio_error': confirmation_brightness_error, 'maximum_mean_rgb_error': confirmation_pixel_error},
        'pico_row_spacing': {'passed': pico_error < 1, 'maximum_y_error': pico_error},
        'pico_confirmation_origin': {'passed': best[1:] == (0, 0) and best[0] < .005 and reference_frame == port_frame == 28,
            'reference_frame': reference_frame, 'port_frame': port_frame,
            'outline_translation': list(best[1:]), 'outline_mismatch': best[0]},
    }
    controls = {'bf_row_spacing_matches': bf_error < 1, 'idle_backdrop_error_under_3': idle_pixel_error < 3}
    return checks, controls, metrics


checks, controls, metrics = evaluate(args.port)
baseline_checks = None
if args.baseline:
    baseline_checks, _, _ = evaluate(args.baseline, require_alpha=False)
    controls['baseline_rejects_all_four_defects'] = all(not case['passed'] for case in baseline_checks.values())
passed = all(case['passed'] for case in checks.values()) and all(controls.values())
result = {'passed': passed, 'captures': len(cases), 'checks': checks, 'controls': controls, 'baseline_checks': baseline_checks}
(args.port / 'comparison-metrics.json').write_text(json.dumps(metrics, indent=2))
(args.port / 'verification-checks.json').write_text(json.dumps(result, indent=2))

font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22)
for name in ['bf-bopeebo-hard', 'pico-bopeebo-hard', 'bf-confirm-350', 'pico-confirm-1200']:
    sheet = Image.new('RGB', (1920, 584), '#171820')
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 8), 'Funkin 0.8.6 executable', font=font, fill='white')
    draw.text((976, 8), 'Current Unity working tree', font=font, fill='white')
    for offset, folder in [(0, args.reference), (960, args.port)]:
        shot = Image.open(folder / (name + '.png')).convert('RGB').resize((960, 540), Image.Resampling.LANCZOS)
        sheet.paste(shot, (offset, 44))
    sheet.save(args.port / (name + '-comparison.png'))
print(json.dumps(result, indent=2))
raise SystemExit(0 if passed else 1)
