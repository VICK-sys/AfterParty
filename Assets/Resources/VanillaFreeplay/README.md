# Freeplay

The main menu opens a Unity adaptation of the Boyfriend Freeplay screen from Friday Night Funkin' 0.8.6.
It uses the original artwork, fonts, capsule animations, album animations, letter filters, and Boyfriend DJ timeline.
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
- Press Tab to select a Unity Party play mode.
- Use the mouse wheel to select a song.
- Click a capsule to select it. Click the selected capsule to play it.

Controller axes select songs and difficulties.
The primary button confirms. The secondary button returns. The third button changes favorites.

The Random entry selects a playable song from the current filter and difficulty.
Song previews use the metadata range, or the first 20 percent of the instrumental.
The menu restores the song and difficulty after gameplay.
Entry keeps the main menu visible behind the moving card, backdrop, and capsules until the DJ intro finishes.
Exit moves the screen elements separately and restores main menu input after 0.5 seconds.
Song confirmation plays the selected icon animation at 10 frames per second, then holds its confirmation pose.

## Scores

The score display reads existing Unity Party high scores.
Completed Boyfriend runs also save clear percentage and rank.
Clear percentage uses `(Sick + Good - Miss) / chart note count`, clamped between zero and one.
Skipped chart notes count as misses for this display.
Aborted runs and autoplay do not save ranks.
Older scores have no rank until a completed run records one.

## Assets and scope

`Scripts/ImportVanillaFreeplay.py` imports assets from the local Funkin Windows release.
The importer extracts four embedded fonts and normalizes the letter animation JSON.
`import-manifest.json` records the release executable hash and imported file hashes.
`VanillaFreeplayAssets.cs` sets texture and audio import properties.

The screen supports the songs and variations installed in Unity Party.
It does not import additional charts, Pico gameplay, character selection, unlock screens, or result-screen celebrations.
Tab opens Unity Party play modes in place of character selection.
The Animate renderer preserves affine transforms, frame labels, atlas rotation, and the color multipliers used by these assets.
Special Animate blend modes use normal alpha blending.
The screen retains a 1280 by 720 viewport with letterboxing at other aspect ratios.

The artwork, sounds, fonts, and music retain their upstream ownership.
`LICENSE.md` contains the asset license. `Source-LICENSE.md` contains the source license.

## Validation

Run validation in a separate project copy with graphics enabled.
Set `UNITY_PARTY_FREEPLAY_TEST_PATH` to the output directory.
Use Unity batch mode with `-executeMethod VanillaFreeplayValidation.Begin`.
Do not add `-quit`. The asynchronous probe exits after its checks.
Set `UNITY_PARTY_BUILD_PATH` to build a Windows player after successful checks.

The probe checks catalog controls, score formulas, rank assets, menu entry, preview cancellation, difficulty filtering, favorites, gameplay return, and the bundle picker.
The probe also checks entry and exit motion, transition input locks, and all 13 imported icon confirmation sequences.
It captures entry, confirmation, exit, normal, Erect, return, widescreen, and blank-control images.
