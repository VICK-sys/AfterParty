import argparse
import json
from pathlib import Path

from ImportVanillaWeeks456 import import_assets


def run(assets, output):
    data = assets / 'preload/data'
    if not data.is_dir():
        data = assets / 'data'
    import_assets(assets, data, output, {1: (['mainStage', 'mainStageErect'], ['bf', 'dad', 'gf'])})
    for path in sorted(output.glob('0[01]-*/*/Vanilla*.json')):
        sidecar = json.loads(path.read_text(encoding='utf-8'))
        suffix = '-erect' if sidecar.get('variation') == 'erect' else ''
        metadata = json.loads((data / f'songs/{sidecar["song"]}/{sidecar["song"]}-metadata{suffix}.json').read_text(encoding='utf-8-sig'))
        sidecar['characters'] = metadata['playData']['characters']
        sidecar['stage'] = metadata['playData']['stage']
        sidecar['timeChanges'] = metadata['timeChanges']
        path.write_text(json.dumps(sidecar, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    args = parser.parse_args()
    run(args.assets, args.output)
