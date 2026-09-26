import argparse
import shutil
from pathlib import Path

import numpy as np
from PIL import Image


parser = argparse.ArgumentParser()
parser.add_argument('reference', type=Path)
args = parser.parse_args()
output = Path(__file__).resolve().parents[1] / 'Assets/Resources/VanillaText'
output.mkdir(parents=True, exist_ok=True)
for name in ['dialogue.png', 'dialogue.json']:
    shutil.copyfile(args.reference / name, output / name)
label = np.asarray(Image.open(args.reference / 'made-in-unity.png').convert('RGBA'), dtype=np.uint16).copy()
label[:, :, :3] = (label[:, :, :3] * label[:, :, 3:] + 127) // 255
Image.fromarray(label.astype('uint8')).save(output / 'made-in-unity.png')
