# AfterParty healthbar and judgements

The AfterParty HUD adapts Friday Night Funkin' 0.8.6 behavior to Unity.
The reference files are `PlayState.hx`, `HealthIcon.hx`, `PopUpStuff.hx`, and `NoteStyle.hx`.
The Flixel reference revision is `141f23c400c0508c76d5a09a143f5ce6790f8122`.

## Healthbar

The border measures 601 by 19 pixels in a 1280 by 720 reference area.
The fill measures 593 by 11 pixels, with a four-pixel inset.
The bar starts at Y 648 for upscroll and Y 72 for downscroll.
The opponent fill is `#FF0000`.
The player fill is `#66FF33` and grows from right to left.
The fill uses Flixel's 100 divisions and pixel rounding.
Each update interpolates displayed health toward actual health by 0.15.
Bot play displays full health without changing actual health.

Icons follow interpolated health but select their faces from actual health.
An idle icon enters its losing state below 20 percent and leaves that state above 20 percent.
Icons with a winning frame use the same strict comparisons at 80 percent.
Two-frame icons retain their idle frame at high health.
Icons measure 150 pixels and expand by 30 pixels on each beat.
The linear return lasts two steps, capped at 0.175 seconds.
Press 9 to toggle the original Boyfriend icon.

The score uses the original bitmap font, outline, spacing, position, and comma separators.
The HUD uses reference coordinates and follows the existing HUD camera zoom.

## Judgements

Ratings overlap instead of replacing an existing sprite.
Ratings use a scale of 0.65 and the upstream screen position.
Ratings move upward at 140 through 175 pixels per second with gravity of 550 pixels per second squared.
Their leftward speed ranges from zero through ten pixels per second.
Ratings fade for 0.2 seconds after one beat.

Combo digits appear at ten hits and use at least three digits.
A broken combo of ten or more displays `000`.
Ghost misses leave the combo unchanged and show no popup.
Missed notes show no rating sprite.
Combo digits use a scale of 0.45 and 36-pixel spacing.
Digits use the upstream random velocity and gravity ranges.
Digits fade for 0.2 seconds after two beats.
Pausing gameplay stops popup movement, fading, and icon bounces.

Boyfriend and Opponent modes show the selected character's score and centered judgement popups.
The default Funkin judgement style and legacy icon sheets are supported.
Animated icon sheets, scripted icon settings, and other judgement styles are outside this port.

## Validation

Run `Scripts/TestFunkinRules.ps1` for rule checks and frame-rate controls.
The Unity gameplay harness checks health smoothing, icon faces, bot display, score formatting, combo thresholds, breaks, overlap, and popup lifetimes.
It saves upscroll, downscroll, low-health, and high-health renders with blank-render controls.
Pixel checks verify bar position and both fill colors.
Run `FunkinGameplayValidation.Begin` in an isolated Unity project without `-quit`.

## Assets

The initial images came from the local Funkin installation.
The 39 matching HUD images were verified against the official 0.8.6 asset revision.
The bitmap font came directly from that revision.
Asset revision: [9300719cb261d23f72344807055be6d109c9949c](https://github.com/FunkinCrew/Funkin.assets/tree/9300719cb261d23f72344807055be6d109c9949c).

`LICENSE.md` preserves the artwork license.
`Source-LICENSE.md` preserves the source license for the adapted implementations.
