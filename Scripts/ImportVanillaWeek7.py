import hashlib
import json
import shutil
import subprocess
import xml.etree.ElementTree as ET

from ImportVanillaWeek2 import read, save_graphic, sparrow
from ImportVanillaWeeks456 import import_assets as import_campaign


def import_assets(assets, data, output, ffmpeg='ffmpeg'):
    import_campaign(assets, data, output, {7: (['tankmanBattlefield', 'tankmanBattlefieldErect'],
                    ['bf', 'gf-tankmen', 'bf-holding-gf', 'pico-speaker', 'tankman'])})
    root = output / 'Week7Assets'
    manifest = read(root / 'source-manifest.json')

    def copy(source, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest[target.relative_to(root).as_posix()] = {
            'source': source.relative_to(assets).as_posix(),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}

    runner = root / 'effects/runner'
    clouds = root / 'effects/clouds'
    clouds.mkdir(parents=True, exist_ok=True)
    source_clouds = root / 'stages/tankmanBattlefield/clouds2'
    for image in source_clouds.glob('*.png'):
        shutil.copyfile(image, clouds / image.name)
    cloud_graphic = read(source_clouds / 'graphic.json')
    tile = cloud_graphic['frames'][0][0]
    width, height = tile['rect'][2:]
    cloud_graphic['frames'] = [[dict(tile, xy=[x, 0, min(x + width, 3200), 0, min(x + width, 3200), 235, x, 235],
                                   rect=[0, 0, min(width, 3200 - x), min(height, 235)])
                                for x in range(0, 3200, int(width))]]
    cloud_graphic['bounds'] = [0, 0, 3200, 235]
    (clouds / 'graphic.json').write_text(json.dumps(cloud_graphic, separators=(',', ':')) + '\n', encoding='utf-8')
    for suffix in ['.png', '.xml']:
        copy(assets / ('week7/images/tankmanKilled1' + suffix), runner / ('tankmanKilled1' + suffix))
    graphic = sparrow(assets / 'week7/images/tankmanKilled1')
    entries = sorted(ET.parse(assets / 'week7/images/tankmanKilled1.xml').getroot(), key=lambda entry: entry.get('name'))
    graphic['frameSizes'] = [[int(entry.get('frameWidth', entry.get('width'))), int(entry.get('frameHeight', entry.get('height')))]
                             for entry in entries]
    graphic['scaledOffsets'] = True
    save_graphic(runner / 'graphic.json', graphic, [
        {'name': 'run', 'prefix': 'tankman running', 'looped': True},
        {'name': 'shot1', 'prefix': 'John Shot 1', 'offsets': [300, 200]},
        {'name': 'shot2', 'prefix': 'John Shot 2', 'offsets': [300, 200]}])
    copy(assets / 'week7/images/erect/masks/gfTankmen_mask.png', root / 'effects/gfTankmen_mask.png')
    for sid in ['ugh', 'guns', 'stress']:
        copy(assets / f'scripts/songs/{sid}.hxc', root / f'Source/songs/{sid}.hxc')
    for name in ['TankmanSprite', 'TankmanSpriteGroup']:
        copy(assets / f'scripts/stages/props/{name}.hxc', root / f'Source/props/{name}.hxc')
    shooting = read(data / 'songs/stress/stress-chart.json')['notes']['picospeaker']
    (root / 'speaker-chart.json').write_text(json.dumps(shooting, separators=(',', ':')) + '\n', encoding='utf-8')
    for index in range(1, 26):
        name = f'jeffGameover-{index}.ogg'
        copy(assets / 'week7/sounds/jeffGameover' / name, root / 'audio' / name)
    for sid in ['ugh', 'guns', 'stress']:
        source = assets / f'videos/videos/{sid}Cutscene.mkv'
        copy(source, root / f'Source/videos/{sid}.mkv')
        target = root / 'video'
        target.mkdir(exist_ok=True)
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:v:0', '-c:v', 'copy', '-an', str(target / f'{sid}.mp4')], check=True)
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:a:0', '-c:a', 'pcm_f32le', str(root / f'audio/{sid}Cutscene.wav')], check=True)
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:s:0', str(target / f'{sid}.srt')], check=True)
    (root / 'source-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
