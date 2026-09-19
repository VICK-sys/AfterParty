import argparse
import hashlib
import json
from pathlib import Path

from ImportVanillaSongs import read


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def notes(chart):
    return sorted((note[0], note[1] if section['mustHitSection'] else (note[1] + 4) % 8, note[2])
                  for section in chart['song']['notes'] for note in section['sectionNotes'])


def run(assets, audio, source, root):
    song = root / '09-Sserafim/01-Spaghetti'
    chart = read(assets / 'data/songs/spaghetti/spaghetti-chart.json')
    assert digest(song / 'Source/chart.json') == digest(assets / 'data/songs/spaghetti/spaghetti-chart.json')
    assert digest(song / 'Source/metadata.json') == digest(assets / 'data/songs/spaghetti/spaghetti-metadata.json')
    assert digest(song / 'Inst.ogg') == digest(audio / 'Inst.ogg')
    assert digest(song / 'Voices.ogg') == digest(audio / 'Voices-sserafim-sakura.ogg')
    for difficulty, count in [('easy', 435), ('normal', 559), ('hard', 707)]:
        converted = read(song / f'Chart-{difficulty}.json')
        expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in chart['notes'][difficulty])
        assert len(expected) == count and notes(converted) == expected
        assert converted['song']['speed'] == chart['scrollSpeed'][difficulty]
        assert [(t, (d + 4) % 8, length) for t, d, length in notes(converted)] != expected
        assert read(song / 'Vanilla.json')['noteKinds'][difficulty] == [n for n in chart['notes'][difficulty] if n.get('k')]
    assert read(song / 'Vanilla.json')['events'] == chart['events']
    content = root / 'SpaghettiAssets'
    for target, entry in read(content / 'source-manifest.json').items():
        original = source / entry['source'].removeprefix('Funkin-0.8.6/') if entry['source'].startswith('Funkin-0.8.6/') else assets / entry['source']
        assert digest(content / target) == entry['sha256'] == digest(original), target
    for path in content.rglob('graphic.json'):
        graphic = read(path)
        for animation in graphic['animations'].values():
            assert animation['frames'] and all(0 <= index < len(graphic['frames']) for index in animation['frames']), path
        assert all((path.parent / quad['image']).is_file() for frame in graphic['frames'] for quad in frame), path
    for name in ['yunjin', 'kazuha', 'chaewon', 'eunchae', 'sakura']:
        folder = content / 'characters' / ('sserafim-' + name)
        graphic = read(folder / 'graphic.json')
        assert len(graphic['attachments']) == len(graphic['frames']) and any(graphic['attachments'])
        assert 'idle' in read(folder / 'lipsync.json')['poses']
    for name in ['sserafim-lipsync', 'sserafim-lipsync-yunjin']:
        assert len(read(content / 'effects' / name / 'graphic.json')['frames']) == 4103
    for folder in ['characters/sserafim-gf', 'characters/sserafim-yunjin',
                   'characters/sserafim-yunjin/foreground', 'effects/cutscene/gfGetUp']:
        path = content / folder
        manifest = read(path / 'masked-frames.json')
        graphic = read(path / 'graphic.json')
        original = path.parent if path.name == 'foreground' else path
        assert manifest['sourceSha256'] == digest(original / 'Animation.json')
        assert manifest['atlasSha256'] == digest(path / 'masked.png')
        assert manifest['frames'] == len(graphic['frames'])
        assert all(quad['image'] == 'masked.png' for frame in graphic['frames'] for quad in frame)
    yunjin = read(content / 'characters/sserafim-yunjin/graphic.json')
    foreground = read(content / 'characters/sserafim-yunjin/foreground/graphic.json')
    assert foreground['bounds'] == yunjin['bounds'] and foreground['animations'] == yunjin['animations']
    for name, animation in foreground['animations'].items():
        frames = [foreground['frames'][index] for index in animation['frames']]
        assert all(bool(frame) == (name == 'idle' or name.startswith('sing')) for frame in frames), name
    assert not (song / 'Chart-erect.json').exists()
    print('SPAGHETTI ASSETS PASSED: 3 charts, 1701 heads, 468 events, unchanged audio and source hashes, atlas indices, masks, lip attachments, and swapped-side control.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--audio', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.audio, args.source, args.root)
