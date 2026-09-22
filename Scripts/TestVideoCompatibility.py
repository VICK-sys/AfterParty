import argparse
import json
import subprocess
from fractions import Fraction
from pathlib import Path


def video_stream(path, count_frames=False):
    arguments = ['-count_frames'] if count_frames else []
    result = subprocess.run(['ffprobe', '-v', 'error', *arguments, '-select_streams', 'v:0',
                             '-show_entries', 'stream=codec_name,pix_fmt,profile,level,width,height,avg_frame_rate,duration,nb_read_frames',
                             '-of', 'json', str(path)], check=True, capture_output=True, text=True)
    return json.loads(result.stdout)['streams'][0]


def check_encoding(path):
    stream = video_stream(path)
    assert stream['codec_name'] == 'h264', (path, stream)
    assert stream['pix_fmt'] == 'yuv420p', (path, stream)
    assert stream['profile'] in ('Constrained Baseline', 'Baseline', 'Main', 'High'), (path, stream)
    assert 0 < stream['level'] <= 41, (path, stream)


def video_timestamps(path):
    result = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
                             '-show_entries', 'packet=pts_time', '-of', 'json', str(path)],
                            check=True, capture_output=True, text=True)
    return sorted(float(packet['pts_time']) for packet in json.loads(result.stdout)['packets'])


def check_video(source, target):
    check_encoding(target)
    original = video_stream(source, count_frames=True)
    converted = video_stream(target, count_frames=True)
    for field in ('width', 'height', 'nb_read_frames'):
        assert original[field] == converted[field], (target, field, original, converted)
    assert int(converted['nb_read_frames']) > 0, target
    frame_duration = 1 / float(Fraction(original['avg_frame_rate']))
    assert abs(float(original['duration']) - float(converted['duration'])) < frame_duration, target
    source_times = video_timestamps(source)
    target_times = video_timestamps(target)
    assert len(source_times) == len(target_times), target
    assert max(abs(a - b) for a, b in zip(source_times, target_times)) < .001, target
    result = subprocess.run(['ffmpeg', '-v', 'info', '-xerror', '-i', str(source), '-i', str(target),
                             '-filter_complex', '[0:v:0]settb=AVTB,setpts=N/(24*TB)[a];[1:v:0]settb=AVTB,setpts=N/(24*TB)[b];[a][b]ssim',
                             '-an', '-fps_mode', 'passthrough', '-f', 'null', '-'], check=True, capture_output=True, text=True)
    similarity = float(result.stderr.rsplit('All:', 1)[1].split()[0])
    assert similarity >= .99, (target, similarity)
    return similarity


def run(root):
    streaming = root / 'Assets/StreamingAssets'
    videos = [path for path in streaming.rglob('*.mp4') if 'Source' not in path.relative_to(streaming).parts]
    assert videos, streaming
    for path in videos:
        check_encoding(path)
    controls = streaming / 'Bundles/Week8Assets/Source/videos'
    for name in ('darnell', '2hot'):
        rejected = False
        try:
            check_encoding(controls / f'{name}.mp4')
        except AssertionError:
            rejected = True
        assert rejected, name
    print(f'VIDEO COMPATIBILITY PASSED: {len(videos)} runtime videos, VP9 and HEVC rejection controls.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    run(args.root)
