# Results screens

BF and Pico results screens use the Friday Night Funkin' 0.8.6 layout, player definitions, animation labels, timing, and music.
Results appear after a completed Freeplay song or the last song in a Story Mode playlist.
Story Mode combines scores and note tallies across the playlist.

The port includes all six ranks, Pico's alternate GOOD animations, and BF's safe and naughty PERFECT animations.
The default uses the naughty variant, as the source does.
The screen shows animated tallies, clear percentage, score digits, difficulty, song title, and the highscore marker.
Character music uses the source intro and loop files.
Confirmation uses character stickers or the existing Freeplay rank animation when the rank improves.

Practice and AutoPlay do not save scores or ranks.
Aborted songs do not show results or save completion records.
Results restore the song, difficulty, mode, and character in Freeplay.

## Assets

The source checkout provides the results logic and license.
The local Windows asset distribution provides the textures, atlases, audio, and player definitions.
`import-manifest.json` records source and output hashes.
`Source` retains the reference implementations.

Animate timelines retain their labels, nested transforms, frame ranges, and original bounds.
The import bakes Pico's blur filters and BF's clipped rainbow text through HaxeFlixel.
The shared renderer reads these additions without changing other atlases.

## Import

Run these commands from the project directory.
Replace the two source paths with the local checkout and asset directory.

```powershell
python Scripts/ImportVanillaResults.py --source <Funkin-0.8.6> --assets <funkin-windows-64bit/assets>
& Scripts/ResultsReference/Build.ps1
python Scripts/ImportResultsFilters.py
python Scripts/ImportResultsMasks.py
python Scripts/TestResultsAssets.py
```

The reference harness requires HaxeFlixel 6.2.0, flixel-animate 1.4.0, Lime, OpenFL, and Neko.
The Python tools require Pillow and NumPy.

## Validation

Run the Unity probes in an isolated project with graphics enabled.
Use `-batchmode -executeMethod VanillaResultsValidation.Begin` for layout, rank, animation, audio handoff, and cleanup checks.
Use `-batchmode -executeMethod VanillaResultsLifecycleValidation.Begin` for gameplay completion and menu return checks.
Both probes exit Unity after completion.
Both use separate validation save identities.

Set `UNITY_PARTY_RESULTS_TEST_PATH` to choose the output directory.
The default directories are `Temp/Results` and `Temp/ResultsLifecycle`.

The lifecycle probe covers BF, Pico Practice, campaign totals, aborted songs, AutoPlay, and legacy song selection.
It checks the improved-rank animation and character selection after returning to Freeplay.

Run `python Scripts/TestResultsRendering.py` after the reference harness and results probe.
It compares 17 character captures across every rank, the alternate animations, and flash frames.
Each comparison requires mean RGB channel error below 0.25 on a 0 to 255 scale.
Blank and shifted images must fail that threshold.
The asset probe verifies hashes, atlas bounds, animation labels, music files, and a missing-file control.

These checks sample animation frames.
They do not establish pixel identity for every frame or verify sound through physical speakers.
Mobile haptics and source platform services are not included.
