import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def run(source, release, output, streaming, ffmpeg):
    assets = release / 'assets'
    files = {name: 'images/' + name for name in ['menuBG.png', 'checkboxThingie.png', 'checkboxThingie.xml', 'funkay.png']}
    for font in ['bold', 'default']:
        for ext in ['png', 'xml']:
            files[f'{font}.{ext}'] = f'images/fonts/{font}.{ext}'
    for name in ['latencyArrow', 'latencyReceptor']:
        files[name + '.png'] = 'shared/images/' + name + '.png'
    for name in ['offsetsLoop', 'drumsLoop']:
        files[name + '.ogg'] = 'music/offsetsLoop/' + name + '.ogg'
    for name in ['scrollMenu', 'confirmMenu', 'cancelMenu']:
        files[name + '.ogg'] = 'sounds/' + name + '.ogg'
    for name in ['volumebox'] + [f'bars_{index}' for index in range(1, 11)]:
        files[f'soundtray/{name}.png'] = f'images/soundtray/{name}.png'
    for name in ['Volup', 'Voldown', 'VolMAX']:
        files[f'soundtray/{name}.ogg'] = f'sounds/soundtray/{name}.ogg'
    output.mkdir(parents=True, exist_ok=True)
    manifest = {'source': 'Funkin-0.8.6', 'assets': {}, 'sourceFiles': {}}
    for name, original in files.items():
        (output / name).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(assets / original, output / name)
        manifest['assets'][name] = {'source': original, 'sha256': hashlib.sha256((output / name).read_bytes()).hexdigest()}
    for path in sorted((source / 'source/funkin/ui/options').rglob('*.hx')):
        manifest['sourceFiles'][path.relative_to(source).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    streaming.mkdir(parents=True, exist_ok=True)
    manifest['censoredVideos'] = {}
    for name, original in [('stress', 'stressCutscene-censored'), ('stress-pico', 'stressPicoCutscene-censored')]:
        video = assets / 'videos/videos' / (original + '.mkv')
        for mapping, codec, suffix in [('0:v:0', ['-c:v', 'copy', '-an'], '.mp4'), ('0:a:0', ['-c:a', 'pcm_f32le'], '.wav'), ('0:s:0', [], '.srt')]:
            destination = streaming / (name + '-censored' + suffix)
            subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(video), '-map', mapping, *codec, str(destination)], check=True)
            manifest['censoredVideos'][destination.name] = hashlib.sha256(destination.read_bytes()).hexdigest()
    shutil.copy2(source / 'LICENSE.md', output / 'Source-LICENSE.md')
    shutil.copy2(release / 'LICENSE.md', output / 'LICENSE.md')
    (output / 'import-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(files)} options assets.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('release', type=Path)
    parser.add_argument('--output', type=Path, default=Path('Assets/Resources/VanillaOptions'))
    parser.add_argument('--streaming', type=Path, default=Path('Assets/StreamingAssets/VanillaOptions'))
    parser.add_argument('--ffmpeg', default='ffmpeg')
    args = parser.parse_args()
    run(args.source, args.release, args.output, args.streaming, args.ffmpeg)
