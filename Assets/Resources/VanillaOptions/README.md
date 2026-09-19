# Options menu

The Options entry uses the desktop menu from Friday Night Funkin' 0.8.6.
The port includes Preferences, Controls, Lag Adjustment, Clear Save Data, and Exit.
Newgrounds account services and the upstream debug editors are not integrated.
Their conditional menu entries are omitted.

## Presentation

The menu uses the original background, animated fonts, checkbox atlas, sounds, and calibration tracks.
The background shader uses the source HSV values.
Pages use a 1280 by 720 viewport.
Preferences use 120-pixel rows and a fixed description panel.
Controls use 70-pixel rows and two binding columns.
The camera follows the selected row with the source smoothing rates.
Button confirmation takes one second.
Numeric values repeat after 0.3 seconds, at 0.08-second intervals.

## Preferences

The 17 desktop preferences save immediately.
Downscroll also updates the existing MiscOptions record.
Naughtyness selects safe results animations, censored note animations, censored dialogue, and the two censored Stress cutscenes.
Subtitles controls song and cutscene subtitles.
Camera Zooms controls beat zooms.
Pause on Unfocus opens the gameplay pause menu.
Strumline Background controls the opacity of the backgrounds behind both strumlines.
Launch in Fullscreen applies when the standalone application starts.
FPS, Unlocked Framerate, VSync, Debug Display, and Discord RPC apply immediately.
Unity uses regular VSync when Adaptive is selected.
The backend does not expose the source adaptive swap interval.

Screenshot controls save PNG files under the persistent data directory in `screenshots`.
Hide Mouse, Fancy Preview, and Preview on Save control capture and preview behavior.
The preview uses a Unity overlay.
It does not reproduce the upstream screenshot editor.

## Controls and calibration

Keyboard and gamepad bindings apply to menus and gameplay.
Existing note bindings migrate into the Controls page.
Bindings retain additional source defaults beyond the two visible columns.
Conflicts swap inputs within their control group.
The final binding for a UI action cannot be removed.
Escape cancels keyboard rebinding.
Backspace removes the selected binding.
Gamepad Back cancels gamepad rebinding.

Lag Adjustment uses the original 100 BPM music and drum loops.
Global Offset uses the existing `Funkin.GlobalOffset` preference.
The calibration computes an average every four taps.
It saves the result after 30 taps.
It resets inconsistent samples when their standard deviation exceeds 40 milliseconds.
Cancel preserves the previous offset.
Test creates four-lane notes and displays early, late, and average timing feedback.

Clear Save Data requires the in-game confirmation prompt.
Cancel leaves saved data intact.
Delete clears PlayerPrefs and returns to the title screen.
Imported content and user files remain on disk.

## Import and validation

Run `python Scripts/ImportVanillaOptions.py <source> <release>` to import the assets.
FFmpeg remuxes the censored cutscenes and extracts their audio and subtitles.
The manifest records source hashes and imported asset hashes.
Run `python Scripts/TestVanillaOptions.py` to verify imported files.

Run Unity in an isolated project with `-batchmode -executeMethod VanillaOptionsValidation.Begin`.
Set `UNITY_PARTY_OPTIONS_TEST_PATH` to select the output directory.
The checks cover entry, preferences, scrolling, control conflicts, persistence, gamepad bindings, save cancellation, calibration, and exit.
The checks restore the tested PlayerPrefs records.
They do not confirm save deletion.
A blank render serves as the negative control for screenshot capture.

Run `Scripts/OptionsReference/Build.ps1 -Source <source>` to capture the source font layout in HaxeFlixel.
The reference needs Haxe, Neko, OpenFL, Lime, and HaxeFlixel 6.2.0.
Screenshots and reference metrics are written under `Builds`.
Run `python Scripts/TestOptionsRendering.py` to compare the root, preferences, and controls captures.
The comparison also rejects the blank render.

## Licenses

`LICENSE.md` contains the release asset license.
`Source-LICENSE.md` contains the source license.
