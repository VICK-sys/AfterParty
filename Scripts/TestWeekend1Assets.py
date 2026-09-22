import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

from ImportVanillaSongs import SONGS
from ImportVanillaWeek2 import bounds
from TestVideoCompatibility import check_video


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
        if bundle != '08-Weekend1':
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
            for role in ([] if sid == 'blazin' else ['player', 'opponent']):
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
        assert not any(path.name in ['Inst-pico.ogg', 'Inst-bf.ogg', 'chart-pico.json', 'chart-bf.json'] for path in target.rglob('*'))
    assert charts == 14 and heads == 10089 and variants == 5
    root = output / 'Week8Assets'
    for path, entry in read(root / 'source-manifest.json').items():
        assert digest(root / path) == entry['sha256'] == digest(assets / entry['source'])
    for path in root.rglob('graphic.json'):
        graphic = read(path)
        assert graphic['bounds'][2] > 0 and graphic['bounds'][3] > 0
        assert graphic['animations'], path
        for animation in graphic['animations'].values():
            assert animation['frames'] and all(0 <= i < len(graphic['frames']) for i in animation['frames'])
        assert all((path.parent / quad['image']).is_file() for frame in graphic['frames'] for quad in frame)
        assert len({quad['image'] for frame in graphic['frames'] for quad in frame}) == 1
    pico = read(root / 'characters/pico-playable/alternate1/graphic.json')
    assert {'cock', 'shoot', 'shootMISS'} <= pico['animations'].keys()
    combat = read(root / 'characters/pico-blazin/graphic.json')
    assert {'punchLow1', 'punchLow2', 'hitLow', 'uppercut'} <= combat['animations'].keys()
    for cid, expected in [('pico-blazin', [-534.7, -556.05, 1274.75, 947.9]),
                          ('darnell-blazin', [-451.25, -532.95, 733.34, 645.6]),
                          ('nene', [-329.75, -423.6, 493.62, 566.1903])]:
        graphic = read(root / 'characters' / cid / 'graphic.json')
        assert all(abs(a - b) < .00001 for a, b in zip(graphic['bounds'], expected)), cid
        assert graphic['hitboxSize'] == [int(value) for value in expected[2:]], cid
    assert abs(bounds(combat['frames'])[2] - combat['bounds'][2]) > 200
    assert {'firstDeath', 'deathLoop', 'deathConfirm'} <= read(root / 'characters/pico-playable/death/graphic.json')['animations'].keys()
    assert {'firstDeath-explosion', 'deathLoop-explosion', 'deathConfirm-explosion'} <= read(root / 'characters/pico-playable/explosion/graphic.json')['animations'].keys()
    for sid in ['darnell', '2hot', 'blazin']:
        source = root / f'Source/videos/{sid}.mp4'
        check_video(source, root / f'video/{sid}.mp4')
        assert media_hash(source, '0:a:0') == media_hash(root / f'audio/{sid}Cutscene.wav', '0:a:0')
    print(f'WEEKEND 1 ASSETS PASSED: {charts} charts, {heads:,} heads, {variants} audio variants, source hashes, H.264 cutscenes, lossless audio, excluded BF and Pico mixes, swapped-side controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.output)
