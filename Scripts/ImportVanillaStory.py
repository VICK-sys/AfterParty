import argparse
import hashlib
import json
import shutil
from pathlib import Path


LEVELS = ['tutorial', 'week1', 'week2', 'week3', 'week4', 'week5', 'week6', 'week7', 'weekend1', 'sserafim']


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(source, release, output):
    assets = release / 'assets'
    output.mkdir(parents=True, exist_ok=True)
    hashes = {}
    for path in (assets / 'images/storymenu').rglob('*'):
        if not path.is_file():
            continue
        relative = path.relative_to(assets / 'images')
        destination = output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
        hashes[relative.as_posix()] = digest(path)
    levels = []
    for level_id in LEVELS:
        path = assets / f'data/levels/{level_id}.json'
        level = json.loads(path.read_text(encoding='utf-8-sig'))
        hashes[f'data/levels/{level_id}.json'] = digest(path)
        level['id'] = level_id
        level['songNames'] = []
        difficulties = {'easy', 'normal', 'hard'}
        for song_id in level['songs']:
            path = assets / f'data/songs/{song_id}/{song_id}-metadata.json'
            metadata = json.loads(path.read_text(encoding='utf-8-sig'))
            hashes[f'data/songs/{song_id}/{song_id}-metadata.json'] = digest(path)
            level['songNames'].append(metadata['songName'])
            difficulties.intersection_update(metadata['playData']['difficulties'])
        level['difficulties'] = [name for name in ['easy', 'normal', 'hard'] if name in difficulties] or ['normal']
        levels.append(level)
    (output / 'levels.json').write_text(json.dumps(levels, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    references = ['ui/story/StoryMenuState.hx', 'ui/story/Level.hx', 'ui/story/LevelTitle.hx',
                  'ui/story/LevelProp.hx', 'play/stage/Bopper.hx', 'util/MathUtil.hx']
    manifest = {
        'version': '0.8.6',
        'sourceFiles': {name: digest(source / 'source/funkin' / name) for name in references},
        'releaseFiles': hashes,
        'levelScripts': {name: digest(assets / f'scripts/levels/{name}.hxc') for name in ['weekend1', 'sserafim']},
        'levelsSha256': digest(output / 'levels.json'),
        'menuBpm': json.loads((assets / 'music/freakyMenu/freakyMenu-metadata.json').read_text())['timeChanges'][0]['bpm']
    }
    if (output / 'vcr32.png').is_file():
        manifest['bitmapFont'] = {name: digest(output / name) for name in ['vcr32.png', 'vcr32.json']}
        manifest['fontSourceSha256'] = digest(output.parent / 'VanillaFreeplay/vcr.ttf')
    (output / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(levels)} Story Mode pages and {len(hashes)} asset references.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('release', type=Path)
    parser.add_argument('--output', type=Path, default=Path('Assets/Resources/VanillaStory'))
    args = parser.parse_args()
    run(args.source, args.release, args.output)
