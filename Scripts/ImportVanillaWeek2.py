import hashlib
import json
from pathlib import Path
import shutil
import struct
import xml.etree.ElementTree as ET

IDENTITY = (1, 0, 0, 1, 0, 0)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def matrix(value):
    if 'MX' in value:
        return value['MX']
    if 'M3D' in value:
        m = value['M3D']
        return [m[0], m[1], m[4], m[5], m[12], m[13]]
    return IDENTITY


def multiply(a, b):
    return (a[0]*b[0]+a[2]*b[1], a[1]*b[0]+a[3]*b[1],
            a[0]*b[2]+a[2]*b[3], a[1]*b[2]+a[3]*b[3],
            a[0]*b[4]+a[2]*b[5]+a[4], a[1]*b[4]+a[3]*b[5]+a[5])


def point(m, x, y):
    return [round(m[0]*x+m[2]*y+m[4], 6), round(m[1]*x+m[3]*y+m[5], 6)]


def length(timeline):
    return max(f['I'] + f.get('DU', 1) for layer in timeline['L'] for f in layer['FR'])


def bounds(frames):
    points = [q['xy'] for frame in frames for q in frame]
    xs = [x for quad in points for x in quad[::2]]
    ys = [y for quad in points for y in quad[1::2]]
    return [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)]


def animate(path, hidden=(), symbol=None, stage_matrix=False):
    data = read(path / 'Animation.json')
    symbols = {s['SN']: s['TL'] for s in data['SD']['S']}
    sprites = {}
    for mapping in sorted(path.glob('spritemap*.json')):
        for entry in read(mapping)['ATLAS']['SPRITES']:
            sprites[entry['SPRITE']['name']] = (entry['SPRITE'], mapping.stem + '.png')
    timeline = symbols[symbol] if symbol and symbol in symbols else data['AN']['TL']
    labels = {f['N']: list(range(f['I'], f['I']+f.get('DU', 1)))
              for layer in timeline['L'] for f in layer['FR'] if 'N' in f}

    def flatten(tl, frame, transform=IDENTITY, tint=(1, 1, 1, 1), addition=(0, 0, 0, 0), depth=0):
        if depth > 64:
            raise ValueError('Recursive Animate symbol')
        result = []
        for layer in reversed(tl['L']):
            if any(layer.get('LN', '').startswith(prefix) for prefix in hidden):
                continue
            key = next((f for f in reversed(layer['FR']) if f['I'] <= frame < f['I']+f.get('DU', 1)), None)
            if key is None:
                continue
            for element in key.get('E', []):
                instance = element.get('SI', element.get('ASI'))
                if instance is None:
                    raise ValueError(f'Unsupported Animate element: {element}')
                color = instance.get('C', {})
                mul = [color.get(c+'M', 1) for c in 'RGBA']
                add = [color.get(c+'O', 0)/255 for c in 'RGBA']
                if color.get('M') == 'T':
                    amount = color.get('TM', 1)
                    rgb = color['TC'].lstrip('#')
                    mul[:3] = [1-amount]*3
                    add[:3] = [int(rgb[i:i+2], 16)/255*amount for i in (0, 2, 4)]
                tint2 = [a*b for a, b in zip(tint, mul)]
                add2 = [a*b+c for a, b, c in zip(tint, add, addition)]
                transform2 = multiply(transform, matrix(instance))
                if 'SI' in element:
                    child = symbols[instance['SN']]
                    mode = instance.get('LP', 'LP')
                    index = instance.get('FF', 0) + (0 if mode == 'SF' else frame-key['I'])
                    index = index % length(child) if mode == 'LP' else min(max(index, 0), length(child)-1)
                    result.extend(flatten(child, index, transform2, tint2, add2, depth+1))
                else:
                    sprite, image = sprites[instance['N']]
                    w, h = sprite['w'], sprite['h']
                    if sprite.get('rotated', False):
                        w, h = h, w
                    result.append({'xy': point(transform2, 0, 0)+point(transform2, w, 0)
                                   +point(transform2, w, h)+point(transform2, 0, h),
                                   'rect': [sprite[k] for k in ['x', 'y', 'w', 'h']],
                                   'image': image, 'rotated': sprite.get('rotated', False), 'tint': tint2, 'add': add2})
        return result

    transform = matrix(data['AN'].get('STI', {}).get('SI', {})) if stage_matrix else IDENTITY
    frames = [flatten(timeline, index, transform) for index in range(length(timeline))]
    if symbol:
        labels[symbol] = list(range(len(frames)))
    return {'frames': frames, 'labels': labels, 'bounds': bounds(frames), 'fps': data['MD'].get('FRT', 24), 'animate': True}


def sparrow(path):
    frames, labels = [], {}
    for entry in sorted(ET.parse(path.with_suffix('.xml')).getroot(), key=lambda e: e.attrib['name']):
        d = entry.attrib
        x, y = -float(d.get('frameX', 0)), -float(d.get('frameY', 0))
        w, h = float(d['width']), float(d['height'])
        if d.get('rotated') == 'true':
            w, h = h, w
        frames.append([{'xy': [x, y, x+w, y, x+w, y+h, x, y+h],
                        'rect': [float(d[k]) for k in ['x', 'y', 'width', 'height']],
                        'image': path.name+'.png', 'rotated': d.get('rotated') == 'true', 'alpha': 1}])
        labels[d['name']] = [len(frames)-1]
    first = ET.parse(path.with_suffix('.xml')).getroot()[0].attrib
    return {'frames': frames, 'labels': labels, 'bounds': [0, 0, float(first.get('frameWidth', first['width'])),
                                                        float(first.get('frameHeight', first['height']))], 'fps': 24}


def image_path(assets, value):
    library, name = value.split(':', 1) if ':' in value else ('shared', value)
    return assets / library / 'images' / name


def save_graphic(target, graphic, animations):
    graphic['animations'] = {}
    for animation in animations:
        prefix = animation['prefix']
        indices = graphic['labels'].get(prefix)
        if indices is None:
            indices = [index for label, entries in graphic['labels'].items() if label.startswith(prefix) for index in entries]
        if not indices:
            raise ValueError(f'Missing animation {prefix} in {target}')
        if animation.get('frameIndices'):
            if graphic.get('animate') and animation.get('animType') == 'symbol':
                indices = [indices[0] + index for index in animation['frameIndices']]
            else:
                indices = [indices[index] for index in animation['frameIndices'] if 0 <= index < len(indices)]
        if not indices:
            raise ValueError(f'Empty animation {animation["name"]} in {target}')
        graphic['animations'][animation['name']] = {'frames': indices, 'fps': animation.get('frameRate', 24),
                                                  'loop': animation.get('looped', False), 'offset': animation.get('offsets', [0, 0])}
    del graphic['labels']
    target.write_text(json.dumps(graphic, separators=(',', ':'))+'\n', encoding='utf-8')


def import_assets(assets, data, output):
    root = output / 'Week2Assets'
    root.mkdir(parents=True, exist_ok=True)
    copied = {}

    def copy(source, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        copied[str(target.relative_to(root)).replace('\\', '/')] = {
            'source': str(source.relative_to(assets)).replace('\\', '/'),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}

    for cid in ['bf', 'bf-dark', 'gf', 'gf-dark', 'spooky', 'spooky-dark', 'monster']:
        character = read(data / f'characters/{cid}.json')
        target = root / 'characters' / cid
        copy(data / f'characters/{cid}.json', target / 'character.json')
        path = image_path(assets, character['assetPath'])
        if path.is_dir():
            for source in path.iterdir():
                if source.suffix in ('.png', '.json'):
                    copy(source, target / source.name)
            graphic = animate(path, ('HAT',) if cid == 'monster' else ())
        else:
            for suffix in ['.png', '.xml']:
                copy(path.with_suffix(suffix), target / (path.name+suffix))
            graphic = sparrow(path)
        save_graphic(target / 'graphic.json', graphic, [a for a in character['animations'] if not a.get('assetPath')])
        print(f'{cid}: {len(graphic["frames"])} frames, bounds {graphic["bounds"]}')

    path = assets / 'shared/images/characters/bf-death'
    target = root / 'characters/bf-death'
    for source in path.iterdir():
        if source.suffix in ('.png', '.json'):
            copy(source, target / source.name)
    animations = read(data / 'characters/bf.json')['animations']
    save_graphic(target / 'graphic.json', animate(path), [a for a in animations if a.get('assetPath') == 'shared:characters/bf-death'])

    for sid in ['spookyMansion', 'spookyMansionErect']:
        target = root / 'stages' / sid
        copy(data / f'stages/{sid}.json', target / 'stage.json')
        stage = read(target / 'stage.json')
        for prop in stage['props']:
            if prop['assetPath'].startswith('#'):
                continue
            path = assets / 'week2/images' / prop['assetPath']
            name = prop['name']
            if path.is_dir():
                for source in path.iterdir():
                    if source.suffix in ('.png', '.json'):
                        copy(source, target / name / source.name)
                save_graphic(target / name / 'graphic.json', animate(path), prop['animations'])
            elif path.with_suffix('.xml').is_file():
                for suffix in ['.png', '.xml']:
                    copy(path.with_suffix(suffix), target / name / (path.name+suffix))
                save_graphic(target / name / 'graphic.json', sparrow(path), prop['animations'])
            else:
                copy(path.with_suffix('.png'), target / name / (path.name+'.png'))
                w, h = struct.unpack('>II', path.with_suffix('.png').read_bytes()[16:24])
                graphic = {'frames': [[{'xy': [0, 0, w, 0, w, h, 0, h], 'rect': [0, 0, w, h],
                                       'image': path.name+'.png', 'rotated': False, 'alpha': 1}]],
                           'labels': {'idle': [0]}, 'bounds': [0, 0, w, h], 'fps': 24}
                save_graphic(target / name / 'graphic.json', graphic, [{'name': 'idle', 'prefix': 'idle'}])
        copy(assets / f'scripts/stages/{sid}.hxc', root / 'Source' / f'{sid}.hxc')
    for number in [1, 2]:
        copy(assets / f'week2/sounds/thunder_{number}.ogg', root / f'thunder_{number}.ogg')
    copy(assets / 'shaders/rain.frag', root / 'Source/rain.frag')
    for cid in ['bf-dark', 'gf-dark', 'spooky-dark', 'monster']:
        copy(assets / f'scripts/characters/{cid}.hxc', root / 'Source' / f'{cid}.hxc')
    (root / 'source-manifest.json').write_text(json.dumps(copied, indent=2)+'\n', encoding='utf-8')
