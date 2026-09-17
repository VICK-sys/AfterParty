# Story Mode

Story Mode uses the desktop level menu from Friday Night Funkin' 0.8.6.
The reference source is the local `Funkin-0.8.6` checkout.
The artwork and level data come from the local Funkin Windows release.

`Scripts/ImportVanillaStory.py` imports the original atlases, titles, difficulty graphics, level definitions, and song names.
Pass the source checkout and Windows release directories to the importer.
`import-manifest.json` records hashes for the source files and imported data.
The screen shares the existing menu music and menu sounds.
`vcr32.png` contains VCR glyphs rendered through Flixel at the original text size.
The text renderer preserves source font metrics and the 29-pixel line spacing.

## Menu behavior

The screen includes Tutorial, Weeks 1 through 7, Weekend 1, and LE SSERAFIM.
The menu preserves original sprite coordinates, trimmed and rotated atlas frames, indexed dances, animation offsets, and character reuse.
Characters dance with the 102 BPM menu music.
Level titles use the source spacing, scrolling curve, opacity, and confirmation colors.
Difficulty changes use the source placement and 0.07-second fade.
Background colors change over 0.9 seconds with the source easing curve.
Confirmation locks input for one second, then fades to black over 0.2 seconds.
The main menu flashing setting also controls the selected level flash.

Use Up and Down or W and S to select a level.
Use Left and Right or A and D to select a difficulty.
Use the mouse wheel to change levels.
Use Home and End to select the first and last levels.
Use Enter or Space to start the level.
Use Escape or Backspace to return to the main menu.
A controller uses its navigation axes, primary button, and secondary button.
The menu restores the selected level and difficulty after gameplay.

## Installed content

Tutorial and Week 1 launch their installed songs in the original order.
The other level pages retain their original artwork and track names.
Selecting a level with missing songs displays the missing song names.
The menu does not import additional gameplay content.
Freeplay retains access to local bundles.

Story Mode offers Easy, Normal, and Hard according to each level definition.
Each campaign uses the player mode and requires every song at the selected difficulty.
The level score totals completed songs from one campaign.
Only a completed campaign can update its best score.
Quitting or leaving game over returns to Story Mode without completing the campaign.
Restarting a song preserves the campaign position and previous song scores.
Weekend 1 hides Blazin' until a completed campaign has a positive score.

## Validation

Run validation in a separate project copy with graphics enabled.
Set `UNITY_PARTY_STORY_TEST_PATH` to the output directory.
Use Unity batch mode with `-executeMethod VanillaStoryValidation.Begin`.
Do not add `-quit`. The probe exits after its checks.
Set `UNITY_PARTY_BUILD_PATH` to build a Windows player after successful checks.

The probe checks playlists, missing content, atlas rotation, animation frames, navigation, confirmation locks, gameplay launch, return state, and scores.
Abort, autoplay, wrong-song, missing-difficulty, and blank-render controls detect false passes.
The probe captures each level, confirmation, return, and alternate aspect ratios.
Run `python Scripts/TestVanillaStoryAssets.py` to check imported hashes, animation definitions, atlas bounds, and font metrics.

`Scripts/StoryFont` regenerates the bitmap font with Haxe, Flixel 6.2.0, OpenFL 9.5.2, and Lime 8.3.2.
Build its project with `haxelib run lime build project.xml neko -64` from that directory.
Set `UNITY_PARTY_STORY_FONT_PATH` to this asset directory before running the exported executable.
The screen retains a 1280 by 720 viewport with letterboxing at other aspect ratios.
Gameplay scenes and loading screens retain Unity Party behavior.

The artwork and fonts retain their upstream ownership.
`LICENSE.md` contains the asset license.
`Source-LICENSE.md` contains the source license and copyright notice.
