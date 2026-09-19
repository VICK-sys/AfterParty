import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from ImportVanillaSongs import COLORS, convert, read, write
from ImportVanillaWeek2 import animate, image_path, save_graphic
from ImportVanillaWeeks456 import static_graphic


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(assets, audio, source, output):
    data = assets / 'data'
    root = output / 'SpaghettiAssets'
    manifest = {}

    def copy(path, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
        manifest[target.relative_to(root).as_posix()] = {
            'source': path.relative_to(assets).as_posix(), 'sha256': digest(path)}

    def graphic(path, target, animations=None, keyword=None):
        target.mkdir(parents=True, exist_ok=True)
        if path.is_dir():
            for file in path.iterdir():
                if file.suffix in ('.json', '.png'):
                    copy(file, target / file.name)
            result = animate(path, attachment_keyword=keyword)
        else:
            copy(path.with_suffix('.png'), target / (path.name + '.png'))
            result = static_graphic(path.with_suffix('.png'))
        if not animations:
            if not result['labels']:
                result['labels']['idle'] = list(range(len(result['frames'])))
            animations = [{'name': name, 'prefix': name} for name in result['labels']]
        result['scaledOffsets'] = True
        save_graphic(target / 'graphic.json', result, animations)
        print(f'{target.relative_to(root)}: {len(result["frames"])} frames', flush=True)

    for name in ['yunjin', 'kazuha', 'chaewon', 'eunchae', 'sakura', 'gf']:
        cid = 'sserafim-' + name
        character = read(data / f'characters/{cid}.json')
        target = root / 'characters' / cid
        copy(data / f'characters/{cid}.json', target / 'character.json')
        script = assets / f'scripts/characters/{cid}.hxc'
        copy(script, root / 'Source/characters' / script.name)
        keyword = None if name == 'gf' else 'mouth yunjin' if name == 'yunjin' else 'mouth edit' if name == 'sakura' else 'mouth default'
        graphic(image_path(assets, character['assetPath']), target,
                [a for a in character['animations'] if not a.get('assetPath')], keyword)
        if keyword:
            offsets = {anim: {'offset': [float(x), float(y)], 'angle': float(angle)}
                       for anim, x, y, angle in re.findall(r"'([^']+)'\s*=>\s*\{\s*offset:\s*\[([^,]+),\s*([^\]]+)\],\s*angle:\s*([^\s}]+)", script.read_text())}
            write(target / 'lipsync.json', {'poses': offsets, 'flipX': 'lipSyncSprite.flipX = true' in script.read_text()})
        extra = [a for a in character['animations'] if a.get('assetPath')]
        if extra:
            graphic(image_path(assets, extra[0]['assetPath']), target / 'death', extra)

    stage = read(data / 'stages/sserafim.json')
    copy(data / 'stages/sserafim.json', root / 'stages/sserafim/stage.json')
    for prop in stage['props']:
        if not prop['assetPath'].startswith('#'):
            graphic(assets / 'sserafim/images' / prop['assetPath'], root / 'stages/sserafim' / prop['name'])
    for name in ['floor', 'cutscene/floor-cutscene', 'cutscene/cutsceneMain', 'cutscene/bfGetUp', 'cutscene/gfGetUp',
                 'dust/dustMid', 'dust/dustBack', 'end/end1', 'end/end2', 'sserafim-lipsync', 'sserafim-lipsync-yunjin']:
        graphic(assets / 'sserafim/images' / name, root / 'effects' / name)
    for path in (assets / 'sserafim/sounds').rglob('*.ogg'):
        copy(path, root / 'audio' / path.relative_to(assets / 'sserafim/sounds'))
    for name in ['stages/sserafim', 'songs/spaghetti', 'levels/sserafim', 'stages/props/PerspectiveSprite',
                 'stages/props/SserafimLipSyncSprite', 'stages/props/SserafimCutsceneSprite',
                 'stages/props/SserafimBfSprite', 'stages/props/SserafimGfSprite']:
        copy(assets / ('scripts/' + name + '.hxc'), root / ('Source/' + name + '.hxc'))
    shader = source / 'source/funkin/graphics/shaders/SserafimShader.hx'
    target = root / 'Source/SserafimShader.hx'
    shutil.copyfile(shader, target)
    manifest['Source/SserafimShader.hx'] = {'source': 'Funkin-0.8.6/source/funkin/graphics/shaders/SserafimShader.hx', 'sha256': digest(shader)}
    write(root / 'source-manifest.json', manifest)

    song_path = '09-Sserafim/01-Spaghetti'
    song = output / song_path
    metadata_path = data / 'songs/spaghetti/spaghetti-metadata.json'
    chart_path = data / 'songs/spaghetti/spaghetti-chart.json'
    metadata, chart = read(metadata_path), read(chart_path)
    difficulties = metadata['playData']['difficulties']
    for difficulty in difficulties:
        write(song / f'Chart-{difficulty}.json', convert('spaghetti', metadata, chart, difficulty))
    write(song / 'meta.json', {'Song Name': metadata['songName'],
          'Song Credits': {'Composer': metadata['artist'], 'Charter': metadata['charter']},
          'Song Difficulties': {d.title(): dict(zip(('r', 'g', 'b', 'a'), (*COLORS[d], 1))) for d in difficulties},
          'Song Description': "Friday Night Funkin' 0.8.6. LE SSERAFIM."})
    write(song / 'Vanilla.json', {'song': 'spaghetti', 'variation': '', 'bpm': metadata['timeChanges'][0]['bpm'],
          'stage': 'sserafim', 'timeChanges': metadata['timeChanges'],
          'opponent': metadata['playData']['characters']['opponent'], 'characters': metadata['playData']['characters'],
          'events': chart['events'], 'noteStyle': 'funkin',
          'noteKinds': {d: [n for n in chart['notes'][d] if n.get('k')] for d in difficulties}})
    (song / 'Source').mkdir(exist_ok=True)
    shutil.copyfile(chart_path, song / 'Source/chart.json')
    shutil.copyfile(metadata_path, song / 'Source/metadata.json')
    for original, imported in [('Inst.ogg', 'Inst.ogg'), ('Voices-sserafim-sakura.ogg', 'Voices.ogg')]:
        shutil.copyfile(audio / original, song / imported)
    write(output / '09-Sserafim/bundle-meta.json', {'bundleName': 'LE SSERAFIM', 'authorName': "The Funkin' Crew"})
    imported = read(output / 'vanilla-import.json')
    imported['songs'] = [entry for entry in imported['songs'] if entry['id'] != 'spaghetti']
    imported['songs'].append({'id': 'spaghetti', 'variation': '', 'path': song_path,
                            'bpm': metadata['timeChanges'][0]['bpm'], 'notes': {d: len(chart['notes'][d]) for d in difficulties},
                            'vocalOffsets': {}, 'instrumentalSha256': digest(song / 'Inst.ogg'),
                            'vocalsSha256': digest(song / 'Voices.ogg')})
    write(output / 'vanilla-import.json', imported)
    icons = output.parents[1] / 'Resources/FunkinHud/Icons'
    icon_manifest = {}
    for name in ['yunjin', 'kazuha', 'chaewon', 'eunchae', 'sakura']:
        path = assets / f'images/icons/icon-{name}.png'
        target = icons / path.name
        shutil.copyfile(path, target)
        icon_manifest[path.name] = {'source': path.relative_to(assets).as_posix(), 'sha256': digest(path)}
    write(icons / 'spaghetti-manifest.json', icon_manifest)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--audio', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.audio, args.source, args.output)
