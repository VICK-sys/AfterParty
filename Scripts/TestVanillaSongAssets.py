import argparse
from array import array
import hashlib
import json
import math
from pathlib import Path
import subprocess


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def decode(ffmpeg, paths, filters=None):
    command = [ffmpeg, '-hide_banner', '-loglevel', 'error']
    for path in paths:
        command += ['-i', str(path)]
    if filters:
        command += ['-filter_complex', filters.replace('[out]', ',asetpts=N/SR/TB[out]'), '-map', '[out]']
    else:
        command += ['-af', 'asetpts=N/SR/TB']
    command += ['-ac', '1', '-ar', '44100', '-f', 'f32le', 'pipe:1']
    samples = array('f')
    samples.frombytes(subprocess.run(command, check=True, stdout=subprocess.PIPE).stdout)
    return samples


def error(actual, expected):
    count = min(len(actual), len(expected))
    return sum((actual[index] - expected[index]) ** 2 for index in range(0, count, 31))


def run(assets, output, ffmpeg):
    data = assets / 'preload/data'
    if not data.is_dir():
        data = assets / 'data'
    total = 0
    for song_id, directory in [('bopeebo', '01-Bopeebo'), ('fresh', '02-Fresh'), ('dadbattle', '03-DadBattle')]:
        target = output / '01-Week1' / directory
        source = data / 'songs' / song_id
        metadata = read(source / f'{song_id}-metadata-erect.json')
        chart = read(source / f'{song_id}-chart-erect.json')
        assert digest(source / f'{song_id}-chart-erect.json') == digest(target / 'Source/chart-erect.json')
        assert digest(source / f'{song_id}-metadata-erect.json') == digest(target / 'Source/metadata-erect.json')
        assert digest(assets / 'songs' / song_id / 'Inst-erect.ogg') == digest(target / 'Inst-erect.ogg')
        assert digest(target / 'Inst-erect.ogg') != digest(target / 'Inst.ogg')
        assert read(target / 'Vanilla-erect.json')['events'] == chart['events']
        picker = read(target / 'meta.json')
        assert list(picker['Song Difficulties']) == ['Easy', 'Normal', 'Hard', 'Erect', 'Nightmare']
        for difficulty in ['erect', 'nightmare']:
            converted = read(target / f'Chart-{difficulty}.json')['song']
            expected = sorted((n['t'], n['d'], n.get('l', 0)) for n in chart['notes'][difficulty])
            actual = sorted((n[0], n[1] if section['mustHitSection'] else (n[1] + 4) % 8, n[2])
                            for section in converted['notes'] for n in section['sectionNotes'])
            assert actual == expected
            assert converted['bpm'] == metadata['timeChanges'][0]['bpm']
            assert converted['speed'] == chart['scrollSpeed'][difficulty]
            assert picker['Song Variations'][difficulty.title()]['Asset Suffix'] == '-erect'
            assert picker['Song Variations'][difficulty.title()]['Song Credits']['Composer'] == metadata['artist']
            total += len(actual)
        vocals = assets / 'songs' / song_id
        paths = [vocals / 'Voices-bf-erect.ogg', vocals / 'Voices-dad-erect.ogg']
        offset = metadata.get('offsets', {}).get('vocals', {}).get('dad', 0)
        adjustment = f'atrim=start={-offset / 1000},asetpts=PTS-STARTPTS' if offset < 0 else f'adelay={offset}:all=1'
        expected = decode(ffmpeg, paths, f'[1:a]{adjustment}[dad];[0:a][dad]amix=inputs=2:normalize=0[out]')
        actual = decode(ffmpeg, [target / 'Voices-erect.ogg'])
        assert abs(len(actual) - len(expected)) < 100
        noise = error(actual, expected)
        energy = sum(sample * sample for sample in expected[::31])
        snr = 10 * math.log10(energy / noise)
        assert snr > 30, f'{song_id}: mixed vocal SNR {snr:.2f} dB'
        if offset:
            wrong = decode(ffmpeg, paths, f'[1:a]adelay={-offset}:all=1[dad];[0:a][dad]amix=inputs=2:normalize=0[out]')
            assert error(actual, wrong) > noise * 10, 'Reversed vocal offset control passed.'
        print(f'{song_id}: exact source data and instrumental, vocals {snr:.2f} dB SNR')
    stage = output / 'Stages/mainStageErect'
    assert digest(stage / 'stage.json') == digest(data / 'stages/mainStageErect.json')
    for path in stage.iterdir():
        if path.suffix in ['.png', '.xml']:
            assert digest(path) == digest(assets / 'week1/images/erect' / path.name)
    assert total == 3926
    print('VANILLA ASSETS PASSED: six remix charts, 3,926 heads, source hashes, audio alignment, stage assets, and negative controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/StreamingAssets/Bundles')
    parser.add_argument('--ffmpeg', default='ffmpeg')
    args = parser.parse_args()
    run(args.assets, args.output, args.ffmpeg)
