import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

from ImportVanillaSongs import SONGS


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def media_hash(path, stream):
    codec = ['-c:a', 'pcm_f32le'] if ':a:' in stream else []
    result = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-map', stream,
                             *codec, '-f', 'hash', '-hash', 'sha256', '-'], check=True, capture_output=True, text=True)
    return result.stdout.strip()


def run(assets, output):
    data = assets / ('preload/data' if (assets / 'preload/data').exists() else 'data')
    charts = heads = variants = 0
    for sid, bundle, folder in SONGS:
        if bundle != '07-Week7':
            continue
        target = output / bundle / folder
        picker = read(target / 'meta.json')
        difficulties = []
        for suffix in ['', '-erect']:
            metadata_path = data / f'songs/{sid}/{sid}-metadata{suffix}.json'
            if not metadata_path.exists():
                assert not (target / 'Chart-erect.json').exists()
                continue
            metadata = read(metadata_path)
            source_path = data / f'songs/{sid}/{sid}-chart{suffix}.json'
            chart = read(source_path)
            assert digest(metadata_path) == digest(target / f'Source/metadata{suffix}.json')
            assert digest(source_path) == digest(target / f'Source/chart{suffix}.json')
            assert digest(assets / f'songs/{sid}/Inst{suffix}.ogg') == digest(target / f'Inst{suffix}.ogg')
            sidecar = read(target / f'Vanilla{suffix}.json')
            for key in ['stage', 'characters', 'noteStyle']:
                assert sidecar[key] == metadata['playData'][key]
            assert sidecar['events'] == chart['events']
            assert sidecar['timeChanges'] == metadata['timeChanges']
            for role in ['player', 'opponent']:
                cid = metadata['playData']['characters'][role + 'Vocals'][0]
                stem = assets / f'songs/{sid}/Voices-{cid}{suffix}.ogg'
                if not stem.exists():
                    stem = assets / f'songs/{sid}/Voices-{cid.split("-")[0]}{suffix}.ogg'
                assert digest(stem) == digest(target / f'Voices-{role}{suffix}.ogg')
            for difficulty in metadata['playData']['difficulties']:
                difficulties.append(difficulty.title())
                converted = read(target / f'Chart-{difficulty}.json')['song']
                expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in chart['notes'][difficulty])
                actual = sorted((n[0], n[1] if section['mustHitSection'] else (n[1]+4) % 8, n[2])
                                for section in converted['notes'] for n in section['sectionNotes'])
                assert actual == expected
                assert [(t, (d+4) % 8, length) for t, d, length in actual] != expected
                assert converted['speed'] == chart['scrollSpeed'][difficulty]
                assert converted['bpm'] == metadata['timeChanges'][0]['bpm']
                assert sidecar['noteKinds'][difficulty] == [n for n in chart['notes'][difficulty] if n.get('k')]
                if suffix:
                    assert picker['Song Variations'][difficulty.title()]['Asset Suffix'] == suffix
                charts += 1
                heads += len(actual)
            variants += 1
        assert list(picker['Song Difficulties']) == difficulties
        assert not any('pico' in path.name.lower() for path in target.rglob('*'))
    assert charts == 11 and heads == 7253 and variants == 4
    root = output / 'Week7Assets'
    for path, entry in read(root / 'source-manifest.json').items():
        assert digest(root / path) == entry['sha256'] == digest(assets / entry['source'])
    for path in root.rglob('graphic.json'):
        graphic = read(path)
        assert graphic['bounds'][2] > 0 and graphic['bounds'][3] > 0
        for animation in graphic['animations'].values():
            assert animation['frames'] and all(0 <= i < len(graphic['frames']) for i in animation['frames'])
        assert all((path.parent / quad['image']).is_file() for frame in graphic['frames'] for quad in frame)
        assert len({quad['image'] for frame in graphic['frames'] for quad in frame}) == 1
    shooting = read(root / 'speaker-chart.json')
    runner = read(root / 'effects/runner/graphic.json')
    frames = sorted(ET.parse(assets / 'week7/images/tankmanKilled1.xml').getroot(), key=lambda entry: entry.get('name'))
    assert runner['frameSizes'] == [[int(entry.get('frameWidth', entry.get('width'))),
                                     int(entry.get('frameHeight', entry.get('height')))] for entry in frames]
    widths = {name: runner['frameSizes'][animation['frames'][0]][0] for name, animation in runner['animations'].items()}
    assert widths == {'run': 505, 'shot1': 1044, 'shot2': 1221}
    assert shooting == read(data / 'songs/stress/stress-chart.json')['notes']['picospeaker']
    assert len(shooting) == 546 and shooting != shooting[1:]
    assert {'ugh', 'hehPrettyGood'} <= read(root / 'characters/tankman/censor/graphic.json')['animations'].keys()
    assert {'firstDeath', 'deathLoop', 'deathConfirm'} <= read(root / 'characters/bf-holding-gf/death/graphic.json')['animations'].keys()
    for sid in ['ugh', 'guns', 'stress']:
        source = root / f'Source/videos/{sid}.mkv'
        assert media_hash(source, '0:v:0') == media_hash(root / f'video/{sid}.mp4', '0:v:0')
        assert media_hash(source, '0:a:0') == media_hash(root / f'audio/{sid}Cutscene.wav', '0:a:0')
        assert (root / f'video/{sid}.srt').stat().st_size > 100
    print(f'WEEK 7 ASSETS PASSED: {charts} charts, {heads:,} heads, {variants} audio variants, source hashes, 546 speaker cues, lossless cutscene media, excluded Pico mixes, swapped-side controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.output)
