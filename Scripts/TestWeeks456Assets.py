import argparse
import hashlib
import json
from pathlib import Path

from ImportVanillaSongs import SONGS


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(assets, output):
    data = assets / 'preload/data'
    if not data.is_dir():
        data = assets / 'data'
    charts, heads, variants = 0, 0, 0
    for sid, bundle, folder in SONGS:
        if bundle[:2] not in ['04', '05', '06']:
            continue
        target = output / bundle / folder
        picker = read(target / 'meta.json')
        expected_difficulties = []
        for suffix in ['', '-erect']:
            source = data / f'songs/{sid}/{sid}-metadata{suffix}.json'
            if not source.is_file():
                assert not (target / 'Chart-erect.json').exists()
                continue
            metadata = read(source)
            source_chart = data / f'songs/{sid}/{sid}-chart{suffix}.json'
            chart = read(source_chart)
            assert digest(source) == digest(target / f'Source/metadata{suffix}.json')
            assert digest(source_chart) == digest(target / f'Source/chart{suffix}.json')
            assert digest(assets / f'songs/{sid}/Inst{suffix}.ogg') == digest(target / f'Inst{suffix}.ogg')
            sidecar = read(target / f'Vanilla{suffix}.json')
            assert sidecar['events'] == chart['events']
            assert sidecar['timeChanges'] == metadata['timeChanges']
            assert sidecar['characters'] == metadata['playData']['characters']
            assert sidecar['stage'] == metadata['playData']['stage']
            assert sidecar['noteStyle'] == metadata['playData']['noteStyle']
            for role in ['player', 'opponent']:
                cid = metadata['playData']['characters'][role + 'Vocals'][0]
                stem = assets / f'songs/{sid}/Voices-{cid}{suffix}.ogg'
                if not stem.is_file():
                    stem = assets / f'songs/{sid}/Voices-{cid.split("-")[0]}{suffix}.ogg'
                assert digest(stem) == digest(target / f'Voices-{role}{suffix}.ogg')
            for difficulty in metadata['playData']['difficulties']:
                expected_difficulties.append(difficulty.title())
                converted = read(target / f'Chart-{difficulty}.json')['song']
                expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in chart['notes'][difficulty])
                actual = sorted((n[0], n[1] if section['mustHitSection'] else (n[1]+4) % 8, n[2])
                                for section in converted['notes'] for n in section['sectionNotes'])
                assert actual == expected
                assert converted['speed'] == chart['scrollSpeed'][difficulty]
                assert converted['bpm'] == metadata['timeChanges'][0]['bpm']
                assert sidecar['noteKinds'][difficulty] == [n for n in chart['notes'][difficulty] if n.get('k')]
                swapped = [(t, (d+4) % 8, length) for t, d, length in actual]
                assert swapped != expected
                if suffix:
                    assert picker['Song Variations'][difficulty.title()]['Asset Suffix'] == suffix
                charts += 1
                heads += len(actual)
            variants += 1
        assert list(picker['Song Difficulties']) == expected_difficulties
        assert not any('pico' in p.name.lower() for p in target.rglob('*'))
    assert charts == 41 and variants == 16
    for week in [4, 5, 6]:
        root = output / f'Week{week}Assets'
        for path, entry in read(root / 'source-manifest.json').items():
            assert digest(root / path) == entry['sha256'] == digest(assets / entry['source'])
        for path in root.rglob('graphic.json'):
            graphic = read(path)
            assert graphic['bounds'][2] > 0 and graphic['bounds'][3] > 0
            for animation in graphic['animations'].values():
                assert animation['frames']
                assert all(0 <= i < len(graphic['frames']) for i in animation['frames'])
            assert all((path.parent / quad['image']).exists() for frame in graphic['frames'] for quad in frame)
            assert len({quad['image'] for frame in graphic['frames'] for quad in frame}) == 1
    assert len(read(output / 'Week5Assets/characters/parents-christmas/graphic.json')['animations']) == 18
    assert 'singDOWN-censor' in read(output / 'Week5Assets/characters/bf-christmas/censor/graphic.json')['animations']
    assert read(output / 'Week6Assets/characters/spirit/graphic.json')['pixel'] is True
    print(f'WEEKS 4-6 ASSETS PASSED: {charts} charts, {heads:,} heads, {variants} audio variants, exact source hashes, note kinds, no Pico mixes, swapped-side controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.output)
