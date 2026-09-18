# Vanilla intro and title

The Title scene starts with the desktop intro from Friday Night Funkin' 0.8.6.
The source reference is the local `Funkin-0.8.6` checkout.
The artwork, audio, splash text, and videos come from the local Funkin Windows distribution.
The import manifest records the source and asset hashes.

The first entry waits one second before the menu music starts.
The original animated credits follow the 102 BPM music through beat 16.
The intro selects one of 60 splash pairs and one of three Newgrounds logos.
The title uses the original logo, Girlfriend dance frames, and Adobe Animate prompt at 24 FPS.
The menu music fades in over four seconds.

Press Enter or controller Start to skip the credits.
Press Enter or the primary controller button on the title to confirm.
Confirmation plays the original sound at 70 percent of the effects volume.
The title starts the main menu transition after two seconds.
Press Enter again to start that transition immediately.
Press Escape, Backspace, or the secondary controller button to quit from the title.
The main menu returns to the title when you press Back.
Both directions use the diamond wipe shader.
The wipe covers the screen over 0.5 seconds, switches screens, and reveals the destination over 0.5 seconds.
Its 10-pixel diamond cells follow the supplied shader formula with normalized progress.
Input stays locked until the reveal completes.
Menu music continues across the wipe.
Returning to the title skips the credits for the current session.
Gameplay returns retain the existing Story Mode, Freeplay, and calibration destinations.

Hold Left or Right to change the title hue.
Use Up, Down, Up, Down, Left, Right, Left, Right to play Girlfriend's ringtone.
The ringtone uses 160 BPM and changes hue on every second beat.
Press Y in a windowed Windows player to move the window with the original tween timing.
Window movement is disabled in the Unity Editor and fullscreen mode.

After 37.5 seconds, the title fades to a trailer over two seconds.
The trailers cycle through Rift of the NecroDancer, the mobile release, and Boyfriend Everywhere.
Hold a key to fill the skip indicator and return to the title.
The ringtone disables the trailer timer.
Menu volume controls music and trailer audio.
The main menu flashing setting also controls title flashes.

The base viewport is 1280 by 720.
Wider displays extend the layout up to 20:9 with the source positions and cutout offsets.
Taller displays use centered letterboxing.

## Import and validation

Run `python Scripts/ImportVanillaTitle.py <source directory> <desktop distribution>` to import the assets.
Run `python Scripts/TestVanillaTitleAssets.py` to check hashes, atlas bounds, fonts, music, splash pairs, BPM, and animation labels.
Run `VanillaTitleValidation.Begin` in an isolated Unity batch editor with graphics enabled.
Do not pass `-quit`.
Set `UNITY_PARTY_TITLE_TEST_PATH` to the output directory.
The probe checks live credit beats, startup input, confirmation timing, session return, the ringtone, trailer playback, and menu destinations.
Negative controls cover incorrect cheat input, early trailer entry, premature confirmation, and blank rendering.
The probe checks wipe input locks, duplicate requests, screen switching, music continuity, and cleanup.
GPU readback checks the diamond formula, both directions, complete endpoints, and two render resolutions.

Run `Scripts/TitleReference/Build.ps1 -Source <source directory>` to render the Flixel reference.
The reference uses the original AtlasText code and the source sprite positions.
The temporary Neko compatibility changes cover the pooling interface, JSON symbol arrays, and frame count initialization.
Run `python Scripts/TestVanillaTitleAssets.py --reference Builds/TitleReference --actual Builds/TitleValidation` to compare rendered frames.

The validated credits frame matches the Flixel reference exactly.
The title and confirmation frames have mean channel errors of 0.0108 and 0.0046 on an 8-bit scale.
Small differences remain along antialiased edges.
The Unity runtime probe and Windows build passed.
Physical controller input and Windows window movement were not exercised by the probe.

The artwork, music, sounds, and videos retain their upstream ownership.
`LICENSE.md` preserves the asset license.
`Source-LICENSE.md` preserves the source license.
