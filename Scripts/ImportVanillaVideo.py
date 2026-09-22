import json
import shutil
import subprocess
import tempfile
from pathlib import Path


def import_video(source, target, ffmpeg='ffmpeg', keep_audio=False):
    source = Path(source)
    target = Path(target)
    executable = Path(ffmpeg)
    ffprobe = str(executable.with_name('ffprobe' + executable.suffix))
    result = subprocess.run([ffprobe, '-v', 'error', '-select_streams', 'v:0',
                             '-show_entries', 'stream=codec_name,pix_fmt,profile,level',
                             '-of', 'json', str(source)], check=True, capture_output=True, text=True)
    stream = json.loads(result.stdout)['streams'][0]
    compatible = (stream['codec_name'] == 'h264' and stream['pix_fmt'] == 'yuv420p'
                  and stream['profile'] in ('Constrained Baseline', 'Baseline', 'Main', 'High')
                  and 0 < stream['level'] <= 41)
    target.parent.mkdir(parents=True, exist_ok=True)
    if compatible and keep_audio:
        if source.resolve() != target.resolve():
            shutil.copy2(source, target)
        return
    encoding = ['-c:v', 'copy'] if compatible else [
        '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p',
        '-profile:v', 'high', '-level:v', '4.1', '-maxrate', '20M', '-bufsize', '25M']
    audio = ['-map', '0:a?', '-c:a', 'copy'] if keep_audio else ['-an']
    with tempfile.NamedTemporaryFile(dir=target.parent, prefix='.' + target.stem + '.', suffix='.mp4', delete=False) as temporary:
        temporary_path = Path(temporary.name)
    try:
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                        '-map', '0:v:0', *encoding, *audio, '-fps_mode', 'passthrough',
                        '-movflags', '+faststart', str(temporary_path)], check=True)
        temporary_path.replace(target)
    finally:
        temporary_path.unlink(missing_ok=True)
