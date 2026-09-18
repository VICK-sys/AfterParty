import argparse
import hashlib
import json
from pathlib import Path

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
    count, heads = 0, 0
    for sid, folder in [('pico', '01-Pico'), ('philly-nice', '02-Philly'), ('blammed', '03-Blammed')]:
        target = output / '03-Week3' / folder
        picker = read(target / 'meta.json')
        assert list(picker['Song Difficulties']) == ['Easy', 'Normal', 'Hard', 'Erect', 'Nightmare']
        assert not any('-pico' in path.name.lower() for path in target.rglob('*'))
        for suffix in ['', '-erect']:
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
            assert sidecar['stage'] == ('phillyTrainErect' if suffix else 'phillyTrain')
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
                assert sidecar['noteKinds'][difficulty] == []
                if suffix:
                    assert picker['Song Variations'][difficulty.title()]['Asset Suffix'] == suffix
                count += 1
                heads += len(actual)
            stems = []
            for role in ['player', 'opponent']:
                character = metadata['playData']['characters'][role+'Vocals'][0]
                stem = assets / f'songs/{sid}/Voices-{character}{suffix}.ogg'
                assert digest(stem) == digest(target / f'Voices-{role}{suffix}.ogg')
                stems.append(stem)
            mixed = decode(ffmpeg, stems, '[0:a][1:a]amix=inputs=2:normalize=0[out]')
            preview = decode(ffmpeg, [target / f'Voices{suffix}.ogg'])
            assert abs(len(mixed)-len(preview)) < 100
            energy = sum(x*x for x in mixed[::31])
            noise = error(preview, mixed)
            assert noise < energy / 1000
            assert error(preview[441:], mixed) > noise * 10, 'Shifted vocal control passed'
            print(f'{sid}{suffix}: exact charts, events, instrumentals, vocal stems, and mixed preview passed')
    assert (count, heads) == (15, 7022)
    root = output / 'Week3Assets'
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
    pico = read(root / 'characters/pico/graphic.json')
    source = animate(assets / 'shared/images/characters/pico/basic-animations')
    assert pico['frames'] == source['frames']
    for animation in read(root / 'characters/pico/character.json')['animations']:
        assert pico['animations'][animation['name']]['frames'] == source['labels'][animation['prefix']]
    assert read(root / 'characters/pico/character.json')['flipX'] is True
    for name in ['sky', 'city', 'behindTrain', 'street']:
        assert digest(root / f'stages/phillyTrain/{name}/{name}.png') != digest(root / f'stages/phillyTrainErect/{name}/{name}.png')
    assert len([entry for entry in read(output / 'vanilla-import.json')['songs'] if entry['path'].startswith('03-Week3/')]) == 6
    print('WEEK 3 ASSETS PASSED: 15 charts, 7,022 heads, exact source audio and atlas hashes, excluded Pico mixes, and swapped-side controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg)
