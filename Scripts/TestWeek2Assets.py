import argparse
import hashlib
import json
from pathlib import Path
import sys

from TestVanillaSongAssets import decode, error
from ImportVanillaWeek2 import animate


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(assets, output, ffmpeg):
    data = assets / 'preload/data'
    if not data.is_dir():
        data = assets / 'data'
    count, heads, noanim = 0, 0, 0
    for sid, folder in [('spookeez', '01-Spookeez'), ('south', '02-South'), ('monster', '03-Monster')]:
        target = output / '02-Week2' / folder
        picker = read(target / 'meta.json')
        expected_difficulties = ['Easy', 'Normal', 'Hard'] + ([] if sid == 'monster' else ['Erect', 'Nightmare'])
        assert list(picker['Song Difficulties']) == expected_difficulties
        assert not any('pico' in path.name.lower() for path in target.rglob('*'))
        for suffix in ([''] if sid == 'monster' else ['', '-erect']):
            source = data / 'songs' / sid
            metadata = read(source / f'{sid}-metadata{suffix}.json')
            chart = read(source / f'{sid}-chart{suffix}.json')
            assert digest(source / f'{sid}-metadata{suffix}.json') == digest(target / f'Source/metadata{suffix}.json')
            assert digest(source / f'{sid}-chart{suffix}.json') == digest(target / f'Source/chart{suffix}.json')
            assert digest(assets / f'songs/{sid}/Inst{suffix}.ogg') == digest(target / f'Inst{suffix}.ogg')
            sidecar = read(target / f'Vanilla{suffix}.json')
            assert sidecar['events'] == chart['events']
            assert sidecar['timeChanges'] == metadata['timeChanges']
            assert sidecar['characters'] == metadata['playData']['characters']
            for difficulty in metadata['playData']['difficulties']:
                converted = read(target / f'Chart-{difficulty}.json')['song']
                expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in chart['notes'][difficulty])
                actual = sorted((n[0], n[1] if section['mustHitSection'] else (n[1]+4) % 8, n[2])
                                for section in converted['notes'] for n in section['sectionNotes'])
                assert expected == actual
                wrong = actual.copy()
                wrong[0] = (wrong[0][0], (wrong[0][1]+4) % 8, wrong[0][2])
                assert wrong != expected
                assert converted['speed'] == chart['scrollSpeed'][difficulty]
                assert sidecar['noteKinds'][difficulty] == [n for n in chart['notes'][difficulty] if n.get('k')]
                noanim += len(sidecar['noteKinds'][difficulty])
                if suffix:
                    assert picker['Song Variations'][difficulty.title()]['Asset Suffix'] == suffix
                count += 1
                heads += len(actual)
            stems = []
            for role in ['player', 'opponent']:
                character = metadata['playData']['characters'][role+'Vocals'][0]
                stem = assets / f'songs/{sid}/Voices-{character}{suffix}.ogg'
                if not stem.is_file():
                    stem = assets / f'songs/{sid}/Voices-{character.split("-")[0]}{suffix}.ogg'
                assert digest(stem) == digest(target / f'Voices-{role}{suffix}.ogg')
                stems.append(stem)
            mixed = decode(ffmpeg, stems, '[0:a][1:a]amix=inputs=2:normalize=0[out]')
            preview = decode(ffmpeg, [target / f'Voices{suffix}.ogg'])
            assert abs(len(mixed)-len(preview)) < 100
            energy = sum(x*x for x in mixed[::31])
            assert error(preview, mixed) < energy / 1000
            print(f'{sid}{suffix}: source charts, events, tempos, exact instrumental and both vocal stems passed')
    assert (count, heads, noanim) == (13, 7045, 12)
    monster = read(output / '02-Week2/03-Monster/Vanilla.json')
    assert len(monster['timeChanges']) == 14
    assert monster['timeChanges'] != [monster['timeChanges'][0]]
    root = output / 'Week2Assets'
    for path, entry in read(root / 'source-manifest.json').items():
        assert digest(root / path) == entry['sha256'] == digest(assets / entry['source'])
    for path in root.rglob('graphic.json'):
        graphic = read(path)
        assert graphic['bounds'][2] > 0 and graphic['bounds'][3] > 0
        for animation in graphic['animations'].values():
            assert animation['frames']
            assert all(0 <= index < len(graphic['frames']) for index in animation['frames'])
        for frame in graphic['frames']:
            assert all((path.parent / quad['image']).is_file() for quad in frame)
    monster_source = assets / 'shared/images/characters/monster'
    with_hat = animate(monster_source)
    without_hat = read(root / 'characters/monster/graphic.json')
    assert sum(map(len, with_hat['frames'])) > sum(map(len, without_hat['frames'])), 'Hat visibility control did not differ'
    assert read(root / 'characters/bf/graphic.json')['bounds'] != read(root / 'characters/bf-dark/graphic.json')['bounds']
    opponent = read(root / 'characters/spooky-dark/graphic.json')
    for animation, frame, size, rotated in [('danceLeft', 0, (381, 549), False), ('danceLeft', 2, (379, 541), True),
                                            ('danceLeft', 4, (357, 484), True), ('singRIGHT', 2, (437, 533), True)]:
        quad = opponent['frames'][opponent['animations'][animation]['frames'][frame]][0]
        xy = quad['xy']
        assert (xy[2]-xy[0], xy[7]-xy[1]) == size, f'{animation} frame {frame}: stretched opponent frame'
        assert quad['rotated'] == rotated
        assert (tuple(quad['rect'][2:]) != size) == rotated, 'Packed-dimensions control did not distinguish rotation'
    assert read(root / 'stages/spookyMansion/halloweenBG/graphic.json')['animations']['lightning']['frames'] == list(range(30))
    print('WEEK 2 ASSETS PASSED: 13 charts, 7,045 heads, 12 noanim notes, 14 Monster tempo markers, exact source audio and atlas hashes.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg)
