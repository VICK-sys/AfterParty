import argparse
import hashlib
import json
from pathlib import Path
import tempfile

from ImportVanillaWeek2 import save_graphic


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_index_selection():
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'graphic.json'
        source = {'animate': True, 'labels': {'Idle': [3, 7, 11]}, 'frames': [[] for _ in range(15)]}
        animation = {'name': 'idle-hold', 'prefix': 'Idle', 'frameIndices': [2, 3, -1, 0, 2]}
        save_graphic(path, source, [animation])
        assert read(path)['animations']['idle-hold']['frames'] == [11, 3, 11], 'Frame selection escaped its label or changed index order'
        source = {'animate': True, 'labels': {'Idle': [0]}, 'frames': [[], []]}
        try:
            save_graphic(path, source, [dict(animation, frameIndices=[1])])
        except ValueError:
            pass
        else:
            raise AssertionError('Empty animation control was accepted')


def check_character_labels(assets, root, manifest):
    count = 0
    for path in (root / 'characters').glob('*/character.json'):
        atlas = path.parent / 'Animation.json'
        if not atlas.is_file():
            continue
        source = read(assets / manifest[atlas.relative_to(root).as_posix()]['source'])
        labels = {frame['N']: list(range(frame['I'], frame['I'] + frame.get('DU', 1)))
                  for layer in source['AN']['TL']['L'] for frame in layer['FR'] if 'N' in frame}
        graphic = read(path.parent / 'graphic.json')
        for animation in read(path)['animations']:
            if animation.get('assetPath') or animation.get('animType', 'framelabel') != 'framelabel':
                continue
            frames = labels[animation['prefix']]
            selected = animation.get('frameIndices')
            expected = frames if not selected else [frames[i] for i in selected if 0 <= i < len(frames)]
            actual = graphic['animations'][animation['name']]['frames']
            assert actual == expected, f'{path.parent.name}/{animation["name"]}: expected {expected}, got {actual}'
            outside = next((i for i in range(len(graphic['frames'])) if i not in frames), None)
            if outside is not None:
                assert not all(i in frames for i in actual + [outside]), 'Adjacent-animation control passed'
            count += 1
    return count


def run(assets, output):
    files = 0
    graphics = 0
    animations = 0
    for manifest in sorted(output.glob('Week*Assets/source-manifest.json')):
        entries = read(manifest)
        for name, entry in entries.items():
            if not name.startswith('characters/'):
                continue
            actual = digest(manifest.parent / name)
            expected = digest(assets / entry['source'])
            assert actual == entry['sha256'] == expected, name
            assert hashlib.sha256((manifest.parent / name).read_bytes() + b'control').hexdigest() != expected
            files += 1
        for path in (manifest.parent / 'characters').rglob('graphic.json'):
            data = read(path)
            for animation in data['animations'].values():
                assert animation['frames'], path
                assert all(0 <= frame < len(data['frames']) for frame in animation['frames']), path
            for frame in data['frames']:
                for quad in frame:
                    assert (path.parent / quad['image']).is_file(), path
            graphics += 1
        animations += check_character_labels(assets, manifest.parent, entries)
    data_root = assets / 'preload/data'
    if not data_root.is_dir():
        data_root = assets / 'data'
    for path in output.glob('0[01]-*/*/Vanilla*.json'):
        sidecar = read(path)
        suffix = '-erect' if sidecar.get('variation') == 'erect' else ''
        metadata = read(data_root / f'songs/{sidecar["song"]}/{sidecar["song"]}-metadata{suffix}.json')
        assert sidecar['characters'] == metadata['playData']['characters'], path
        assert sidecar['timeChanges'] == metadata['timeChanges'], path
        assert sidecar['stage'] == metadata['playData']['stage'], path
    assert files >= 110 and graphics >= 25
    check_index_selection()
    print(f'CHARACTER ASSETS PASSED: {files} exact source files, {graphics} graphics, {animations} label-bounded animations, seven main-stage variants. Altered-byte, adjacent-animation, and empty-animation controls rejected.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.output)
