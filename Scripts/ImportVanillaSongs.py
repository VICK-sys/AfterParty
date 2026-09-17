import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess

REVISION = '9300719cb261d23f72344807055be6d109c9949c'
SONGS = [('tutorial', '00-Tutorial', '01-Tutorial'), ('bopeebo', '01-Week1', '01-Bopeebo'),
         ('fresh', '01-Week1', '02-Fresh'), ('dadbattle', '01-Week1', '03-DadBattle')]
COLORS = {'easy': (0, 1, 0), 'normal': (1, 1, 0), 'hard': (1, 0, 0),
          'erect': (0.8, 0.3, 1), 'nightmare': (1, 0.2, 0.6)}


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def focus(event):
    value = event['v']
    return int(value.get('char', 0)) if isinstance(value, dict) else int(value)


def convert(song_id, metadata, chart, difficulty):
    changes = metadata['timeChanges']
    if len(changes) != 1 or changes[0]['t'] != 0:
        raise ValueError(f'{song_id}: this importer requires a single tempo from time zero.')
    if metadata.get('offsets', {}).get('instrumental', 0):
        raise ValueError(f'{song_id}: instrumental offsets require a separate conversion.')
    bpm = changes[0]['bpm']
    length = 240000 / bpm
    notes = chart['notes'][difficulty]
    if any(n.get('k') or not 0 <= n['d'] < 8 for n in notes):
        raise ValueError(f'{song_id}: unsupported note kind or lane.')
    end = max(n['t'] + n.get('l', 0) for n in notes)
    sections = []
    camera = sorted((e for e in chart['events'] if e['e'] == 'FocusCamera'), key=lambda e: e['t'])
    for index in range(math.floor(end / length) + 1):
        previous = [e for e in camera if e['t'] <= index * length + 0.0001]
        must_hit = not previous or focus(previous[-1]) == 0
        sections.append({'lengthInSteps': 16, 'mustHitSection': must_hit, 'sectionNotes': [], 'typeOfSection': 0})
    for note in notes:
        section = sections[math.floor(note['t'] / length)]
        lane = note['d'] if section['mustHitSection'] else (note['d'] + 4) % 8
        section['sectionNotes'].append([note['t'], lane, note.get('l', 0)])
    for section in sections:
        section['sectionNotes'].sort(key=lambda n: (n[0], n[1]))
    expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in notes)
    restored = sorted((n[0], n[1] if section['mustHitSection'] else (n[1] + 4) % 8, n[2])
                      for section in sections for n in section['sectionNotes'])
    if restored != expected:
        raise ValueError(f'{song_id}/{difficulty}: note conversion changed timing, lanes, or sustain lengths.')
    return {'song': {'song': metadata['songName'], 'bpm': bpm, 'needsVoices': True,
                     'player1': 'bf', 'player2': metadata['playData']['characters']['opponent'],
                     'speed': chart['scrollSpeed'][difficulty], 'notes': sections, 'validScore': True}}


def mix_vocals(source, target, metadata, suffix, ffmpeg):
    characters = metadata['playData']['characters']
    offsets = metadata.get('offsets', {}).get('vocals', {})
    stems = characters.get('playerVocals', [characters['player']]) + characters.get('opponentVocals', [characters['opponent']])
    inputs = []
    filters = []
    for index, character in enumerate(stems):
        inputs += ['-i', str(source / f'Voices-{character}{suffix}.ogg')]
        offset = offsets.get(character, 0)
        adjustment = f'atrim=start={-offset / 1000},asetpts=PTS-STARTPTS' if offset < 0 else f'adelay={offset}:all=1'
        filters.append(f'[{index}:a]{adjustment}[v{index}]')
    filters.append(''.join(f'[v{i}]' for i in range(len(stems))) +
                   f'amix=inputs={len(stems)}:duration=longest:normalize=0[out]')
    subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *inputs,
                    '-filter_complex', ';'.join(filters), '-map', '[out]',
                    '-c:a', 'libvorbis', '-q:a', '8', str(target / f'Voices{suffix}.ogg')], check=True)


def run(assets, output, ffmpeg, erect_only=False):
    data_root = assets / 'preload/data'
    if not data_root.is_dir():
        data_root = assets / 'data'
    manifest = {'version': '0.8.6', 'assetCommit': REVISION, 'songs': []}
    if erect_only:
        manifest = read(output / 'vanilla-import.json')
    for song_id, bundle, directory in SONGS:
        target = output / bundle / directory
        target.mkdir(parents=True, exist_ok=True)
        (target / 'Source').mkdir(exist_ok=True)
        metadata = read(data_root / f'songs/{song_id}/{song_id}-metadata.json')
        song_meta = {
            'Song Name': metadata['songName'],
            'Song Credits': {'Composer': metadata['artist'], 'Charter': metadata.get('charter', 'The Funkin\' Crew')},
            'Song Difficulties': {},
            'Song Description': "Friday Night Funkin' 0.8.6. " + ('Tutorial.' if song_id == 'tutorial' else 'Week 1: Daddy Dearest.')
        }
        if erect_only:
            song_meta = read(target / 'meta.json')
        variations = [''] if song_id == 'tutorial' else ['', '-erect']
        for suffix in variations:
            if erect_only and not suffix:
                continue
            metadata = read(data_root / f'songs/{song_id}/{song_id}-metadata{suffix}.json')
            chart = read(data_root / f'songs/{song_id}/{song_id}-chart{suffix}.json')
            difficulties = metadata['playData']['difficulties']
            for difficulty in difficulties:
                song_meta['Song Difficulties'][difficulty.title()] = dict(zip(('r', 'g', 'b', 'a'), (*COLORS[difficulty], 1)))
                if suffix:
                    song_meta.setdefault('Song Variations', {})[difficulty.title()] = {
                        'Asset Suffix': suffix, 'Song Name': metadata['songName'],
                        'Song Credits': {'Composer': metadata['artist'], 'Charter': metadata['charter']}}
                write(target / f'Chart-{difficulty}.json', convert(song_id, metadata, chart, difficulty))
            write(target / f'Vanilla{suffix}.json', {'song': song_id, 'variation': suffix.lstrip('-'),
                  'bpm': metadata['timeChanges'][0]['bpm'], 'stage': metadata['playData']['stage'],
                  'opponent': metadata['playData']['characters']['opponent'], 'events': chart['events']})
            shutil.copyfile(data_root / f'songs/{song_id}/{song_id}-chart{suffix}.json', target / f'Source/chart{suffix}.json')
            shutil.copyfile(data_root / f'songs/{song_id}/{song_id}-metadata{suffix}.json', target / f'Source/metadata{suffix}.json')
            source = assets / f'songs/{song_id}'
            shutil.copyfile(source / f'Inst{suffix}.ogg', target / f'Inst{suffix}.ogg')
            mix_vocals(source, target, metadata, suffix, ffmpeg)
            entry = {'id': song_id, 'variation': suffix.lstrip('-'), 'path': f'{bundle}/{directory}',
                     'bpm': metadata['timeChanges'][0]['bpm'], 'notes': {d: len(chart['notes'][d]) for d in difficulties},
                     'vocalOffsets': metadata.get('offsets', {}).get('vocals', {}),
                     'instrumentalSha256': hashlib.sha256((target / f'Inst{suffix}.ogg').read_bytes()).hexdigest()}
            manifest['songs'] = [old for old in manifest['songs'] if (old['id'], old.get('variation', '')) != (song_id, suffix.lstrip('-'))]
            manifest['songs'].append(entry)
            print(f'{song_id}{suffix}: ' + ', '.join(f'{d}={len(chart["notes"][d])}' for d in difficulties))
        write(target / 'meta.json', song_meta)
        subtitles = data_root / f'songs/{song_id}/subtitles/song-lyrics.srt'
        if subtitles.is_file() and not erect_only:
            shutil.copyfile(subtitles, target / 'Subtitles.txt')
    stage = output / 'Stages/mainStageErect'
    stage.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(data_root / 'stages/mainStageErect.json', stage / 'stage.json')
    for image in (assets / 'week1/images/erect').iterdir():
        if image.suffix in ('.png', '.xml'):
            shutil.copyfile(image, stage / image.name)
    write(output / '00-Tutorial/bundle-meta.json', {'bundleName': 'Tutorial', 'authorName': "The Funkin' Crew"})
    write(output / '01-Week1/bundle-meta.json', {'bundleName': 'Week 1: Daddy Dearest', 'authorName': "The Funkin' Crew"})
    write(output / 'vanilla-import.json', manifest)
    if (assets / 'LICENSE.md').is_file():
        shutil.copyfile(assets / 'LICENSE.md', output / 'Vanilla-LICENSE.md')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--erect-only', action='store_true')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg, args.erect_only)
