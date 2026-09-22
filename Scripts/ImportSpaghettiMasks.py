import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


def run(reference, root, only=None):
    for source, destination in [('characters-sserafim-gf', 'characters/sserafim-gf'),
                                ('characters-sserafim-yunjin-base', 'characters/sserafim-yunjin'),
                                ('characters-sserafim-yunjin-foreground', 'characters/sserafim-yunjin/foreground'),
                                ('gfGetUp', 'effects/cutscene/gfGetUp')]:
        if only is not None and destination != only:
            continue
        folder = root / destination
        folder.mkdir(parents=True, exist_ok=True)
        capture = reference / source
        entries = json.loads((capture / 'masks.json').read_text())
        original = folder.parent if folder.name == 'foreground' else folder
        data = json.loads((original / 'graphic.json').read_text())
        attachments = data.get('attachments')
        foreground_frames = {index for animation in data['animations'].values()
                             if attachments and any(attachments[frame] for frame in animation['frames'])
                             for index in animation['frames']}
        if folder.name == 'foreground':
            data.pop('attachments', None)
        unique, frames, packed = {}, [], []
        x, y, row, width = 2, 2, 0, 4096
        for index, file in enumerate(entries['frames']):
            if folder.name == 'foreground' and index not in foreground_frames:
                frames.append([])
                continue
            image = Image.open(capture / file).convert('RGBA')
            box = image.getbbox()
            if box is None:
                frames.append([])
                continue
            image = image.crop(box)
            key = (image.size, hashlib.sha256(image.tobytes()).hexdigest())
            if key not in unique:
                if x + image.width + 2 > width:
                    x, y, row = 2, y + row + 2, 0
                unique[key] = [x, y, image.width, image.height]
                packed.append((image, x, y))
                x += image.width + 2
                row = max(row, image.height)
            left, top = entries['bounds'][0] + box[0], entries['bounds'][1] + box[1]
            right, bottom = left + image.width, top + image.height
            frames.append([{'image': 'masked.png', 'rect': unique[key],
                            'xy': [left, top, right, top, right, bottom, left, bottom],
                            'rotated': False, 'tint': [1, 1, 1, 1], 'add': [0, 0, 0, 0]}])
        output = Image.new('RGBA', (width, y + row + 2))
        if output.height > 16384:
            raise ValueError('Masked atlas exceeds the texture limit.')
        for image, x, y in packed:
            output.paste(image, (x, y))
        output.save(folder / 'masked.png')
        data['frames'] = frames
        data['bounds'] = entries['bounds']
        data['hitboxSize'] = [int(value) for value in entries['bounds'][2:]]
        (folder / 'graphic.json').write_text(json.dumps(data, separators=(',', ':')) + '\n')
        (folder / 'masked-frames.json').write_text(json.dumps({
            'renderer': 'HaxeFlixel 6.2.0, flixel-animate 1.4.0',
            'frames': len(frames), 'uniqueFrames': len(unique),
            'sourceSha256': hashlib.sha256((original / 'Animation.json').read_bytes()).hexdigest(),
            'atlasSha256': hashlib.sha256((folder / 'masked.png').read_bytes()).hexdigest()
        }, indent=2) + '\n')
        print(f'{destination}: {len(frames)} masked frames, {len(unique)} unique, {output.width}x{output.height}')


if __name__ == '__main__':
    project = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, default=project / 'Builds/SpaghettiReference')
    parser.add_argument('--output', type=Path, default=project / 'Assets/StreamingAssets/Bundles/SpaghettiAssets')
    parser.add_argument('--only', choices=['characters/sserafim-gf', 'characters/sserafim-yunjin',
                                         'characters/sserafim-yunjin/foreground', 'effects/cutscene/gfGetUp'])
    args = parser.parse_args()
    run(args.reference, args.output, args.only)
