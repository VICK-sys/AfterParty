# Funkin note behavior

The behavior reference is Friday Night Funkin' v0.8.6.
The source reference is the local `Funkin-0.8.6` checkout.
The artwork comes from the local `funkin-windows-64bit` installation.

The following files adapt the upstream Haxe implementation to C# and Unity.
These files contain modified implementations, rather than copies of the original files.

| Unity file | Upstream source |
| --- | --- |
| `FunkinRules.cs` | `Scoring.hx`, `Constants.hx`, `GRhythmUtil.hx`, `Strumline.hx`, `StrumlineNote.hx`, `PlayState.hx` |
| `Player.cs` | `PreciseInputManager.hx`, `Controls.hx`, `PlayState.hx` |
| `Song.Funkin.cs` | `PlayState.hx`, `Strumline.hx` |
| `NoteObject.cs` | `NoteSprite.hx`, `Strumline.hx` |
| `FunkinHoldMesh.cs` | `SustainTrail.hx` |
| `FunkinNoteSkin.cs` | `NoteStyle.hx`, Sparrow atlas data |
| `FunkinStrumEffect.cs` | `NoteSplash.hx`, `NoteHoldCover.hx`, `Strumline.hx` |

## Input and judgement

Unity Input System events provide timestamps for keyboard and controller input.
The legacy Input Manager remains enabled for menus and existing controls.
Restart Unity after changing the active input backend.

Gameplay processes queued presses before releases, as the reference does.
Each physical key maintains its own held state.
Releasing one binding does not release another binding for the same direction.
One press consumes one eligible note.
The selection uses time order and deprioritizes low priority notes.

The hit window includes both endpoints at 160 milliseconds.
Candidate eligibility follows the reference frame order.
Event timestamps compensate judgement latency, but cannot recover notes that have already registered a miss.
Judgement truncates the signed timing difference toward zero before applying PBOT1.

| Rating | Absolute timing | Health change on a 200-point scale | Combo |
| --- | --- | --- | --- |
| Sick | 0 through 45 ms | +3 | Increase |
| Good | 46 through 90 ms | +1.5 | Increase |
| Bad | 91 through 135 ms | 0 | Break |
| Shit | 136 through 160 ms | -2 | Break |
| Miss | Past the window | -8 | Break |

PBOT1 awards 500 points below 5 ms and uses the upstream sigmoid for other hits.
A missed note costs 100 points.
An empty press costs 10 points and 8 health, without changing the note tally or combo.
Optional ghost tapping permits empty presses only outside active notes, holds, and the 0.375-second lockout.

## Holds and rendering

Each hold uses its exact chart duration and one continuous mesh.
Holding grants 250 points and 12 health per second.
A dropped hold cannot recover.
A remaining tail longer than 160 ms costs 125 points per remaining second and breaks the combo.
The reference assigns no extra health loss to a dropped tail.
Fractional hold scores accumulate before the display converts them to integers.
Bot play animates notes and holds without awarding player score or hit tallies.

Receptors use static, press, confirm, and confirm-hold states.
The confirm timeout is 0.15 seconds after player animation completion.
Automated receptors start that timeout when confirmation begins.
Note movement uses 0.45 pixels per millisecond, multiplied by scroll speed.
The renderer uses 112-pixel lane spacing and a 1280 by 720 reference area.
The atlas loader preserves frame offsets and packed frame rotation.
Sick hits create splashes, and successful holds create covers.
Each strumline shares a pool of six splashes across its four lanes.

Unity Party retains its song loader, countdown, characters, camera, options, calibration, and multiplayer extensions.
This change does not port the upstream chart format, scripted note kinds, mobile controls, or every note style.
The built-in notes use the Funkin style.
The existing input and visual offset preferences remain available.
Hardware latency and cross-engine pixel parity require comparison with a running upstream build.

## Validation

Run `Scripts/TestFunkinRules.ps1` with .NET 10 to check deterministic rules and negative controls.
Run Unity with `-batchmode -executeMethod FunkinGameplayValidation.Begin` in an isolated project copy.
Do not pass `-quit` to the gameplay validation command.
The harness exits after checking the gameplay scene and saving two renders under `Validation`.
The harness changes play mode and temporary runtime options.

Rule checks cover input and HUD behavior, plus 18 bundled charts with 5,801 note heads at 30, 60, and 144 frames per second.
Runtime checks cover countdown input, event latency, keyboard and controller bindings, hold release, pooling, bot play, and both scroll directions.
Negative controls check incorrect event timestamps, blank renders, and separate strumline splash pools.

`VanillaSongValidation.Begin` checks the bundled songs, including bot note consumption and zero bot score.

## Ownership

Friday Night Funkin' Copyright 2020-2024 The Funkin' Crew Inc.
`Source-LICENSE.md` preserves the upstream source license and copyright notice.
`LICENSE.md` preserves the asset license.
The artwork retains its upstream ownership and distribution restrictions.
