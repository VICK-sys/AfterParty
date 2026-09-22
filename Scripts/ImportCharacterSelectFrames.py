import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def read_capture(capture, prefix, index):
    black = np.asarray(Image.open(capture / f'{prefix}-{index}-black.png').convert('RGB'), dtype=float) / 255
    white = np.asarray(Image.open(capture / f'{prefix}-{index}-white.png').convert('RGB'), dtype=float) / 255
    alpha = np.clip(1 - np.median(white - black, axis=2), 0, 1)[..., None]
    rgb = np.clip(black / np.maximum(alpha, 1 / 255), 0, 1)
    return rgb, alpha


def render_frame(capture, index, base_prefix='base'):
    ranges = {'bfChill': (28, 46), 'gfChill': (54, 106), 'picoChill': (28, 38), 'neneChill': (47, 52)}
    start, end = ranges[capture.name]
    if start <= index < end:
        base, alpha = read_capture(capture, base_prefix, index)
        shade, shade_alpha = read_capture(capture, 'overlay', index)
        front, front_alpha = read_capture(capture, 'front', index)
        back_alpha = np.maximum(alpha - front_alpha, 0)
        back = np.clip((base * alpha - front * front_alpha) / np.maximum(back_alpha, 1 / 255), 0, 1)
        overlay = np.where(back < .5, 2 * back * shade, 1 - 2 * (1 - back) * (1 - shade))
        rgb = base + (overlay - back) * shade_alpha * back_alpha / np.maximum(alpha, 1 / 255)
    else:
        rgb, alpha = read_capture(capture, 'raw', index)
    return Image.fromarray((np.concatenate([rgb, alpha], axis=2).clip(0, 1) * 255).round().astype('uint8'))


project = Path(__file__).resolve().parents[1]
root = project / 'Assets/Resources/VanillaFreeplay/charSelect'
reference = project / 'Builds/CharacterSelectReference'
manifest_path = root / 'rendered-frames.json'
manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
for name in (sys.argv[1:] or ['lockedChill', 'bfChill', 'gfChill', 'picoChill', 'lock', 'neneChill']):
    folder = root / name
    capture = reference / name
    if name in ['bfChill', 'gfChill', 'picoChill', 'neneChill']:
        entries = []
        for index in range(56 if name == 'bfChill' else 106 if name == 'gfChill' else 52 if name == 'neneChill' else 79):
            image = render_frame(capture, index)
            if (capture / f'base-{index}-black.png').exists() or name == 'gfChill':
                backdrop = Image.new('RGBA', image.size, (128, 128, 128, 255))
                backdrop.alpha_composite(image)
                if name == 'neneChill':
                    scene = render_frame(capture, index, 'scene')
                    backdrop = Image.new('RGBA', scene.size, (128, 128, 128, 255))
                    backdrop.alpha_composite(scene)
                backdrop.convert('RGB').crop((0, 0, 1280, 720)).save(reference / f'{name}-{index}.png')
            box = image.getbbox() or (0, 0, 1, 1)
            image.crop(box).save(capture / f'frame-{index}.png')
            stage = [640, 360] if name == 'bfChill' else [639.95, 360] if name == 'gfChill' else [713.95, 411] if name == 'neneChill' else [758.7, 401.95]
            entries.append({'file': f'frame-{index}.png', 'x': box[0] - stage[0], 'y': box[1] - stage[1]})
        data = {'frames': entries, 'bounds': [0, 0, 2048, 1024]}
    else:
        data = json.loads((capture / 'frames.json').read_text())
    animation = json.loads((folder / 'Animation.json').read_text(encoding='utf-8-sig'))
    atlas = json.loads((folder / 'spritemap1.json').read_text(encoding='utf-8-sig'))
    base = Image.open(folder / 'spritemap1.png').convert('RGBA')
    target = next(layer for layer in animation['AN']['TL']['L'] if layer['LN'] == 'Nene') if name == 'neneChill' else animation['AN']
    previous = target.get('RB', {})
    if 'originalSize' in previous:
        base = base.crop((0, 0, *previous['originalSize']))
    atlas['ATLAS']['SPRITES'] = [entry for entry in atlas['ATLAS']['SPRITES'] if not entry['SPRITE']['name'].startswith('cs-render-')]
    x, y, row, width = 2, base.height + 2, 0, max(8192, base.width)
    unique, packed, frames, offsets = {}, [], [], []
    for entry in data['frames']:
        image = Image.open(capture / entry['file']).convert('RGBA')
        key = (image.size, hashlib.sha256(image.tobytes()).hexdigest())
        if key not in unique:
            if x + image.width + 2 > width:
                x, y, row = 2, y + row + 2, 0
            identifier = f'cs-render-{len(unique)}'
            unique[key] = identifier
            packed.append((image, x, y))
            atlas['ATLAS']['SPRITES'].append({'SPRITE': {'name': identifier, 'x': x, 'y': y, 'w': image.width, 'h': image.height, 'rotated': False}})
            x += image.width + 2
            row = max(row, image.height)
        frames.append(unique[key])
        offsets.append([entry['x'], entry['y']])
    height = y + row + 2
    if height > 8192:
        raise ValueError(f'{name}: atlas exceeds 8192 pixels: {height}')
    output = Image.new('RGBA', (width, height))
    output.paste(base, (0, 0))
    for image, x, y in packed:
        output.paste(image, (x, y))
    output.save(folder / 'spritemap1.tmp.png')
    (folder / 'spritemap1.tmp.png').replace(folder / 'spritemap1.png')
    target['RB'] = {'bounds': data['bounds'], 'frames': frames, 'offsets': offsets, 'originalSize': list(base.size)}
    (folder / 'Animation.json').write_text(json.dumps(animation), encoding='utf-8')
    (folder / 'spritemap1.json').write_text(json.dumps(atlas), encoding='utf-8')
    manifest[name] = {'frames': len(frames), 'uniqueFrames': len(unique), 'size': [width, height],
                      'sha256': {file: hashlib.sha256((folder / file).read_bytes()).hexdigest()
                                 for file in ['Animation.json', 'spritemap1.json', 'spritemap1.png']}}
    if name in ['bfChill', 'gfChill', 'picoChill', 'neneChill']:
        manifest[name]['overlayBlend'] = 'sRGB Overlay'
    print(f'{name}: {len(frames)} frames, {len(unique)} unique, {width}x{height}')
(root / 'rendered-frames.json').write_text(json.dumps(manifest, indent=2) + '\n')

for path in reference.glob('*.png'):
    with Image.open(path) as image:
        image.crop((0, 0, 1280, 720)).save(path)
