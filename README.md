# AfterParty Engine

**Friday Night Funkin’: AfterParty** is a Unity-based Friday Night Funkin' engine.
AfterParty forks [Unity Party](https://github.com/Team-Determination/Unity-Party), created by Team Determination.

## Features

- Intro, title, menus, credits, and gameplay behavior adapted from Friday Night Funkin' 0.8.6.
- Tutorial, Weeks 1 through 7, and Weekend 1, with available Erect and Nightmare charts.
- All 15 Pico mixes and both BF mixes from Friday Night Funkin' 0.8.6.
- BF and Pico results screens with rank animations, song tallies, and campaign totals.
- Boyfriend, Opponent, and AutoPlay modes.
- Primary and secondary control bindings.
- Song bundles with custom scripts, song metadata, and album artwork.
- Note colors, input and note offsets, and separate volume controls.

Online and local multiplayer are not supported.
The port documentation describes supported content, behavior, and remaining differences.

## Controller controls

Use the D-pad or either stick to play notes.
The left, bottom, top, and right face buttons play left, down, up, and right notes.
Use the D-pad or left stick to navigate menus.
Press the bottom face button to confirm, advance dialogue, start a song, or retry after game over.
Press the right face button to go back or exit after game over.
Press Start to pause or resume gameplay.
Press the left face button to change a song favorite in Freeplay.

## Open and build

Install Unity **6000.6.1f1** and Git before opening this project.
The project uses Universal Render Pipeline 17.6 and Input System 1.19 for gameplay.
The legacy Input Manager remains enabled for menus.
TextMesh Pro is included through the Unity UI package.

Open the project in Unity and wait for package resolution and asset import.
Open `Assets/Scenes/Title.unity` to start the game in the editor.

Select **Build > Windows 64-bit** to create `Builds/Windows/AfterParty.exe`.
For command-line builds, use Unity batch mode with `-executeMethod BuildAutomation.BuildWindows`.
Set `AFTERPARTY_BUILD_PATH` to select another output executable path.
The build also accepts `UNITY_PARTY_BUILD_PATH` for existing automation.
`AFTERPARTY_BUILD_PATH` takes precedence when both variables have values.

The GitHub Actions workflow builds Windows 64-bit and uploads the `AfterParty-Windows64` artifact.
Configure `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL` repository secrets before running it.
The workflow requires a valid Unity license and a matching GameCI editor image.

Unity 2021 is not supported.
Keep the package lock file when cloning or updating the project.

## Validation

Run Unity with `-batchmode -quit -executeMethod UpgradeValidation.Run` to check object pools, material caching, and CRT rendering.
Keep graphics enabled for this check.
Set `UNITY_PARTY_VALIDATION_PATH` to save the CRT control and output images.
The validation tools retain their `UNITY_PARTY_*` variables for compatibility.
Each port document lists its validation commands and control cases.

Run `VanillaGameplayPerformanceValidation.RunAndBegin` in an isolated batch editor with graphics enabled and without `-quit`.
Set `UNITY_PARTY_GAMEPLAY_PERFORMANCE_PATH` to the output directory.
The probe measures character allocations and checks animation frames, note hits, and mesh creation during four songs.
Frame timings include editor overhead and other running applications.

## Documentation

- [Intro, title, and diamond transitions](Assets/Resources/VanillaTitle/README.md)
- [Main menu](Assets/VanillaMenu/README.md)
- [Options menu](Assets/Resources/VanillaOptions/README.md)
- [Story Mode](Assets/Resources/VanillaStory/README.md)
- [Freeplay](Assets/Resources/VanillaFreeplay/README.md)
- [Credits](Assets/Resources/VanillaCredits/README.md)
- [Songs, characters, and stages](Assets/StreamingAssets/Bundles/README.md)
- [Input, judgement, and notes](Assets/Resources/FunkinNotes/README.md)
- [Healthbar and judgements](Assets/Resources/FunkinHud/README.md)
- [Pause menu](Assets/Resources/FunkinPause/README.md)
- [Results screens](Assets/Resources/VanillaResults/README.md)

## Save compatibility

AfterParty retains the Unity company and product identifiers `Rei` and `FridayNight`.
These identifiers preserve existing settings, scores, songs, bundles, characters, scenes, and replays.
Windows data remains in `%USERPROFILE%\AppData\LocalLow\Rei\FridayNight`.
Player preferences retain the registry key `HKEY_CURRENT_USER\Software\Rei\FridayNight`.
The Windows caption displays **Friday Night Funkin’: AfterParty**.
Internal shader identifiers and validation variables also retain their existing names.

## Credits and licenses

The in-game credits preserve the Friday Night Funkin' 0.8.6 credits and the original Unity Party developers.

| Original Unity Party contributor | Role |
| --- | --- |
| Team Determination | Original Unity Party team |
| Rei the Goat | Engine Developer |
| UniBrine | Engine Designer |
| St4bility aka Thesnakerox | Engine Music Composer and Engine Advisor |

Special thanks to **raonyreis13** for procedural note spawning.

[LICENSE](LICENSE) contains the repository license.
Imported source and assets retain their upstream ownership and licenses.
The port directories contain the applicable license files.
