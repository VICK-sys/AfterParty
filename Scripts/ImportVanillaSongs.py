import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess

REVISION = '9300719cb261d23f72344807055be6d109c9949c'
SONGS = [('tutorial', '00-Tutorial', '01-Tutorial'), ('bopeebo', '01-Week1', '01-Bopeebo'),
         ('fresh', '01-Week1', '02-Fresh'), ('dadbattle', '01-Week1', '03-DadBattle'),
         ('spookeez', '02-Week2', '01-Spookeez'), ('south', '02-Week2', '02-South'),
         ('monster', '02-Week2', '03-Monster'), ('pico', '03-Week3', '01-Pico'),
         ('philly-nice', '03-Week3', '02-Philly'), ('blammed', '03-Week3', '03-Blammed'),
         ('satin-panties', '04-Week4', '01-SatinPanties'), ('high', '04-Week4', '02-High'),
         ('milf', '04-Week4', '03-MILF'), ('cocoa', '05-Week5', '01-Cocoa'),
         ('eggnog', '05-Week5', '02-Eggnog'), ('winter-horrorland', '05-Week5', '03-WinterHorrorland'),
         ('senpai', '06-Week6', '01-Senpai'), ('roses', '06-Week6', '02-Roses'),
         ('thorns', '06-Week6', '03-Thorns'), ('ugh', '07-Week7', '01-Ugh'),
         ('guns', '07-Week7', '02-Guns'), ('stress', '07-Week7', '03-Stress'),
         ('darnell', '08-Weekend1', '01-Darnell'), ('lit-up', '08-Weekend1', '02-LitUp'),
         ('2hot', '08-Weekend1', '03-2hot'), ('blazin', '08-Weekend1', '04-Blazin')]
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
    if changes[0]['t'] != 0:
        raise ValueError(f'{song_id}: the first tempo must start at time zero.')
    if metadata.get('offsets', {}).get('instrumental', 0):
        raise ValueError(f'{song_id}: instrumental offsets require a separate conversion.')
    bpm = changes[0]['bpm']
    length = 240000 / bpm
    notes = chart['notes'][difficulty]
    if any(not str(n.get('k') or '').startswith('weekend-1-') and n.get('k') not in (None, '', 'noanim', 'mom', 'censor', 'ugh', 'hehPrettyGood') or not 0 <= n['d'] < 8 for n in notes):
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
    return {'song': {'song': metadata['songName'], 'bpm': bpm, 'needsVoices': song_id != 'blazin',
                     'player1': metadata['playData']['characters']['player'], 'player2': metadata['playData']['characters']['opponent'],
                     'speed': chart['scrollSpeed'][difficulty], 'notes': sections, 'validScore': True}}


def mix_vocals(source, target, metadata, suffix, ffmpeg, quality=8):
    if not list(source.glob('Voices*.ogg')):
        return
    characters = metadata['playData']['characters']
    offsets = metadata.get('offsets', {}).get('vocals', {})
    stems = characters.get('playerVocals', [characters['player']]) + characters.get('opponentVocals', [characters['opponent']])
    inputs = []
    filters = []
    for index, character in enumerate(stems):
        stem = source / f'Voices-{character}{suffix}.ogg'
        if not stem.is_file():
            stem = source / f'Voices-{character.split("-")[0]}{suffix}.ogg'
        inputs += ['-i', str(stem)]
        offset = offsets.get(character, 0)
        adjustment = f'atrim=start={-offset / 1000},asetpts=PTS-STARTPTS' if offset < 0 else f'adelay={offset}:all=1'
        filters.append(f'[{index}:a]{adjustment}[v{index}]')
    filters.append(''.join(f'[v{i}]' for i in range(len(stems))) +
                   f'amix=inputs={len(stems)}:duration=longest:normalize=0[out]')
    subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *inputs,
                    '-filter_complex', ';'.join(filters), '-map', '[out]',
                    '-c:a', 'libvorbis', '-q:a', str(quality), str(target / f'Voices{suffix}.ogg')], check=True)


def run(assets, output, ffmpeg, erect_only=False, week2_only=False, week3_only=False, weeks456_only=False, week7_only=False, weekend1_only=False):
    data_root = assets / 'preload/data'
    if not data_root.is_dir():
        data_root = assets / 'data'
    manifest = {'version': '0.8.6', 'assetCommit': REVISION, 'songs': []}
    if erect_only or week2_only or week3_only or weeks456_only or week7_only or weekend1_only:
        manifest = read(output / 'vanilla-import.json')
    for song_id, bundle, directory in SONGS:
        if weekend1_only and bundle != '08-Weekend1':
            continue
        if week7_only and bundle != '07-Week7':
            continue
        if week2_only and bundle != '02-Week2':
            continue
        if week3_only and bundle != '03-Week3':
            continue
        if weeks456_only and bundle not in ('04-Week4', '05-Week5', '06-Week6'):
            continue
        target = output / bundle / directory
        target.mkdir(parents=True, exist_ok=True)
        (target / 'Source').mkdir(exist_ok=True)
        metadata = read(data_root / f'songs/{song_id}/{song_id}-metadata.json')
        song_meta = {
            'Song Name': metadata['songName'],
            'Song Credits': {'Composer': metadata['artist'], 'Charter': metadata.get('charter', 'The Funkin\' Crew')},
            'Song Difficulties': {},
            'Song Description': "Friday Night Funkin' 0.8.6. " + ('Tutorial.' if song_id == 'tutorial'
                else 'Week 4: Mommy Must Murder.' if bundle == '04-Week4'
                else 'Week 5: Red Snow.' if bundle == '05-Week5'
                else 'Week 6: Hating Simulator.' if bundle == '06-Week6'
                else 'Weekend 1: Due Debts.' if bundle == '08-Weekend1'
                else 'Week 7: Tankman.' if bundle == '07-Week7'
                else 'Week 3: Pico.' if bundle == '03-Week3'
                else 'Week 2: Spooky Month.' if bundle == '02-Week2' else 'Week 1: Daddy Dearest.')
        }
        if erect_only:
            song_meta = read(target / 'meta.json')
        variations = ['', '-erect'] if (data_root / f'songs/{song_id}/{song_id}-metadata-erect.json').is_file() else ['']
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
                  'timeChanges': metadata['timeChanges'],
                  'opponent': metadata['playData']['characters']['opponent'],
                  'characters': metadata['playData']['characters'], 'events': chart['events'],
                  'noteStyle': metadata['playData'].get('noteStyle', 'funkin'),
                  'noteKinds': {d: [n for n in chart['notes'][d] if n.get('k')] for d in difficulties}})
            shutil.copyfile(data_root / f'songs/{song_id}/{song_id}-chart{suffix}.json', target / f'Source/chart{suffix}.json')
            shutil.copyfile(data_root / f'songs/{song_id}/{song_id}-metadata{suffix}.json', target / f'Source/metadata{suffix}.json')
            source = assets / f'songs/{song_id}'
            shutil.copyfile(source / f'Inst{suffix}.ogg', target / f'Inst{suffix}.ogg')
            mix_vocals(source, target, metadata, suffix, ffmpeg, 10 if bundle >= '03-Week3' else 8)
            if bundle >= '02-Week2' and list(source.glob('Voices*.ogg')):
                characters = metadata['playData']['characters']
                for role in ['player', 'opponent']:
                    character = characters.get(role+'Vocals', [characters[role]])[0]
                    stem = source / f'Voices-{character}{suffix}.ogg'
                    if not stem.is_file():
                        stem = source / f'Voices-{character.split("-")[0]}{suffix}.ogg'
                    shutil.copyfile(stem, target / f'Voices-{role}{suffix}.ogg')
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
    if not week2_only and not week3_only and not weeks456_only and not week7_only and not weekend1_only:
        from ImportVanillaCharacters import run as import_characters
        import_characters(assets, output)
        stage = output / 'Stages/mainStageErect'
        stage.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(data_root / 'stages/mainStageErect.json', stage / 'stage.json')
        for image in (assets / 'week1/images/erect').iterdir():
            if image.suffix in ('.png', '.xml'):
                shutil.copyfile(image, stage / image.name)
        write(output / '00-Tutorial/bundle-meta.json', {'bundleName': 'Tutorial', 'authorName': "The Funkin' Crew"})
        write(output / '01-Week1/bundle-meta.json', {'bundleName': 'Week 1: Daddy Dearest', 'authorName': "The Funkin' Crew"})
    if not week3_only and not weeks456_only and not week7_only and not weekend1_only:
        from ImportVanillaWeek2 import import_assets
        import_assets(assets, data_root, output)
        write(output / '02-Week2/bundle-meta.json', {'bundleName': 'Week 2: Spooky Month', 'authorName': "The Funkin' Crew"})
    if not week2_only and not weeks456_only and not week7_only and not weekend1_only:
        from ImportVanillaWeek3 import import_assets
        import_assets(assets, data_root, output)
        write(output / '03-Week3/bundle-meta.json', {'bundleName': 'Week 3: Pico', 'authorName': "The Funkin' Crew"})
    if not week2_only and not week3_only and not week7_only and not weekend1_only:
        from ImportVanillaWeeks456 import import_assets
        import_assets(assets, data_root, output)
        for week, title in [(4, 'Mommy Must Murder'), (5, 'Red Snow'), (6, 'Hating Simulator')]:
            write(output / f'0{week}-Week{week}/bundle-meta.json',
                  {'bundleName': f'Week {week}: {title}', 'authorName': "The Funkin' Crew"})
    if not week2_only and not week3_only and not weeks456_only and not weekend1_only:
        from ImportVanillaWeek7 import import_assets
        import_assets(assets, data_root, output, ffmpeg)
        write(output / '07-Week7/bundle-meta.json', {'bundleName': 'Week 7: Tankman', 'authorName': "The Funkin' Crew"})
    if not week2_only and not week3_only and not weeks456_only and not week7_only:
        from ImportVanillaWeekend1 import import_assets
        import_assets(assets, data_root, output, ffmpeg)
        write(output / '08-Weekend1/bundle-meta.json', {'bundleName': 'Weekend 1: Due Debts', 'authorName': "The Funkin' Crew"})
    write(output / 'vanilla-import.json', manifest)
    if (assets / 'LICENSE.md').is_file():
        shutil.copyfile(assets / 'LICENSE.md', output / 'Vanilla-LICENSE.md')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--erect-only', action='store_true')
    weeks = parser.add_mutually_exclusive_group()
    weeks.add_argument('--week2-only', action='store_true')
    weeks.add_argument('--week3-only', action='store_true')
    weeks.add_argument('--weeks456-only', action='store_true')
    weeks.add_argument('--week7-only', action='store_true')
    weeks.add_argument('--weekend1-only', action='store_true')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg, args.erect_only, args.week2_only, args.week3_only, args.weeks456_only, args.week7_only, args.weekend1_only)
