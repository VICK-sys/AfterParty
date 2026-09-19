import argparse
import hashlib
import json
import shutil
from pathlib import Path

from ImportVanillaFreeplay import normalize


def run(assets, source, output):
    manifest = {}

    def copy(path, target):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
        manifest[target.relative_to(output).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()

    for path in (assets / 'images/charSelect').rglob('*'):
        if path.is_file():
            target = output / 'charSelect' / path.relative_to(assets / 'images/charSelect')
            copy(path, target)
            if path.name == 'Animation.json':
                target.write_text(json.dumps(normalize(json.loads(path.read_text(encoding='utf-8-sig')))), encoding='utf-8')
    for path in (assets / 'sounds').glob('CS*.ogg'):
        copy(path, output / 'audio/charSelect' / path.name)
    copy(assets / 'sounds/static loop.ogg', output / 'audio/charSelect/static loop.ogg')
    copy(assets / 'music/stayFunky/stayFunky.ogg', output / 'audio/charSelect/stayFunky.ogg')
    for suffix in ['.png', '.xml']:
        copy(assets / ('images/digital_numbers_pico' + suffix), output / ('digital_numbers_pico' + suffix))
    for name in ['bf', 'pico']:
        copy(assets / f'data/players/{name}.json', output / f'charSelect/{name}.json')
    for path in (source / 'source/funkin/ui/charSelect').glob('*.hx'):
        copy(path, output / 'Source/charSelect' / path.name)
    video = output.parents[1] / 'StreamingAssets/CharacterSelect'
    video.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(assets / 'videos/videos/introSelect.mp4', video / 'introSelect.mp4')
    (output / 'charSelect-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/Resources/VanillaFreeplay')
    args = parser.parse_args()
    run(args.assets, args.source, args.output)
