# AfterParty credits

The AfterParty credits follow `CreditsState.hx` from Friday Night Funkin' 0.8.6.
The original sections retain their text and order.
The data matches `FunkinCrew/funkin.assets` commit `9300719cb261d23f72344807055be6d109c9949c`.
The original Unity Party section follows the original credits.
It preserves Team Determination, Rei the Goat, UniBrine, and St4bility aka Thesnakerox with their original roles.

The screen uses a black background and a 24-pixel left margin.
Text occupies half the available width, after two 24-pixel margins.
Headers use size 32 and bold text.
Body text uses size 24.
The Windows source requests Consolas through OpenFL.
The baked text preserves the renderer's output, including its system font fallback.
No font file is bundled.

Credits start below the screen and move at 100 pixels per second.
Entry and return use the original vertical fades, with a 0.7-second cover and a one-second reveal.
Scrolling waits while a transition runs.
Hold Enter or Space to move at 400 pixels per second.
Hold Shift or P to pause scrolling.
Fast scrolling takes precedence over pause.
Press Escape, Backspace, X, or the controller back button to return.
The menu also returns when the final line leaves the screen.
Music uses `VanillaFreeplay/audio/freeplayRandom` and fades from silence to 80 percent of the menu volume over six seconds.
Returning restores `freakyMenu` and the selected menu entry.

`unity-party.json` preserves the original Unity Party section after the AfterParty rename.
`credits.json` preserves the imported original data.
`lines.json` contains text metrics and atlas coordinates for logical widths from 1280 through 1600.
Rebuild the text after changing either credit document.

Run `Scripts/CreditsReference/Build.ps1 -Source <Funkin source directory>` to bake text and generate reference captures.
The harness compiles the original credits state with the pinned FlxText source from its dependency manifest.
It substitutes state switching, audio, and unused background loading.
It retains credit construction, spacing, scrolling, and recycling.
It also captures the original transition gradients from `TransitionFade`.
Run `Scripts/ImportVanillaCredits.py` with `--source` and `--executable` to pack the generated text.
The importer verifies every original credit string against the local Funkin executable.

`VanillaCreditsValidation.Begin` tests menu entry, scrolling, pause, fast scroll, automatic return, reopening, music, and source layout metrics.
Run it in an isolated batch editor.
Set `UNITY_PARTY_CREDITS_REFERENCE_PATH` to the reference capture directory.
Set `UNITY_PARTY_CREDITS_TEST_PATH` to the validation output directory.
`Scripts/TestVanillaCredits.py` compares runtime captures with source captures and checks a blank control.
Static text and fade captures allow one color level of rendering roundoff.
Fractional scroll captures allow a mean channel error below 0.02 on the 0-255 scale.

The original credits and music retain their upstream ownership.
`LICENSE.md` contains the asset license.
`Source-LICENSE.md` contains the source license.
