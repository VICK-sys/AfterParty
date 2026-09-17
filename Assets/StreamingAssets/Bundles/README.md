# Tutorial and Week 1

These bundles contain the original Tutorial, Bopeebo, Fresh, and DadBattle charts from Friday Night Funkin' 0.8.6.
Each song includes Easy, Normal, and Hard.
Bopeebo, Fresh, and DadBattle also include Erect and Nightmare.
The song picker lists Tutorial before Week 1.
Week 1 lists Bopeebo, Fresh, and DadBattle in that order.

Built-in bundles load from `Assets/StreamingAssets/Bundles` in the editor and from StreamingAssets in Windows builds.
User bundles continue to load from the application data directory.
The importer does not copy files into user bundles or replace them.

The source reference is `FunkinCrew/Funkin`, tag `v0.8.6`.
The asset reference is `FunkinCrew/funkin.assets`, commit `9300719cb261d23f72344807055be6d109c9949c`.
`Source/chart.json` and `Source/metadata.json` preserve the source data for each song.
`Source/chart-erect.json` and `Source/metadata-erect.json` preserve the remix source files without changes.
`vanilla-import.json` records the revision, note counts, and instrumental hashes.
The instrumentals are unchanged.
The importer mixes the separate vocal stems at their original gain into one Vorbis file at quality 8.
DadBattle Erect advances the Dad vocal stem by 8 milliseconds, as specified by the source metadata.
Erect and Nightmare share remix audio and events.
Each difficulty retains its source note timings, lanes, sustain lengths, tempo, and fractional scroll speed.
The difficulty selector updates the preview, song title, and credits when the variation changes.

`VanillaSongPlayback` processes camera focus, camera zoom, character animation, and scroll-speed events from `Vanilla.json`.
The original difficulties use the existing Unity stage and character artwork.
The remixes use the source Erect stage artwork, crowd animation, parallax, additive lights, and character color adjustments.
The remixes support Girlfriend camera focus, camera offsets, eased movement, stage-relative zoom, and camera bop events.
Character sprites and their animations remain the existing Unity assets.
These sprites differ from the upstream Animate atlases, so character animation frames do not match exactly.
Tutorial adds a singing Girlfriend using the existing sprites and the original portrait.
Bopeebo and Tutorial include the Boyfriend greeting animation.
Bundled songs start after the countdown.
Custom songs keep the existing start-key behavior.

Pico variations are not included.
The Story Mode entry still uses the bundle picker.
The bundles do not add a sequential story campaign.

## Rebuild the bundles

Install Python and FFmpeg.
Obtain the pinned `funkin.assets` revision.
Run `python Scripts/ImportVanillaSongs.py --assets "path/to/funkin.assets"` from the project directory.
The importer also accepts the `assets` directory from the matching 0.8.6 Windows build.
Add `--erect-only` to extend existing bundles without regenerating their original audio and charts.
The importer overwrites these generated bundles.
Select `Tools > Unity Party > Build Tutorial Character` to regenerate the character animation assets.

## Validation

Set `UNITY_PARTY_SONG_TEST_PATH` to an empty output directory.
Set `UNITY_PARTY_BUILD_PATH` to the output executable path.
Run Unity with `-batchmode -executeMethod VanillaSongValidation.Begin`.
The probe checks all 18 charts through the gameplay parser.
It rejects controls for swapped note sides, rounded scroll speeds, and incorrect linear camera easing.
It completes all six remix charts and each original Hard chart in autoplay.
It checks note counts, misses, audio selection, opponents, camera events, stage loading, and menu return.
It saves a screenshot for each run.
The probe builds Windows after the gameplay checks pass.

Run `Scripts/TestFunkinRules.ps1` to simulate all 5,801 note heads at 30, 60, and 144 frames per second.
Run `python Scripts/TestVanillaSongAssets.py --assets "path/to/assets"` to compare the imported remixes with their source files.
The asset check verifies chart data, audio hashes, vocal alignment, stage hashes, and a reversed-offset control.

The content remains subject to `Vanilla-LICENSE.md`.
The upstream asset license restricts public redistribution.
