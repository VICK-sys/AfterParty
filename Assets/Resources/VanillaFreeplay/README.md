# Friday Fight Funkin' Freeplay

The main menu opens a Unity adaptation of the Freeplay screen from Friday Night Funkin' 0.8.6.
It uses the original artwork, fonts, capsule animations, album animations, letter filters, and BF and Pico DJ timelines.
The menu reads built-in and local bundles.
Story Mode uses the original level order and installed vanilla songs.
Only songs with a chart and instrumental for the selected difficulty appear.

## Controls

- Use Up and Down, or W and S, to select a song.
- Use Left and Right, or A and D, to change difficulty.
- Use Q and E to change the letter or favorites filter.
- Press F to add or remove a favorite.
- Press Home or End to select the first or last entry.
- Press Enter, Space, or Z to play the selected song.
- Press Escape, Backspace, or X to return to the main menu.
- Press Tab to open character select.
- Press M to select a Friday Fight Funkin' play mode.
- Use the mouse wheel to select a song.
- Click a capsule to select it. Click the selected capsule to play it.

Controller axes select songs and difficulties.
The primary button confirms. The secondary button returns. The third button changes favorites.

The Random entry selects a playable song from the current filter and difficulty.
Song previews use the metadata range, or the first 20 percent of the instrumental.
The menu restores the song and difficulty after gameplay.
Entry keeps the main menu visible behind the moving card, backdrop, and capsules until the DJ intro finishes.
The intro completion switches the DJ to idle and reveals the backing card.
UI entrance timing uses the DJ clip duration.
Exit moves the screen elements separately and restores main menu input after 0.5 seconds.
Song confirmation plays the selected icon animation at 10 frames per second, then holds its confirmation pose.
BF confirmation waits one second before a 0.2-second linear fade. Pico waits 1.45 seconds before the same fade.
Capsule titles use the source glyph atlas, software glow, selected blur, and confirmation blending.
Title textures stay cached in each capsule until the menu closes.
Selection changes title, rank, and heart opacity separately.
BF uses the original beat lighting, card flash, and confirmation layers.
Difficulty labels slide over 0.2 seconds. Difficulty dots and filter letters keep their separate animation timing.
Favorite changes animate both heart layers and move the capsule before accepting more input.
Letter filters use alphabetical order. ALL and favorites retain catalog order.
Difficulty changes select the nearest compatible song when the current song lacks the requested chart.
Album artwork and titles switch when the album identifier changes.
SPAGHETTI shows NEW until a standard Boyfriend clear and changes BF background text while selected.
Character select starts its upward transition 0.45 seconds after the DJ animation begins.
Menu elements use separate vertical offsets and a 0.8-second easing curve.
The original gradient rises over the blue-to-black fade while preview music fades over 0.9 seconds.
The screen change waits for the DJ animation and confirmation sound to finish.

The DJ plays the AFK animation after 60 idle seconds, at the end of an idle loop.
BF returns to idle, then watches TV after another 120 idle seconds.
Menu actions reset the idle timer and allow the AFK animation to play again.
Browsing does not interrupt an active AFK or TV animation.
TV playback uses the original remote sounds, cartoon audio, random blinks, and channel changes.
Cartoon audio streams from disk. Song preview volume falls to 15 percent while cartoons play.
Song confirmation and menu exit fade cartoon audio over 0.25 seconds.

## Scores

Friday Fight Funkin' reads existing Unity Party high scores.
Completed Boyfriend runs also save clear percentage and rank.
Clear percentage uses `(Sick + Good - Miss) / chart note count`, clamped between zero and one.
Skipped chart notes count as misses for this display.
Aborted runs and autoplay do not save ranks.
Older scores have no rank until a completed run records one.
Freeplay celebrates the first saved rank and each higher rank when gameplay returns to the menu.
The sequence uses the 0.8.6 badge animations, sparks, rank colors, sounds, camera timing, capsule recoil, and DJ reactions.
The badge lands after 0.6 seconds. The capsule returns after 1.6 seconds. Menu input resumes after 2.2 seconds.
The previous rank remains visible until the new badge lands.
Song previews resume when the menu accepts input.
Equal or lower ranks, aborted runs, autoplay, and Opponent mode do not trigger the sequence.

## Assets and scope

`Scripts/ImportVanillaFreeplay.py` imports assets from the local Funkin Windows release.
The importer extracts four embedded fonts and normalizes the letter animation JSON.
`import-manifest.json` records the release executable hash and imported file hashes.
`VanillaFreeplayAssets.cs` sets texture and audio import properties.

The screen supports the songs and variations installed in Friday Fight Funkin'.
Song previews stream audio to avoid decoding complete instrumentals during selection.
Difficulty changes reuse song rows and update their titles, icons, BPM, ratings, ranks, and favorites.
The screen retains unchanged filters and does not replay the list entrance when the difficulty changes.
Pico includes the four original Weekend 1 songs.
Original Weekend 1 songs appear in Freeplay only when Pico is selected.
Pico and the four Weekend 1 songs are available in Freeplay from the first session.
Pico also includes 15 Pico mixes.
BF includes Darnell and Lit Up BF mixes.
Each mix has separate scores and favorites.
Character selection filters songs by their player metadata.
The results screen retains the existing Friday Fight Funkin' behavior.
Character select preserves the BF and Pico animations, selection grid, introduction video, and selection music.
Escape cancels confirmation before the exit transition.
Character selection persists between sessions.
Character select preloads character atlases, Freeplay textures, and DJ timelines in the background.
Cursor interpolation uses the source precision curve and separate afterimage durations.
Confirmation cancellation reverses the icon animation and restores music over one second.
Exit applies the original blue-channel fade, flipped gradient, camera motion, and cursor opacity.
BF, GF, Pico, static character, and lock frames retain the source masks and blend effects.
Cancellation poses composite purple face layers with Overlay blending before atlas packing.
The reference capture separates base artwork, Overlay layers, and foreground details.
The importer combines these captures in sRGB to retain skin colors and eye details.
Nene retains the source blur and blend effects. Her speaker visualizer remains animated.
`Scripts/CharacterSelectReference/Build.ps1` renders the imported timelines with HaxeFlixel.
`Scripts/ImportCharacterSelectFrames.py` packs these frames into their existing atlases.
BF, GF, Pico, and Nene use captures at twice the reference resolution.
Atlas packing retains their logical size and placement.
Windows and UWP use BC7 compression for these four atlases.
`charSelect/rendered-frames.json` records frame counts and output hashes.
Character switching reuses the current song catalog.
The Animate renderer preserves affine transforms, frame labels, atlas rotation, and the color multipliers used by these assets.
Character select and the Pico backing card use additive, screen, and multiply blending.
The screen retains a 1280 by 720 viewport with letterboxing at other aspect ratios.

The artwork, sounds, fonts, and music retain their upstream ownership.
`LICENSE.md` contains the asset license. `Source-LICENSE.md` contains the source license.

## Validation

Run validation in a separate project copy with graphics enabled.
Set `UNITY_PARTY_FREEPLAY_TEST_PATH` to the output directory.
Use Unity batch mode with `-executeMethod VanillaFreeplayValidation.Begin`.
Do not add `-quit`. The asynchronous probe exits after its checks.
Set `FRIDAY_FIGHT_FUNKIN_BUILD_PATH` to build a Windows player after successful checks.

The probe checks catalog controls, score formulas, rank assets, menu entry, preview cancellation, difficulty filtering, favorites, gameplay return, and the bundle picker.
The probe also checks entry and exit motion, transition input locks, and all 13 imported icon confirmation sequences.
It captures entry, confirmation, exit, normal, Erect, return, widescreen, and blank-control images.

Use `-executeMethod VanillaFreeplayParityValidation.Begin` to check presentation timing, selection rules, favorites, albums, and SPAGHETTI states.
The probe captures both characters and checks the confirmation delay and black fade.
Run `Scripts/FreeplayReference/Build.ps1` to capture capsule titles with HaxeFlixel.
Run `python Scripts/TestFreeplayPresentation.py` to compare the reference and Unity title captures.
The comparison includes normal titles, confirmation flashes, and flat, blank, and shifted controls.
These checks cover isolated title rendering and selected menu states. They do not establish complete screen or console parity.

Run `Scripts/FreeplayScreenReference/Build.ps1` to capture 11 screen states from an isolated copy of the supplied executable.
Use `-executeMethod VanillaFreeplayScreenValidation.Begin` to capture matching Unity states.
Set `UNITY_PARTY_FREEPLAY_TEST_PATH` to `Builds/FreeplayScreenVerification` for these captures.
Run `python Scripts/TestFreeplayScreenRendering.py` to compare capsule positions, detail visibility, backdrop brightness, and Pico confirmation placement.
Pass `--baseline Builds/FreeplayScreenBaseline` when captures from before the corrections are available.
The baseline must fail all four checks. BF spacing, idle brightness, and faded details provide additional controls.
The Unity probe also executes the launch coroutine through 0.6 seconds for both characters and rejects detail fading.
Captures cover Bopeebo on Hard with BF and Pico. Pico artwork comparisons use matching atlas frame 28.
These checks do not establish parity for other songs, every animation frame, or Xbox output.

Set `UNITY_PARTY_FREEPLAY_PERFORMANCE_PATH` and run `VanillaFreeplayPerformanceValidation.Begin` to measure song selection and preview frame gaps.
The probe checks streamed playback, seeking, preview loops, and rapid selection cancellation.
It saves timing and audio memory measurements in `result.json`.
Set `UNITY_PARTY_FREEPLAY_DIFFICULTY_TEST=1` to measure difficulty changes with the same probe.
This mode checks row reuse, difficulty details, rank visibility, remapped buttons, unavailable remixes, and empty filters.

Use `-executeMethod VanillaFreeplayRankValidation.Begin` to check rank return behavior in the isolated project.
This probe checks six ranks, upgrades, first clears, animation timing, input locks, preview audio, and actual gameplay return.
Negative controls cover equal ranks, lower ranks, aborted runs, autoplay, Opponent mode, empty charts, and mismatched song or difficulty.
It captures badge impact, capsule recoil, the restored menu, widescreen output, and a blank control.

Use `-executeMethod VanillaFreeplayDJValidation.Begin` to check DJ behavior in the isolated project.
This probe checks idle thresholds, loop boundaries, activity resets, TV frame cues, streamed audio, preview volume, and exit cleanup.
Controls verify that AFK stays inactive before its threshold and does not replace rank reactions.
The probe observes the 60-second idle trigger in Play mode and captures AFK and TV frames.

Set `UNITY_PARTY_CHARACTER_TEST_PATH` and run `VanillaCharacterSelectValidation.Begin` to check character select.
The probe checks the introduction video, fresh-save Pico access, confirmation cancellation, saved selection, and both character catalogs.
It checks that BF excludes original Weekend 1 songs before and after switching to Pico.
It rejects a finished intro held for 100 milliseconds and checks that Pico resumes animation with his backing card visible.
Character filtering, missing charts, and blank renders provide controls.

Set `UNITY_PARTY_CHARACTER_PARITY_TEST=1` to run character rendering and timing checks with the character select probe.
Use `Builds/CharacterSelectParity` as the output directory for `Scripts/TestCharacterSelectRendering.py`.
The comparison checks 38 reference frames and 32 additional poses at 1080p.
Controls check flat colours, blank renders, shifted renders, and enlarged 720p output.
Nene checks include local pixel differences, removed filters, and separate visualizer changes.
All four characters include an opaque face overlay control.
Face checks also reject incorrect foreground order around Pico and Nene eye outlines.
Use `VanillaCharacterSelectValidation.RunRendering` to capture reference poses without entering Play mode.
The runtime probe checks cursor delays, locked denial, confirmation cancellation, music recovery, exit fading, and cleanup.
