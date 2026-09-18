import hashlib
import json
import shutil
import struct

from ImportVanillaWeek2 import animate, image_path, read, save_graphic, sparrow


def import_assets(assets, data, output):
    root = output / 'Week3Assets'
    copied = {}

    def copy(source, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        copied[target.relative_to(root).as_posix()] = {
            'source': source.relative_to(assets).as_posix(),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}

    for cid in ['bf', 'gf', 'pico']:
        character = read(data / f'characters/{cid}.json')
        target = root / 'characters' / cid
        copy(data / f'characters/{cid}.json', target / 'character.json')
        path = image_path(assets, character['assetPath'])
        if path.is_dir():
            for source in path.iterdir():
                if source.suffix in ('.png', '.json'):
                    copy(source, target / source.name)
            graphic = animate(path)
        else:
            for suffix in ['.png', '.xml']:
                copy(path.with_suffix(suffix), target / (path.name + suffix))
            graphic = sparrow(path)
        save_graphic(target / 'graphic.json', graphic, [a for a in character['animations'] if not a.get('assetPath')])
        print(f'{cid}: {len(graphic["frames"])} frames, bounds {graphic["bounds"]}')

    path = assets / 'shared/images/characters/bf-death'
    target = root / 'characters/bf-death'
    for source in path.iterdir():
        if source.suffix in ('.png', '.json'):
            copy(source, target / source.name)
    save_graphic(target / 'graphic.json', animate(path),
                 [a for a in read(data / 'characters/bf.json')['animations'] if a.get('assetPath') == 'shared:characters/bf-death'])

    for sid in ['phillyTrain', 'phillyTrainErect']:
        target = root / 'stages' / sid
        copy(data / f'stages/{sid}.json', target / 'stage.json')
        for prop in read(target / 'stage.json')['props']:
            path = assets / 'week3/images' / (prop['assetPath'] + '.png')
            folder = target / prop['name']
            copy(path, folder / path.name)
            w, h = struct.unpack('>II', path.read_bytes()[16:24])
            graphic = {'frames': [[{'xy': [0, 0, w, 0, w, h, 0, h], 'rect': [0, 0, w, h],
                                   'image': path.name, 'rotated': False, 'alpha': 1}]],
                       'labels': {'idle': [0]}, 'bounds': [0, 0, w, h], 'fps': 24}
            save_graphic(folder / 'graphic.json', graphic, [{'name': 'idle', 'prefix': 'idle'}])
        copy(assets / f'scripts/stages/{sid}.hxc', root / 'Source' / f'{sid}.hxc')
    copy(assets / 'week3/sounds/train_passes.ogg', root / 'train_passes.ogg')
    copy(assets / 'shaders/building.frag', root / 'Source/building.frag')
    copy(assets / 'shaders/adjustColor.frag', root / 'Source/adjustColor.frag')
    copy(assets / 'scripts/shaders/BuildingEffectShader.hxc', root / 'Source/BuildingEffectShader.hxc')
    copy(assets / 'scripts/characters/gf.hxc', root / 'Source/gf.hxc')
    (root / 'source-manifest.json').write_text(json.dumps(copied, indent=2) + '\n', encoding='utf-8')
