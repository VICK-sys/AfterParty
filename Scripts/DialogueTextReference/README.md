# Dialogue text reference

The probe runs inside the Windows Funkin 0.8.6 executable.
It exports the original watermark renderer with the text `Made in Unity`.
It also exports Pixel Arial 11 Bold glyphs and 44 dialogue cases.
The glyph capture includes spaces to preserve negative bearings.
The cases cover all six bundled Week 6 conversations and three typewriter samples.

Use a separate executable copy with a Polymod module named `freeplay-probe`.
Copy `FreeplayProbe.hxc` into that module's `scripts` directory.
Copy `cases.json` to `dialogue-text-cases.json` beside the executable.
Run the executable from its directory.
The probe writes `DialogueTextParity/reference` and closes after `DIALOGUE_TEXT_DONE 44`.
Restore any previous probe module after capture.

Run `Scripts/ImportDialogueTextReference.py` with the exported reference directory.
The importer copies the glyph atlas and metrics.
It converts the label to premultiplied alpha for scaled rendering.

Set `UNITY_PARTY_DIALOGUE_TEXT_PATH` to a directory containing `reference`.
Run `VanillaDialogueTextValidation.Run` in an isolated Unity batch editor with graphics enabled.
Run `Scripts/TestDialogueTextRendering.py` with the same directory.
The comparison checks wrapping and RGB output at 720p and 1080p.
Blank and shifted images must fail the comparison.
