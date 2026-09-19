import hashlib
import json
import shutil
import subprocess
from pathlib import Path

from ImportVanillaWeek2 import animate, read, save_graphic, sparrow
from ImportVanillaWeeks456 import import_assets as import_campaign, static_graphic


def import_blazin_placement(root):
    for cid in ['pico-blazin', 'darnell-blazin', 'nene']:
        folder = root / 'characters' / cid
        path = folder / 'graphic.json'
        graphic = read(path)
        graphic['bounds'] = animate(folder, filter_bounds=True)['bounds']
        graphic['hitboxSize'] = [int(value) for value in graphic['bounds'][2:]]
        path.write_text(json.dumps(graphic, separators=(',', ':')) + '\n', encoding='utf-8')


def import_assets(assets, data, output, ffmpeg='ffmpeg'):
    import_campaign(assets, data, output, {8: (['phillyStreets', 'phillyStreetsErect', 'phillyBlazin'],
                    ['pico-playable', 'pico-blazin', 'darnell', 'darnell-blazin', 'nene'])})
    root = output / 'Week8Assets'
    import_blazin_placement(root)
    manifest = read(root / 'source-manifest.json')

    def copy(source, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest[target.relative_to(root).as_posix()] = {
            'source': source.relative_to(assets).as_posix(),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}

    def graphic(source, name, animations=None):
        target = root / 'effects' / name
        target.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            for path in source.iterdir():
                if path.suffix in ('.json', '.png'):
                    copy(path, target / path.name)
            result = animate(source)
        elif source.with_suffix('.xml').is_file():
            for suffix in ('.png', '.xml'):
                copy(source.with_suffix(suffix), target / (source.name + suffix))
            result = sparrow(source)
        else:
            copy(source.with_suffix('.png'), target / (source.name + '.png'))
            result = static_graphic(source.with_suffix('.png'))
        if animations is None:
            if not result['labels']:
                result['labels']['idle'] = list(range(len(result['frames'])))
            animations = [{'name': k, 'prefix': k} for k in result['labels']]
        result['scaledOffsets'] = True
        save_graphic(target / 'graphic.json', result, animations)

    images = assets / 'weekend1/images'
    graphic(images / 'spraycanAtlas', 'can')
    graphic(images / 'PicoBullet', 'casing', [{'name': 'pop', 'prefix': 'Pop0'}, {'name': 'idle', 'prefix': 'Bullet0', 'looped': True}])
    for name, prefix in [('SpraypaintExplosion', 'Explosion 1 movie0'),
                         ('spraypaintExplosionEZ', 'explosion round 1 short0'),
                         ('CanImpactParticle', 'CanImpactParticle0')]:
        graphic(images / name, name, [{'name': 'idle', 'prefix': prefix}])
    graphic(images / 'wked1_cutscene_1_can', 'cutsceneCan',
            [{'name': 'up', 'prefix': 'can kicked up0'}, {'name': 'forward', 'prefix': 'can kick quick0'}])
    graphic(assets / 'shared/images/characters/NeneKnifeToss', 'knife', [{'name': 'throw', 'prefix': 'knife toss0'}])
    for name in ['abotSystem', 'systemEyes', 'stereoBG']:
        graphic(assets / 'shared/images/characters/abot' / name, name)
    graphic(assets / 'shared/images/characters/abot/aBotViz', 'visualizer',
            [{'name': str(i), 'prefix': f'viz{i}'} for i in range(1, 8)])
    for name, path in [('sky', 'phillyStreets/phillySkybox'), ('skyBlazin', 'phillyBlazin/skyBlur')]:
        graphic(images / path, name)
    for name in ['phillySkybox', 'mistMid', 'mistBack']:
        graphic(images / 'phillyStreets/erect' / name, name)
    for path in (assets / 'weekend1/sounds').rglob('*.ogg'):
        copy(path, root / 'audio' / path.relative_to(assets / 'weekend1/sounds'))
    copy(assets / 'weekend1/music/darnellCanCutscene/darnellCanCutscene.ogg', root / 'audio/darnellCanCutscene.ogg')
    for sid in ['darnell', 'lit-up', '2hot', 'blazin']:
        copy(assets / f'scripts/songs/{sid}.hxc', root / f'Source/songs/{sid}.hxc')
    for name in ['SpraycanAtlasSprite', 'ABotAtlasSprite']:
        candidates = list((assets / 'scripts').rglob(name + '.hxc'))
        if candidates:
            copy(candidates[0], root / f'Source/props/{name}.hxc')
    for sid in ['darnell', '2hot', 'blazin']:
        source = assets / f'videos/videos/{sid}Cutscene.mp4'
        copy(source, root / f'Source/videos/{sid}.mp4')
        (root / 'video').mkdir(exist_ok=True)
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:v:0', '-c:v', 'copy', '-an', str(root / f'video/{sid}.mp4')], check=True)
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:a:0', '-c:a', 'pcm_f32le', str(root / f'audio/{sid}Cutscene.wav')], check=True)
    resources = output.parents[1] / 'Resources'
    for name in ['pico', 'darnell']:
        source = assets / f'shared/images/icons/icon-{name}.png'
        if not source.is_file():
            source = next(assets.rglob(f'icon-{name}.png'))
        shutil.copyfile(source, resources / f'FunkinHud/Icons/icon-{name}.png')
    for suffix in ['pico', 'pico-gutpunch', 'pico-explode']:
        source = next(assets.rglob(f'fnf_loss_sfx-{suffix}.ogg'))
        target = resources / f'FunkinHud/Pico/loss-{suffix}.ogg'
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    for name in ['gameOver-pico', 'gameOverEnd-pico']:
        shutil.copyfile(next(assets.rglob(name + '.ogg')), resources / f'FunkinHud/Pico/{name}.ogg')
    shutil.copyfile(next(assets.rglob('breakfast-pico.ogg')), resources / 'FunkinPause/breakfast-pico.ogg')
    (root / 'source-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    args = parser.parse_args()
    import_assets(args.assets, args.assets / 'data', Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
