import argparse
import hashlib
import json
import shutil
from pathlib import Path

from ImportVanillaFreeplay import normalize


def run(source, assets, output):
    files = {}

    def copy(path, destination):
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
        if path.name == 'Animation.json':
            destination.write_text(json.dumps(normalize(json.loads(path.read_text(encoding='utf-8-sig')))), encoding='utf-8')
        files[destination.relative_to(output).as_posix()] = {
            'source': str(path),
            'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'sha256': hashlib.sha256(destination.read_bytes()).hexdigest(),
        }

    for path in (assets / 'shared/images/resultScreen').rglob('*'):
        if path.is_file():
            copy(path, output / 'images' / path.relative_to(assets / 'shared/images/resultScreen'))
    for path in (assets / 'shared/music').glob('results*/*'):
        if path.suffix == '.ogg':
            copy(path, output / 'music' / path.name)
    for name in ['bf', 'pico']:
        copy(assets / f'data/players/{name}.json', output / f'players/{name}.json')
    for path in (assets / 'scripts/players/results').rglob('*.hxc'):
        copy(path, output / 'Source' / path.name)
    for name in ['ResultState.hx', 'ResultScore.hx', 'components/ClearPercentCounter.hx', 'components/TallyCounter.hx', 'scoring/Scoring.hx']:
        copy(source / 'source/funkin/play' / name, output / 'Source' / Path(name).name)
    copy(assets / 'shared/sounds/tickleFight.ogg', output / 'sounds/tickleFight.ogg')
    copy(source / 'LICENSE.md', output / 'LICENSE.md')
    (output / 'import-manifest.json').write_text(json.dumps({'source': 'Funkin 0.8.6 source and local Funkin Windows assets', 'files': files}, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(files)} results files.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/Resources/VanillaResults')
    args = parser.parse_args()
    run(args.source, args.assets, args.output)
