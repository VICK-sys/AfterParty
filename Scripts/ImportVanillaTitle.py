import argparse
import hashlib
import json
import shutil
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(source, release, output):
    assets = release / 'assets'
    files = {}
    names = ['logoBumpin.png', 'logoBumpin.xml', 'gfDanceTitle.png', 'gfDanceTitle.xml',
             'newgrounds_logo.png', 'newgrounds_logo_classic.png', 'newgrounds_logo_animated.png',
             'fonts/bold.png', 'fonts/bold.xml', 'title-screen-text/Animation.json',
             'title-screen-text/spritemap1.json', 'title-screen-text/spritemap1.png']
    for name in names:
        files['images/' + name] = output / name
    files['data/introText.txt'] = output / 'introText.txt'
    for name in ['freakyMenu', 'girlfriendsRingtone']:
        files[f'music/{name}/{name}.ogg'] = output / f'audio/{name}.ogg'
        files[f'music/{name}/{name}-metadata.json'] = output / f'audio/{name}-metadata.json'
    files['sounds/confirmMenu.ogg'] = output / 'audio/confirmMenu.ogg'
    videos = output.parents[1] / 'StreamingAssets/VanillaTitle'
    for name in ['riftCollabTrailer', 'mobileRelease', 'boyfriendEverywhere']:
        files[f'videos/videos/{name}.mp4'] = videos / f'{name}.mp4'
    for name, destination in files.items():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(assets / name, destination)
    references = ['ui/title/TitleState.hx', 'ui/title/AttractState.hx', 'ui/AtlasText.hx',
                  'graphics/shaders/ColorSwap.hx']
    manifest = {
        'version': '0.8.6',
        'sourceFiles': {name: digest(source / 'source/funkin' / name) for name in references},
        'assets': {name: {'path': destination.relative_to(output.parents[1]).as_posix(),
                          'sha256': digest(destination)} for name, destination in files.items()}
    }
    (output / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    shutil.copy2(source / 'LICENSE.md', output / 'Source-LICENSE.md')
    shutil.copy2(output.parents[1] / 'VanillaMenu/LICENSE.md', output / 'LICENSE.md')
    print(f'Imported {len(files)} title assets from the local desktop distribution.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('release', type=Path)
    parser.add_argument('--output', type=Path, default=Path('Assets/Resources/VanillaTitle'))
    args = parser.parse_args()
    run(args.source, args.release, args.output)
