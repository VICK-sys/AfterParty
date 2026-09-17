**Unity Party** is an FNF' engine created using the Unity game engine focused on allowing lower-end PCs to play FNF'.

## Note Worthy Features
### High FPS
Thanks to Unity, the game is able to run better on lower-end PCs than HaxeFlixel. You can modify the engine's source code to do complicated effects that might cause some lower-end PCs to struggle when done on HaxeFlixel, though you can replicate the effects in Unity and lower-end PCs will run it fine.
### Control Mapping
You can remap your keybinds to any key on your keyboard, but you also have a SECONDARY keybind list that you can use to at any time without having to manually switch it all the time!
### Two Player Mode
The engine natively supports two-player. You and the other player play on the same keyboard. Keybinds for both players are also modifiable.
If the game is set to Two Player Mode, primary keybinds are used for player one and secondary keybinds are used for player two.
### Song Bundles
Bundles are a collection of songs that each can possibly contain custom scripting. Each song in a bundle can contain information such as artist name, charter name, and even an album cover. Bundles can be exported to a .ZIP format and easily shared. If applicable, users can download bundles if they have the download URL via the in-game bundle downloader. Bundles are almost always forward-compatible with future versions of the engine.
### AutoPlay
You just wanna see how that one song is played out? The engine can autoplay any song for you. This depends on your PC's performance, however. Meaning lower performance, the less accurate the AutoPlay will be, but usually AutoPlay will be precise.
### Note Color Customization
This is also a kind of support for the colorblind! You can customize the color of each of the 4 note keys to any color you want, even pure black!
### Offset System
You can test and change your offset for both inputs and notes.
### Sound Channels
Are some voices or music too loud? What if the music is too loud but the voices are not? That's okay, you can choose which sound to turn down separately!

## Special Thanks
**raonyreis13** - Procedural Notes Spawning

## Requirements for Editing and Building

Install Unity **6000.6.1f1** and Git before opening this project.
The project uses Universal Render Pipeline 17.6 and Input System 1.19 for gameplay.
The legacy Input Manager remains enabled for menus.
TextMesh Pro is included through the Unity UI package.

Open the project in Unity and wait for package resolution and asset import.
Open `Assets/Scenes/Title.unity` to start the game in the editor.

Select **Build > Windows 64-bit** to create `Builds/Windows/Unity Party.exe`.
For command-line builds, use Unity batch mode with `-executeMethod BuildAutomation.BuildWindows`.
Set `UNITY_PARTY_BUILD_PATH` to select another output executable path.

Run `-batchmode -quit -executeMethod UpgradeValidation.Run` to check object pools, material caching, and CRT rendering.
Keep graphics enabled for this check.
Set `UNITY_PARTY_VALIDATION_PATH` to save the CRT control and output images.

The GitHub Actions workflow builds Windows 64-bit.
Configure `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL` repository secrets before running it.
The workflow requires a valid Unity license and a matching GameCI editor image.

Unity 2021 is no longer supported by this fork.
Keep the package lock file when cloning or updating the project.

Gameplay uses the Funkin 0.8.6 input, judgement, receptor, and sustain rules.
See `Assets/Resources/FunkinNotes/README.md` for behavior, scope, and validation commands.
