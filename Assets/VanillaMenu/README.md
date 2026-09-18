# Vanilla main menu

The Title scene uses the desktop main menu artwork and layout from Friday Night Funkin' 0.8.6.
The source reference is `FunkinCrew/Funkin`, tag `v0.8.6`.
The asset reference is `FunkinCrew/funkin.assets`, commit `9300719cb261d23f72344807055be6d109c9949c`.

`VanillaMainMenu.cs` and `VanillaMenuItem.cs` adapt the menu behavior to Unity UI.
`VanillaMenuBuilder.cs` converts Sparrow atlas coordinates and creates scene objects.
The implementation uses 24 FPS animation, 160-pixel spacing, and the original camera follow and flicker timing.

Select `Tools > Unity Party > Rebuild Vanilla Main Menu` to regenerate the main menu in the Title scene.
This action replaces the main menu scene objects.
The scene contains editable backgrounds, animated entries, and a version label.
The version label identifies the visual reference.

Use Up and Down or W and S to select an entry.
Use Enter or Space to confirm.
A controller can use its vertical stick, primary button, and secondary button.
Escape closes Credits or returns to the title from the main menu.
In the Unity Editor, quit stops Play mode.

Story Mode opens the level menu from Friday Night Funkin' 0.8.6.
See `Assets/Resources/VanillaStory/README.md` for controls, installed content, and validation.
Freeplay opens the Boyfriend Freeplay screen from Friday Night Funkin' 0.8.6.
See `Assets/Resources/VanillaFreeplay/README.md` for controls and validation.
Options opens the existing settings screen.
Merch opens the official shop URL.
Credits shows the original credits and the Unity Party team.
See `Assets/Resources/VanillaCredits/README.md` for controls and validation.
The intro and title screen follow Friday Night Funkin' 0.8.6.
See `Assets/Resources/VanillaTitle/README.md` for controls and validation.
The serialized `flashingLights` field disables confirmation flashing.

The artwork, music, sounds, font, and credits retain their upstream ownership.
`LICENSE.md` contains the asset license.
That license restricts public redistribution of the content.
`Source-LICENSE.md` contains the source license and copyright notice.
