import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from ImportVanillaSongs import COLORS, SONGS, convert, mix_vocals, read, write
from ImportVanillaWeek2 import animate, save_graphic, sparrow
from ImportVanillaWeeks456 import import_assets as import_campaign, static_graphic


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def mixes(data):
    for sid, bundle, folder in SONGS:
        for variation in ['pico', 'bf']:
            source = data / f'songs/{sid}/{sid}-metadata-{variation}.json'
            if source.is_file():
                yield sid, variation, bundle, folder + ('-Pico' if variation == 'pico' else '-BF')


def stem_path(assets, sid, metadata, role, variation):
    cid = metadata['playData']['characters'].get(role + 'Vocals', [metadata['playData']['characters'][role]])[0]
    source = assets / f'songs/{sid}/Voices-{cid}-{variation}.ogg'
    if not source.is_file():
        source = assets / f'songs/{sid}/Voices-{cid.split("-")[0]}-{variation}.ogg'
    return source


def import_extras(assets, data, output, ffmpeg):
    needed = {}
    for sid, variation, bundle, _ in mixes(data):
        week = int(bundle[:2])
        metadata = read(data / f'songs/{sid}/{sid}-metadata-{variation}.json')
        needed.setdefault(week, set()).update(metadata['playData']['characters'][role] for role in ['player', 'opponent', 'girlfriend'])
    needed[2].update(['pico-playable', 'nene'])
    weeks = {week: ([], sorted(cid for cid in ids if not (output / f'Week{week}Assets/characters/{cid}/character.json').is_file()))
             for week, ids in needed.items()}
    import_campaign(assets, data, output, weeks, extras_only=True)
    manifests = {week: read(output / f'Week{week}Assets/source-manifest.json') for week in weeks}

    def copy(source, target, week):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifests[week][target.relative_to(output / f'Week{week}Assets').as_posix()] = {
            'source': source.relative_to(assets).as_posix(), 'sha256': digest(source)}

    def graphic(source, target, week, animations=None, pixel=False):
        target.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            for path in source.iterdir():
                if path.suffix in ('.json', '.png'):
                    copy(path, target / path.name, week)
            result = animate(source)
        elif source.with_suffix('.xml').is_file():
            for suffix in ('.png', '.xml'):
                copy(source.with_suffix(suffix), target / (source.name + suffix), week)
            result = sparrow(source)
        else:
            copy(source.with_suffix('.png'), target / (source.name + '.png'), week)
            result = static_graphic(source.with_suffix('.png'))
        if animations is None:
            if not result['labels']:
                result['labels']['idle'] = list(range(len(result['frames'])))
            animations = [{'name': key, 'prefix': key, 'looped': target.name == 'doppleganger' and key.startswith('loop')} for key in result['labels']]
        result['pixel'] = pixel
        result['scaledOffsets'] = True
        save_graphic(target / 'graphic.json', result, animations)

    for week, ids in needed.items():
        root = output / f'Week{week}Assets'
        for cid in ids:
            folder = root / 'characters' / cid
            if (cid.startswith('nene') or cid == 'otis-speaker') and (folder / 'Animation.json').is_file():
                result = read(folder / 'graphic.json')
                result['bounds'] = animate(folder, filter_bounds=True)['bounds']
                result['hitboxSize'] = [int(value) for value in result['bounds'][2:]]
                (folder / 'graphic.json').write_text(json.dumps(result, separators=(',', ':')) + '\n', encoding='utf-8')
        if any(cid.startswith('nene') and cid != 'nene-pixel' or cid == 'otis-speaker' for cid in ids):
            for name in ['abotSystem', 'systemEyes', 'stereoBG']:
                graphic(assets / 'shared/images/characters/abot' / name, root / 'effects' / name, week)
            graphic(assets / 'shared/images/characters/abot/aBotViz', root / 'effects/visualizer', week,
                    [{'name': str(i), 'prefix': f'viz{i}'} for i in range(1, 8)])
            graphic(assets / 'shared/images/characters/NeneKnifeToss', root / 'effects/knife', week,
                    [{'name': 'throw', 'prefix': 'knife toss0'}])
        for sid, variation, bundle, _ in mixes(data):
            if int(bundle[:2]) != week:
                continue
            script = assets / f'scripts/songs/{sid}-{variation}.hxc'
            if script.is_file():
                copy(script, root / 'Source/songs' / script.name, week)

    root = output / 'Week2Assets'
    copy(assets / 'shared/images/characters/abot/dark/abotSystem/spritemap1.png', root / 'effects/abotSystem/dark.png', 2)
    root = output / 'Week3Assets'
    for name, source, animations in [
        ('doppleganger', 'pico_doppleganger', None), ('bloodPool', 'bloodPool', None),
        ('cigarette', 'cigarette', [{'name': 'cigarette spit', 'prefix': 'cigarette spit'}])]:
        graphic(assets / 'week3/images/philly/erect' / source, root / 'effects' / name, 3, animations)
    for directory in ['sounds/cutscene', 'music/cutscene']:
        for source in (assets / 'week3' / directory).glob('*.ogg'):
            copy(source, root / 'audio/cutscene' / source.name, 3)
    for name in ['PicoDopplegangerSprite', 'PicoBloodPool']:
        copy(assets / f'scripts/stages/props/{name}.hxc', root / f'Source/props/{name}.hxc', 3)
    root = output / 'Week5Assets'
    graphic(assets / 'shared/images/characters/neneChristmasKnife', root / 'effects/knife', 5,
            [{'name': 'throw', 'prefix': 'knife toss xmas0'}])
    root = output / 'Week6Assets'
    for name in ['picoPixel_mask', 'nenePixel_mask', 'aBotPixelSpeaker_mask']:
        copy(assets / f'week6/images/weeb/erect/masks/{name}.png', root / 'effects' / (name + '.png'), 6)
    for name, animations in [
        ('abotHead', [{'name': name, 'prefix': name + '0'} for name in ['toleft', 'toright']]),
        ('aBotPixelBody', [{'name': 'danceLeft', 'prefix': 'danceLeft'}, {'name': 'danceRight', 'prefix': 'danceRight'},
                           {'name': 'lowerKnife', 'prefix': 'return'}]),
        ('aBotPixelSpeaker', [{'name': 'danceLeft', 'prefix': 'danceLeft'}]),
        ('aBotPixelBack', None),
        ('aBotVizPixel', [{'name': str(i), 'prefix': f'viz{i}'} for i in range(1, 8)])]:
        graphic(assets / 'shared/images/characters/abotPixel' / name, root / 'effects' / name, 6, animations, True)
    graphic(assets / 'shared/images/characters/nenePixel/nenePixelKnifeToss', root / 'effects/knife', 6,
            [{'name': 'throw', 'prefix': 'knifetosscolor0'}], True)
    speakers = set()
    for sid in ['senpai-pico', 'roses-pico', 'roses-pico-censored']:
        source = data / f'dialogue/conversations/{sid}.json'
        copy(source, root / f'dialogue/{sid}.json', 6)
        speakers.update(entry['speaker'] for entry in read(source)['dialogue'])
    for speaker in sorted(speakers):
        target = root / 'dialogue/speakers' / speaker
        if (target / 'data.json').is_file():
            continue
        source = data / f'dialogue/speakers/{speaker}.json'
        definition = read(source)
        copy(source, target / 'data.json', 6)
        library, path = definition['assetPath'].split(':', 1) if ':' in definition['assetPath'] else ('week6', definition['assetPath'])
        source = assets / library / 'images' / path
        if not source.is_dir() and not source.with_suffix('.png').is_file():
            source = assets / 'shared/images' / path
        graphic(source, target, 6, definition['animations'], True)
    root = output / 'Week7Assets'
    for name in ['neneTankmen_mask', 'tankmanCaptainBloody_mask']:
        copy(assets / f'week7/images/erect/masks/{name}.png', root / 'effects' / (name + '.png'), 7)
    graphic(assets / 'shared/images/characters/Nene_comboHeart', root / 'effects/comboHeart', 7,
            [{'name': 'idle', 'prefix': 'hearts aflutter'}])
    graphic(assets / 'shared/images/characters/otis/muzzle-flashes/otis_flashes', root / 'effects/otisMuzzle', 7,
            [{'name': f'shoot{i + 1}', 'prefix': prefix} for i, prefix in enumerate(['shoot back0', 'shoot back low0', 'shoot forward0', 'shoot forward low0'])])
    for source in (assets / 'week7/sounds/jeffGameover-pico').glob('*.ogg'):
        copy(source, root / 'audio/jeffGameover-pico' / source.name, 7)
    copy(assets / 'week7/sounds/erect/endCutscene.ogg', root / 'audio/erect/endCutscene.ogg', 7)
    copy(data / 'songs/stress/subtitles/end-cutscene-pico.srt', root / 'video/stress-pico-ending.srt', 7)
    write(root / 'speaker-chart-pico.json', read(data / 'songs/stress/stress-chart-pico.json')['notes']['picospeaker'])
    source = assets / 'videos/videos/stressPicoCutscene.mkv'
    copy(source, root / 'Source/videos/stress-pico.mkv', 7)
    for arguments in [
        ['-map', '0:v:0', '-c:v', 'copy', '-an', str(root / 'video/stress-pico.mp4')],
        ['-map', '0:a:0', '-c:a', 'pcm_f32le', str(root / 'audio/stress-picoCutscene.wav')],
        ['-map', '0:s:0', str(root / 'video/stress-pico.srt')]]:
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source), *arguments], check=True)
    for week, manifest in manifests.items():
        write(output / f'Week{week}Assets/source-manifest.json', manifest)
    resources = output.parents[1] / 'Resources'
    manifest = {}
    for source, target in [
        ('shared/sounds/gameplay/gameover/fnf_loss_sfx-pixel-pico.ogg', 'FunkinHud/Pico/loss-pixel-pico.ogg'),
        ('week7/sounds/gameplay/gameover/fnf_loss_sfx-pico-and-nene.ogg', 'FunkinHud/Pico/loss-pico-and-nene.ogg'),
        ('shared/music/gameplay/gameover/gameOver-pixel-pico.ogg', 'FunkinHud/Pico/gameOver-pixel-pico.ogg'),
        ('shared/music/gameplay/gameover/gameOverEnd-pixel-pico.ogg', 'FunkinHud/Pico/gameOverEnd-pixel-pico.ogg'),
        ('images/icons/icon-tankman-bloody.png', 'FunkinHud/Icons/icon-tankman-bloody.png'),
        ('images/icons/icon-pico-pixel.png', 'FunkinHud/Icons/icon-pico-pixel.png')]:
        path = resources / target
        path.parent.mkdir(parents=True, exist_ok=True)
        source_path = assets / source
        if not source_path.is_file():
            source_path = next(assets.rglob(Path(source).name))
        shutil.copyfile(source_path, path)
        manifest[target] = {'source': source_path.relative_to(assets).as_posix(), 'sha256': digest(path)}
    write(resources / 'FunkinHud/Pico/mix-manifest.json', manifest)


def run(assets, output, ffmpeg, extras_only=False):
    data = assets / ('preload/data' if (assets / 'preload/data').is_dir() else 'data')
    manifest = read(output / 'vanilla-import.json')
    if not extras_only:
        for sid, variation, bundle, folder in mixes(data):
            target = output / bundle / folder
            metadata_path = data / f'songs/{sid}/{sid}-metadata-{variation}.json'
            chart_path = data / f'songs/{sid}/{sid}-chart-{variation}.json'
            metadata, chart = read(metadata_path), read(chart_path)
            difficulties = metadata['playData']['difficulties']
            assert difficulties == ['easy', 'normal', 'hard'], (sid, difficulties)
            (target / 'Source').mkdir(parents=True, exist_ok=True)
            shutil.copyfile(metadata_path, target / 'Source/metadata.json')
            shutil.copyfile(chart_path, target / 'Source/chart.json')
            shutil.copyfile(assets / f'songs/{sid}/Inst-{variation}.ogg', target / 'Inst.ogg')
            mix_vocals(assets / f'songs/{sid}', target, metadata, '-' + variation, ffmpeg, 10)
            (target / f'Voices-{variation}.ogg').replace(target / 'Voices.ogg')
            for role in ['player', 'opponent']:
                shutil.copyfile(stem_path(assets, sid, metadata, role, variation), target / f'Voices-{role}.ogg')
            for difficulty in difficulties:
                write(target / f'Chart-{difficulty}.json', convert(sid, metadata, chart, difficulty))
            write(target / 'meta.json', {
                'Song Name': metadata['songName'],
                'Song Credits': {'Composer': metadata['artist'], 'Charter': metadata['charter']},
                'Song Difficulties': {difficulty.title(): dict(zip(('r', 'g', 'b', 'a'), (*COLORS[difficulty], 1))) for difficulty in difficulties},
                'Song Description': "Friday Night Funkin' 0.8.6."})
            write(target / 'Vanilla.json', {'song': sid, 'variation': variation,
                  'bpm': metadata['timeChanges'][0]['bpm'], 'stage': metadata['playData']['stage'],
                  'timeChanges': metadata['timeChanges'], 'opponent': metadata['playData']['characters']['opponent'],
                  'characters': metadata['playData']['characters'], 'events': chart['events'],
                  'noteStyle': metadata['playData'].get('noteStyle', 'funkin'),
                  'noteKinds': {d: [n for n in chart['notes'][d] if n.get('k')] for d in difficulties}})
            subtitle = data / f'songs/{sid}/subtitles/song-lyrics-{variation}.srt'
            if subtitle.is_file():
                shutil.copyfile(subtitle, target / 'Subtitles.txt')
            entry = {'id': sid, 'variation': variation, 'path': f'{bundle}/{folder}',
                     'bpm': metadata['timeChanges'][0]['bpm'], 'notes': {d: len(chart['notes'][d]) for d in difficulties},
                     'vocalOffsets': metadata.get('offsets', {}).get('vocals', {}),
                     'instrumentalSha256': digest(target / 'Inst.ogg')}
            existing = next((old for old in manifest['songs'] if (old['id'], old.get('variation', '')) == (sid, variation)), None)
            if existing is None:
                manifest['songs'].append(entry)
            elif existing != entry:
                raise ValueError(f'Existing mix manifest entry differs: {sid}/{variation}')
            print(f'{sid}/{variation}: ' + ', '.join(f'{d}={len(chart["notes"][d])}' for d in difficulties), flush=True)
        write(output / 'vanilla-import.json', manifest)
    import_extras(assets, data, output, ffmpeg)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--extras-only', action='store_true')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg, args.extras_only)
