import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def pixels(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)


def difference(reference, actual):
    background = reference[0, 0]
    mask = np.any(abs(reference - background) > 2, axis=2)
    mask |= np.any(abs(actual - background) > 2, axis=2)
    error = abs(reference - actual)
    return {
        "mean_channel_error": float(error[mask].mean()) if mask.any() else 0,
        "maximum_channel_error": int(error.max()),
        "foreground_pixels": int(mask.sum()),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", type=Path, default=Path("Builds/FreeplayReference"))
    parser.add_argument("--actual", type=Path, default=Path("Builds/FreeplayParity"))
    args = parser.parse_args()
    cases = json.loads((args.reference / "cases.json").read_text())
    result = {"cases": []}
    for index, case in enumerate(cases):
        reference = pixels(args.reference / f"title-{index}.png")
        actual = pixels(args.actual / f"title-{index}.png")
        result["cases"].append({**case, **difference(reference, actual)})
    reference = pixels(args.reference / "title-0.png")
    result["controls"] = {
        name: difference(reference, pixels(args.actual / f"title-{name}-control.png"))
        for name in ("flat", "blank")
    }
    result["controls"]["shifted"] = difference(reference, np.roll(pixels(args.actual / "title-0.png"), 2, axis=1))
    result["passed"] = all(case["mean_channel_error"] < .5 and case["maximum_channel_error"] <= 3 for case in result["cases"])
    result["passed"] &= all(control["mean_channel_error"] > 3 for control in result["controls"].values())
    (args.actual / "title-comparison.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
