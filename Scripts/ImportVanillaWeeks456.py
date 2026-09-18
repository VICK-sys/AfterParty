import hashlib
import json
import re
import shutil
import struct
import xml.etree.ElementTree as ET
from pathlib import Path

from ImportVanillaWeek2 import animate, read, save_graphic, sparrow

WEEKS = {
    4: (['limoRide', 'limoRideErect'], ['bf-car', 'gf-car', 'mom-car']),
    5: (['mallXmas', 'mallXmasErect', 'mallEvil'],
        ['bf-christmas', 'gf-christmas', 'parents-christmas', 'monster-christmas']),
    6: (['school', 'schoolErect', 'schoolEvil', 'schoolEvilErect'],
        ['bf-pixel', 'gf-pixel', 'senpai', 'senpai-angry', 'spirit'])
}


def natural(value):
    return [int(part) if part.isdigit() else part for part in re.split(r'(\d+)', value)]


def packer(path):
    entries = []
    for line in path.with_suffix('.txt').read_text().splitlines():
        name, values = line.rsplit(' = ', 1)
        entries.append((name, [int(v) for v in values.split()]))
    frames, labels = [], {}
    for name, (x, y, w, h) in sorted(entries, key=lambda entry: natural(entry[0])):
        labels[name] = [len(frames)]
        frames.append([{'xy': [0, 0, w, 0, w, h, 0, h], 'rect': [x, y, w, h],
                        'image': path.name + '.png', 'rotated': False, 'alpha': 1}])
    return {'frames': frames, 'labels': labels, 'bounds': [0, 0, entries[0][1][2], entries[0][1][3]], 'fps': 24}


def static_graphic(path):
    w, h = struct.unpack('>II', path.read_bytes()[16:24])
    return {'frames': [[{'xy': [0, 0, w, 0, w, h, 0, h], 'rect': [0, 0, w, h],
                        'image': path.name, 'rotated': False, 'alpha': 1}]],
            'labels': {'idle': [0]}, 'bounds': [0, 0, w, h], 'fps': 24}


def import_assets(assets, data, output, weeks=None):
    for week, (stages, characters) in (WEEKS if weeks is None else weeks).items():
        root = output / f'Week{week}Assets'
        root.mkdir(parents=True, exist_ok=True)
        copied = {}

        def copy(source, target):
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            copied[target.relative_to(root).as_posix()] = {
                'source': source.relative_to(assets).as_posix(),
                'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}

        def resolve(value, library):
            if ':' in value:
                library, value = value.split(':', 1)
            path = assets / library / 'images' / value
            if path.exists() or path.with_suffix('.png').exists():
                return path
            return assets / 'shared/images' / value

        def graphic(path, target, animations, pixel=False, symbol=None, stage_matrix=False, initial_animation=None):
            target.mkdir(parents=True, exist_ok=True)
            if path.is_dir():
                for source in path.iterdir():
                    if source.suffix in ('.png', '.json'):
                        copy(source, target / source.name)
                result = animate(path, symbol=symbol, stage_matrix=stage_matrix)
                if stage_matrix:
                    x, y, w, h = result['bounds']
                    result['bounds'] = [0, 0, x + w, y + h]
            elif path.with_suffix('.xml').is_file():
                for suffix in ['.png', '.xml']:
                    copy(path.with_suffix(suffix), target / (path.name + suffix))
                result = sparrow(path)
            elif path.with_suffix('.txt').is_file():
                for suffix in ['.png', '.txt']:
                    copy(path.with_suffix(suffix), target / (path.name + suffix))
                result = packer(path)
                animations = [dict(a, prefix=a.get('prefix', '')) for a in animations]
            else:
                copy(path.with_suffix('.png'), target / (path.name + '.png'))
                result = static_graphic(path.with_suffix('.png'))
            if not animations:
                animations = [{'name': 'idle', 'prefix': next(iter(result['labels']))}]
            if not result.get('animate'):
                animations = [dict(a) for a in animations]
                for animation in animations:
                    if animation.get('frameIndices'):
                        count = sum(len(v) for k, v in result['labels'].items() if k.startswith(animation['prefix']))
                        animation['frameIndices'] = [i for i in animation['frameIndices'] if i < count]
            result['pixel'] = pixel
            result['scaledOffsets'] = True
            save_graphic(target / 'graphic.json', result, animations)
            if initial_animation and path.with_suffix('.xml').is_file():
                initial = next(a for a in animations if a['name'] == initial_animation)
                entries = sorted((entry for entry in ET.parse(path.with_suffix('.xml')).getroot()
                                  if entry.get('name').startswith(initial['prefix'])), key=lambda entry: natural(entry.get('name')))
                entry = entries[(initial.get('frameIndices') or [0])[0]]
                result['bounds'][2:] = [int(entry.get('frameWidth', entry.get('width'))),
                                        int(entry.get('frameHeight', entry.get('height')))]
            for animation in result['animations'].values():
                animation['frames'] = [i for i in animation['frames'] if i < len(result['frames'])]
                if not animation['frames']:
                    raise ValueError(f'Empty animation in {target}')
            (target / 'graphic.json').write_text(json.dumps(result, separators=(',', ':')) + '\n', encoding='utf-8')

        for cid in characters:
            character = read(data / f'characters/{cid}.json')
            target = root / 'characters' / cid
            copy(data / f'characters/{cid}.json', target / 'character.json')
            graphic(resolve(character['assetPath'], 'shared'), target,
                    [a for a in character['animations'] if not a.get('assetPath')], character.get('isPixel', False),
                    initial_animation=character.get('startingAnimation', 'idle'))
            alternate = {}
            for animation in character['animations']:
                if animation.get('assetPath') and animation['name'] != 'fakeoutDeath':
                    alternate.setdefault(animation['assetPath'], []).append(animation)
            for asset, animations in alternate.items():
                name = 'death' if animations[0]['name'] == 'firstDeath' else 'censor'
                graphic(resolve(asset, 'shared'), target / name, animations, character.get('isPixel', False))
            source = assets / f'scripts/characters/{cid}.hxc'
            if source.is_file():
                copy(source, root / 'Source/characters' / source.name)
            print(f'Week {week}: {cid}')

        for sid in stages:
            target = root / 'stages' / sid
            copy(data / f'stages/{sid}.json', target / 'stage.json')
            for prop in read(target / 'stage.json')['props']:
                if prop['assetPath'].startswith('#'):
                    continue
                animations = prop.get('animations', [])
                symbol = next((a['prefix'] for a in animations if a.get('animType') == 'symbol'), None)
                graphic(resolve(prop['assetPath'], f'week{week}'), target / prop['name'], animations,
                        prop.get('isPixel', False), symbol, prop.get('atlasSettings', {}).get('applyStageMatrix', False))
            source = assets / f'scripts/stages/{sid}.hxc'
            if source.is_file():
                copy(source, root / 'Source/stages' / source.name)
            print(f'Week {week}: {sid}')

        if week == 4:
            for name in ['mistMid', 'mistBack']:
                graphic(assets / f'week4/images/limo/erect/{name}', root / 'effects' / name, [])
            for index in range(2):
                copy(assets / f'week4/sounds/carPass{index}.ogg', root / f'audio/carPass{index}.ogg')
        if week == 6:
            for name in ['bfPixel_mask', 'gfPixel_mask', 'senpai_mask']:
                copy(assets / f'week6/images/weeb/erect/masks/{name}.png', root / 'effects' / (name + '.png'))
            copy(data / 'notestyles/pixel.json', root / 'pixel.json')
            for sid in ['senpai', 'roses', 'thorns']:
                copy(data / f'dialogue/conversations/{sid}.json', root / f'dialogue/{sid}.json')
            for kind, ids in [('boxes', ['roses', 'thorns']), ('speakers', ['senpai', 'senpai-angry', 'bf-pixel', 'spirit'])]:
                for cid in ids:
                    definition = read(data / f'dialogue/{kind}/{cid}.json')
                    folder = root / 'dialogue' / kind / cid
                    copy(data / f'dialogue/{kind}/{cid}.json', folder / 'data.json')
                    graphic(resolve(definition['assetPath'], 'week6'), folder, definition['animations'], True)
            graphic(assets / 'week6/images/weeb/senpaiCrazy', root / 'dialogue/explosion',
                    [{'name': 'idle', 'prefix': 'Senpai Pre Explosion instance 1', 'frameRate': 24}], True)
            for name in ['Senpai_Dies', 'ANGRY_TEXT_BOX', 'pixelText', 'textboxClick']:
                source = next(assets.rglob(name + '.ogg'))
                copy(source, root / f'audio/{name}.ogg')
            for name in ['Lunchbox', 'LunchboxScary']:
                copy(assets / f'week6/music/{name}.ogg', root / f'audio/{name}.ogg')
        if week == 5:
            for name, prefix in [('santa_speaks_assets', 'santa whole scene'), ('parents_shoot_assets', 'parents whole scene')]:
                graphic(assets / f'week5/images/christmas/{name}', root / 'cutscene' / name,
                        [{'name': 'cutscene', 'prefix': prefix}], symbol=prefix)
            for name in ['Lights_Turn_On', 'santa_emotion', 'santa_shot_n_falls']:
                copy(assets / f'week5/sounds/{name}.ogg', root / f'audio/{name}.ogg')
        for sid, songweek in [('satin-panties', 4), ('high', 4), ('milf', 4), ('cocoa', 5), ('eggnog', 5),
                             ('eggnog-erect', 5), ('winter-horrorland', 5), ('senpai', 6), ('roses', 6), ('thorns', 6)]:
            source = assets / f'scripts/songs/{sid}.hxc'
            if songweek == week and source.is_file():
                copy(source, root / 'Source/songs' / source.name)
        (root / 'source-manifest.json').write_text(json.dumps(copied, indent=2) + '\n', encoding='utf-8')

    if weeks is not None and 6 not in weeks:
        return
    resources = output.parents[1] / 'Resources'
    def resource(source, folder, name=None):
        target = resources / folder / (name or source.name)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)

    for filename, source in [('arrows-pixels', 'week6/images/weeb/pixelUI/arrows-pixels'),
                             ('pixelNoteSplash', 'week6/images/pixelNoteSplash'),
                             ('pixelNoteHoldCover', 'week6/images/pixelNoteHoldCover')]:
        for suffix in ['.png', '.xml']:
            resource(assets / (source + suffix), 'FunkinNotes/Pixel', filename + suffix)
    resource(assets / 'week6/images/weeb/pixelUI/arrowEndsNew.png', 'FunkinNotes/Pixel')
    for name in ['sick', 'good', 'bad', 'shit'] + [f'num{i}' for i in range(10)]:
        resource(assets / f'images/ui/popup/pixel/{name}.png', 'FunkinHud/Pixel')
    for name in ['ready', 'set', 'go']:
        resource(assets / f'shared/images/ui/countdown/pixel/{name}.png', 'FunkinHud/Pixel')
        resource(assets / f'shared/images/ui/countdown/funkin/{name}.png', 'FunkinHud/Countdown')
    for name in ['introTHREE', 'introTWO', 'introONE', 'introGO']:
        resource(assets / f'shared/sounds/gameplay/countdown/pixel/{name}.ogg', 'FunkinHud/Pixel')
        resource(assets / f'shared/sounds/gameplay/countdown/funkin/{name}.ogg', 'FunkinHud/Countdown')
    resource(assets / 'week6/music/breakfast-pixel/breakfast-pixel.ogg', 'FunkinPause', 'breakfast-pixel.ogg')
    resource(assets / 'shared/sounds/gameplay/gameover/fnf_loss_sfx-pixel.ogg', 'FunkinHud/Pixel', 'loss.ogg')
    for source, name in [('gameOver-pixel', 'gameOver'), ('gameOverEnd-pixel', 'gameOverEnd')]:
        resource(assets / f'shared/music/gameplay/gameover/{source}.ogg', 'FunkinHud/Pixel', name + '.ogg')
    shutil.copyfile(resources.parent / 'Fonts/pixel.otf', resources / 'FunkinHud/Pixel/dialogue.otf')
    shutil.copyfile(resources.parent / 'Fonts/vcr.ttf', resources / 'FunkinHud/Countdown/vcr.ttf')
