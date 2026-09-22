# AfterParty pause menu

The AfterParty pause menu adapts the desktop menu from Friday Night Funkin' 0.8.6.
The source references are `PauseSubState.hx`, `AtlasText.hx`, and `StickerSubState.hx`.
The artwork and audio come from the local Funkin Windows distribution.
The import manifest records source and asset hashes.

## Appearance

The menu uses a 1280 by 720 viewport.
The original bold atlas animates at 24 FPS.
Each letter retains the source dimensions, baseline, trim, and spacing.
The selected row moves to X 90 and Y 345.6.
Each following row adds 26 pixels horizontally and 156 pixels vertically.
Selection movement uses a 0.33-second quartic ease.
Unselected rows use 60 percent opacity.
The black background reaches 60 percent opacity over 0.8 seconds.

Metadata uses the original VCR font at 32 pixels.
Each line fades in over 1.8 seconds and moves down five pixels.
The stagger starts at 0.1 seconds and increases by 0.1 seconds per line.
Artist and charter alternate after 15 seconds, with 0.75-second fades.
Offset text uses the same font at 16 pixels.

Breakfast starts at a random millisecond within the first half of the track.
Its volume reaches 0.75 over five seconds, subject to the menu volume setting.
Navigation plays the original scroll sound at volume 0.4.
Exit uses the original Boyfriend stickers and keyboard sounds.
Stickers appear over 0.9 seconds and disappear after the destination menu loads.

## Controls and gameplay

Press the configured pause key, Escape, or controller Start to pause.
Use Up and Down, W and S, or the controller stick to select an entry.
Press Enter, Space, or the primary controller button to select it.
Press Escape or controller Start to resume from either menu.
Hold Shift with Up or Down to change the global offset.
Offset adjustment starts at one millisecond and repeats at 30 milliseconds per second after 0.5 seconds.
The offset range is -1500 through 1500 milliseconds.

Resume restores the countdown, song clocks, audio, and gameplay time scale.
Restart resets the current song in place and retains practice mode and death count.
The song, stage, and audio clips stay loaded.
The reset clears scores, health changes, held inputs, note effects, chart events, and camera transitions.
Outgoing notes and holds move one viewport height over 0.5 seconds with exponential easing.
Receptors stay in place.
The five-beat countdown starts after that delay.
Notes present at the countdown start enter from a 200-pixel offset over 0.5 seconds.
Downscroll reverses both note animations.
Pausing freezes the restart delay and countdown.
Restarting during the countdown cancels its previous sounds and graphics.
Game-over retry skips the outgoing note animation.
Confirmation waits for the confirmation clip duration divided by seven, then fades to black over two seconds.
Gameplay returns through a one-second fade.
Pixel songs use the source stepped color subtraction.
Change Difficulty lists installed charts from the current variation.
Changing difficulty resets accumulated campaign score and retains the campaign position.
Practice mode prevents death and excludes the run from saved scores and ranks.
Exit returns to the originating Story Mode or Freeplay menu.
Cutscene and chart editor pause modes require gameplay systems that this project does not provide.

## Validation

Run `VanillaPauseValidation.Begin` in an isolated Unity batch editor with graphics enabled.
Do not pass `-quit`.
Set `UNITY_PARTY_PAUSE_TEST_PATH` to the output directory.
The probe checks countdown pause, audio samples, camera position, death count, difficulty reload, practice persistence, resume, and menu return.
It checks retained song, stage, and audio objects during retries and difficulty changes.
It checks outgoing heads and holds, incoming offsets, paused restart delays, and countdown cancellation.
Set `UNITY_PARTY_RESTART_DOWNSCROLL=1` to run the same checks with downscroll.
Set `UNITY_PARTY_RESTART_SONG=Senpai` to check pixel-song retries.
The pixel fade check compares GPU output against color subtraction, unchanged colors, and black.
It checks campaign score suppression against an eligible campaign control.
It captures standard, difficulty, practice, selected-exit, wide, and tall layouts, with a blank-render control.
Run `python Scripts/TestPauseAssets.py` to verify imported hashes, glyph coverage, atlas bounds, and font metrics.

Run `Scripts/PauseReference/Build.ps1 -Source <Funkin source directory>` to render the original AtlasText implementation with Flixel.
The reference capture includes metadata and offset text from the source layout.
The validated standard capture differed from the Flixel reference by at most one value per 8-bit color channel.
`Scripts/PauseFont` exports the 16-pixel font with Flixel 6.2.0, OpenFL 9.5.2, and Lime 8.3.2.
The 32-pixel font uses the existing Story Mode export.

The artwork, fonts, and audio retain their upstream ownership.
`LICENSE.md` preserves the asset license.
`Source-LICENSE.md` preserves the source license.
