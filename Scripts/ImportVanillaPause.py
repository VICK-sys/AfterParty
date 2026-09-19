import argparse
import hashlib
import json
import shutil
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(source, release, output):
    assets = release / 'assets'
    files = {
        'bold.png': 'images/fonts/bold.png',
        'bold.xml': 'images/fonts/bold.xml',
        'breakfast.ogg': 'shared/music/breakfast/breakfast.ogg',
        'scrollMenu.ogg': 'sounds/scrollMenu.ogg',
    }
    pack = json.loads((assets / 'data/stickerpacks/default.json').read_text(encoding='utf-8-sig'))
    for sticker in pack['stickers']:
        files['stickers/' + Path(sticker).name + '.png'] = 'shared/images/' + sticker + '.png'
    for character in ['bf', 'pico']:
        pack_path = f'data/stickerpacks/standard-{character}.json'
        pack = json.loads((assets / pack_path).read_text(encoding='utf-8-sig'))
        files[f'stickerPacks/{character}.json'] = pack_path
        for sticker in pack['stickers']:
            files[f'stickerPacks/{character}/' + Path(sticker).name + '.png'] = 'shared/images/' + sticker + '.png'
    for sound in (assets / 'shared/sounds/stickersounds/keys').glob('*.ogg'):
        files['stickerSounds/' + sound.name] = sound.relative_to(assets).as_posix()
    output.mkdir(parents=True, exist_ok=True)
    for target, original in files.items():
        destination = output / target
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(assets / original, destination)
    for name in ['vcr32.png', 'vcr32.json', 'LICENSE.md', 'Source-LICENSE.md']:
        shutil.copy2(output.parent / 'VanillaStory' / name, output / name)
    references = ['play/PauseSubState.hx', 'ui/AtlasText.hx', 'ui/transition/stickers/StickerSubState.hx']
    manifest = {
        'source': 'Funkin-0.8.6',
        'sourceFiles': {name: digest(source / 'source/funkin' / name) for name in references},
        'releaseFiles': {name: {'source': original, 'sha256': digest(assets / original)} for name, original in files.items()},
        'bitmapFonts': {path.name: digest(path) for path in sorted(output.glob('vcr*')) if path.suffix in ('.png', '.json')},
    }
    (output / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(files)} pause menu assets.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('release', type=Path)
    parser.add_argument('--output', type=Path, default=Path('Assets/Resources/FunkinPause'))
    args = parser.parse_args()
    run(args.source, args.release, args.output)
