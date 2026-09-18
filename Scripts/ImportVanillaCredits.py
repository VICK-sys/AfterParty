import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(reference, source, executable, target):
    root = Path(__file__).resolve().parents[1]
    original_path = target / 'credits.json'
    if not original_path.exists():
        original_path = root / 'Assets/VanillaMenu/credits.json'
    original = json.loads(original_path.read_text(encoding='utf-8-sig'))
    binary = executable.read_bytes()
    for entry in original['entries']:
        for text in [entry.get('header', '')] + [member['line'] for member in entry.get('body', [])]:
            if text and text.encode('utf-8') not in binary and text.encode('utf-16le') not in binary:
                raise ValueError(f'Credit is absent from the reference executable: {text}')
    data = json.loads((reference / 'raw/lines.json').read_text(encoding='utf-8'))
    atlas = Image.new('RGBA', (2048, 2048))
    x = y = row_height = page = 0
    page_files = []

    def save_page():
        name = f'credits-{page}.png'
        atlas.save(target / name)
        page_files.append(name)

    target.mkdir(parents=True, exist_ok=True)
    for line in data['lines']:
        for variant in line['variants']:
            image = Image.open(reference / 'raw' / variant.pop('file')).convert('RGBA')
            if x + image.width + 4 > atlas.width:
                x = 0
                y += row_height
                row_height = 0
            if y + image.height + 4 > atlas.height:
                save_page()
                page += 1
                atlas = Image.new('RGBA', (2048, 2048))
                x = y = row_height = 0
            atlas.paste(image, (x + 2, y + 2))
            variant['fullHeight'] = variant.pop('height')
            variant.update(atlas=f'credits-{page}', x=x + 2, y=y + 2, width=image.width, height=image.height)
            x += image.width + 4
            row_height = max(row_height, image.height + 4)
    save_page()
    for name in ['fade-cover.png', 'fade-reveal.png']:
        (target / name).write_bytes((reference / 'raw' / name).read_bytes())
        page_files.append(name)
    (target / 'lines.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (target / 'credits.json').write_bytes(original_path.read_bytes())
    (target / 'Source-LICENSE.md').write_bytes((source / 'LICENSE.md').read_bytes())
    (target / 'LICENSE.md').write_bytes((root / 'Assets/VanillaMenu/LICENSE.md').read_bytes())
    files = ['credits.json', 'unity-party.json', 'lines.json'] + page_files
    manifest = {
        'source': 'Friday Night Funkin\' 0.8.6 Windows credits',
        'sourceCodeSHA256': digest(source / 'source/funkin/ui/credits/CreditsState.hx'),
        'originalEntries': len(original['entries']),
        'lineCount': len(data['lines']),
        'minimumWidth': 1280,
        'maximumWidth': 1600,
        'files': {name: digest(target / name) for name in files},
    }
    (target / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f"Imported {len(data['lines'])} credit lines and two transition gradients. All original names verified against Funkin.exe.")


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, default=Path('Builds/CreditsReference'))
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--target', type=Path, default=Path('Assets/Resources/VanillaCredits'))
    args = parser.parse_args()
    run(args.reference, args.source, args.executable, args.target)
