import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

from ImportVanillaMixes import mixes, stem_path
from ImportVanillaSongs import SONGS
from ImportVanillaWeek2 import animate, save_graphic, sparrow


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def media_hash(ffmpeg, path, stream):
    codec = ['-c:a', 'pcm_f32le'] if ':a:' in stream else []
    result = subprocess.run([ffmpeg, '-v', 'error', '-i', str(path), '-map', stream,
                             *codec, '-f', 'hash', '-hash', 'sha256', '-'], check=True, capture_output=True, text=True)
    return result.stdout.strip()


def check_graphic(path):
    graphic = read(path)
    assert graphic['bounds'][2] > 0 and graphic['bounds'][3] > 0, path
    assert graphic['animations'], path
    for animation in graphic['animations'].values():
        assert animation['frames'], path
        assert all(0 <= index < len(graphic['frames']) for index in animation['frames']), path
    assert all((path.parent / quad['image']).is_file() for frame in graphic['frames'] for quad in frame), path


def check_stress_sing_frames(output):
    root = output / 'Week7Assets/characters'
    folder = root / 'pico-holding-nene'
    source = read(folder / 'Animation.json')
    labels = {frame['N'].rstrip(): list(range(frame['I'], frame['I'] + frame.get('DU', 1)))
              for layer in source['AN']['TL']['L'] for frame in layer['FR'] if 'N' in frame}
    imported = read(folder / 'graphic.json')['animations']
    with tempfile.TemporaryDirectory() as temporary:
        target = Path(temporary) / 'graphic.json'
        regenerated = animate(folder)
        definitions = [item for item in read(folder / 'character.json')['animations'] if not item.get('assetPath')]
        save_graphic(target, regenerated, definitions)
        for direction in ['UP', 'DOWN', 'LEFT', 'RIGHT']:
            sing = 'sing' + direction
            miss = sing + 'miss'
            for animations in [imported, regenerated['animations']]:
                assert animations[sing]['frames'] == labels[direction.lower()], sing
                assert animations[miss]['frames'] == labels[direction.lower() + ' miss'], miss
                assert set(animations[sing]['frames']).isdisjoint(animations[miss]['frames']), direction
        try:
            save_graphic(target, animate(folder), [{'name': 'invalid', 'prefix': 'u'}])
        except ValueError:
            pass
        else:
            raise AssertionError('Partial Animate label matched unrelated clips.')
        boyfriend = root / 'bf-holding-gf'
        graphic = sparrow(boyfriend / 'bfAndGF')
        definitions = [item for item in read(boyfriend / 'character.json')['animations'] if not item.get('assetPath')]
        save_graphic(target, graphic, definitions)
        assert graphic['animations'] == read(boyfriend / 'graphic.json')['animations']
    print('STRESS ANIMATIONS PASSED: exact source hit and miss frames, reimport, partial-label rejection, and Boyfriend control.')


def run(assets, output, ffmpeg, skip_media=False):
    check_stress_sing_frames(output)
    data = assets / ('preload/data' if (assets / 'preload/data').is_dir() else 'data')
    manifest = read(output / 'vanilla-import.json')['songs']
    manifest_keys = [(entry['id'], entry.get('variation', '')) for entry in manifest]
    assert len(manifest_keys) == len(set(manifest_keys)), 'Duplicate song variation entries.'
    expected_keys = {(sid, variation) for sid, variation, _, _ in mixes(data)}
    actual_keys = {key for key in manifest_keys if key[1] in ['pico', 'bf']}
    assert actual_keys == expected_keys and len(expected_keys) == 17
    charts = heads = events = 0
    characters = set()
    weeks = set()
    for sid, variation, bundle, folder in mixes(data):
        target = output / bundle / folder
        week = int(bundle[:2])
        weeks.add(week)
        metadata_path = data / f'songs/{sid}/{sid}-metadata-{variation}.json'
        chart_path = data / f'songs/{sid}/{sid}-chart-{variation}.json'
        metadata, chart = read(metadata_path), read(chart_path)
        assert digest(metadata_path) == digest(target / 'Source/metadata.json')
        assert digest(chart_path) == digest(target / 'Source/chart.json')
        assert digest(assets / f'songs/{sid}/Inst-{variation}.ogg') == digest(target / 'Inst.ogg')
        base_folder = next(base for song, _, base in SONGS if song == sid)
        assert digest(target / 'Inst.ogg') != digest(output / bundle / base_folder / 'Inst.ogg'), sid
        assert (sid, '') in manifest_keys
        entry = next(entry for entry in manifest if (entry['id'], entry.get('variation', '')) == (sid, variation))
        assert entry['path'] == f'{bundle}/{folder}'
        assert entry['instrumentalSha256'] == digest(target / 'Inst.ogg')
        assert entry['vocalOffsets'] == metadata.get('offsets', {}).get('vocals', {})
        picker = read(target / 'meta.json')
        assert picker['Song Name'] == metadata['songName']
        assert list(picker['Song Difficulties']) == ['Easy', 'Normal', 'Hard']
        assert picker['Song Credits'] == {'Composer': metadata['artist'], 'Charter': metadata['charter']}
        assert not list(target.glob('Chart-erect.json'))
        sidecar = read(target / 'Vanilla.json')
        assert sidecar['song'] == sid and sidecar['variation'] == variation
        for key in ['stage', 'characters', 'noteStyle']:
            assert sidecar[key] == metadata['playData'].get(key, 'funkin')
        assert sidecar['events'] == chart['events']
        assert sidecar['timeChanges'] == metadata['timeChanges']
        for role in ['player', 'opponent']:
            assert digest(stem_path(assets, sid, metadata, role, variation)) == digest(target / f'Voices-{role}.ogg')
        assert (target / 'Voices.ogg').is_file()
        for difficulty in metadata['playData']['difficulties']:
            converted = read(target / f'Chart-{difficulty}.json')['song']
            expected = sorted((note['t'], note['d'], note.get('l', 0)) for note in chart['notes'][difficulty])
            actual = sorted((note[0], note[1] if section['mustHitSection'] else (note[1] + 4) % 8, note[2])
                            for section in converted['notes'] for note in section['sectionNotes'])
            assert actual == expected, (sid, difficulty)
            assert sorted((time, (lane + 4) % 8, length) for time, lane, length in actual) != expected
            assert converted['speed'] == chart['scrollSpeed'][difficulty]
            assert converted['bpm'] == metadata['timeChanges'][0]['bpm']
            assert sidecar['noteKinds'][difficulty] == [note for note in chart['notes'][difficulty] if note.get('k')]
            assert entry['notes'][difficulty] == len(actual)
            charts += 1
            heads += len(actual)
        assert chart['notes']['easy'] != chart['notes']['hard']
        for role in ['player', 'opponent', 'girlfriend']:
            characters.add((week, metadata['playData']['characters'][role]))
        events += len(chart['events'])
    assert (charts, heads, events) == (51, 29804, 1329), (charts, heads, events)
    for week, cid in characters | {(2, 'pico-playable'), (2, 'nene')}:
        folder = output / f'Week{week}Assets/characters/{cid}'
        assert digest(folder / 'character.json') == digest(data / f'characters/{cid}.json')
        declared = {animation['name'] for animation in read(folder / 'character.json')['animations'] if animation['name'] != 'fakeoutDeath'}
        source_offsets = {animation['name']: animation.get('offsets', [0, 0]) for animation in read(data / f'characters/{cid}.json')['animations']}
        available = set()
        for path in folder.rglob('graphic.json'):
            check_graphic(path)
            available.update(read(path)['animations'])
            for name, animation in read(path)['animations'].items():
                if name in source_offsets:
                    assert animation['offset'] == source_offsets[name], (path, name, 'animation offset')
        assert declared <= available, (week, cid, declared - available)
        if (cid.startswith('nene') or cid == 'otis-speaker') and (folder / 'Animation.json').is_file():
            graphic = read(folder / 'graphic.json')
            assert graphic['hitboxSize'] == [int(value) for value in graphic['bounds'][2:]]
    for week in weeks:
        root = output / f'Week{week}Assets'
        for path, entry in read(root / 'source-manifest.json').items():
            assert digest(root / path) == entry['sha256'] == digest(assets / entry['source']), path
    for week, names in [(2, ['abotSystem/dark.png']),
                        (6, ['picoPixel_mask.png', 'nenePixel_mask.png', 'aBotPixelSpeaker_mask.png']),
                        (7, ['neneTankmen_mask.png', 'tankmanCaptainBloody_mask.png'])]:
        for name in names:
            assert (output / f'Week{week}Assets/effects' / name).is_file()
    for week, names in [(3, ['doppleganger', 'bloodPool', 'cigarette']),
                        (5, ['knife']),
                        (6, ['abotHead', 'aBotPixelBody', 'aBotPixelSpeaker', 'aBotPixelBack', 'aBotVizPixel', 'knife']),
                        (7, ['comboHeart', 'otisMuzzle'])]:
        for name in names:
            check_graphic(output / f'Week{week}Assets/effects/{name}/graphic.json')
    for cid in ['senpai-pico', 'roses-pico', 'roses-pico-censored']:
        conversation = output / f'Week6Assets/dialogue/{cid}.json'
        assert digest(conversation) == digest(data / f'dialogue/conversations/{cid}.json')
        for entry in read(conversation)['dialogue']:
            speaker = output / 'Week6Assets/dialogue/speakers' / entry['speaker']
            check_graphic(speaker / 'graphic.json')
            assert entry['speakerAnimation'] in read(speaker / 'graphic.json')['animations']
    root = output / 'Week7Assets'
    speaker = read(root / 'speaker-chart-pico.json')
    assert speaker == read(data / 'songs/stress/stress-chart-pico.json')['notes']['picospeaker']
    assert len(speaker) == 584 and speaker != read(root / 'speaker-chart.json')
    assert digest(root / 'video/stress-pico-ending.srt') == digest(data / 'songs/stress/subtitles/end-cutscene-pico.srt')
    assert digest(output / '07-Week7/03-Stress-Pico/Subtitles.txt') == digest(data / 'songs/stress/subtitles/song-lyrics-pico.srt')
    resources = output.parents[1] / 'Resources'
    for path, entry in read(resources / 'FunkinHud/Pico/mix-manifest.json').items():
        assert digest(resources / path) == entry['sha256'] == digest(assets / entry['source'])
    assert (resources / 'FunkinHud/Icons/icon-tankman-bloody.png').is_file()
    if not skip_media:
        source = root / 'Source/videos/stress-pico.mkv'
        assert media_hash(ffmpeg, source, '0:v:0') == media_hash(ffmpeg, root / 'video/stress-pico.mp4', '0:v:0')
        assert media_hash(ffmpeg, source, '0:a:0') == media_hash(ffmpeg, root / 'audio/stress-picoCutscene.wav', '0:a:0')
        assert (root / 'video/stress-pico.srt').stat().st_size > 0
    print(f'MIX ASSETS PASSED: {charts} charts, {heads:,} heads, {events:,} events, 17 variants, source hashes, companion assets, and negative controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--skip-media', action='store_true')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg, args.skip_media)
