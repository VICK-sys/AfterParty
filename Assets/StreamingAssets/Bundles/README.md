# Friday Fight Funkin' campaign songs

These bundles contain the original Tutorial, Bopeebo, Fresh, and DadBattle charts from Friday Night Funkin' 0.8.6.
Each song includes Easy, Normal, and Hard.
Bopeebo, Fresh, and DadBattle also include Erect and Nightmare.
The song picker lists Tutorial, Weeks 1 through 7, and Weekend 1 in that order.
Week 1 lists Bopeebo, Fresh, and DadBattle in that order.

Week 2 lists Spookeez, South, and Monster in that order.
Each song includes Easy, Normal, and Hard.
Spookeez and South also include Erect and Nightmare.
The source does not contain a Monster Erect mix.

Built-in bundles load from `Assets/StreamingAssets/Bundles` in the editor and from StreamingAssets in Windows builds.
User bundles continue to load from the application data directory.
The importer does not copy files into user bundles or replace them.

The source reference is `FunkinCrew/Funkin`, tag `v0.8.6`.
The asset reference is `FunkinCrew/funkin.assets`, commit `9300719cb261d23f72344807055be6d109c9949c`.
`Source/chart.json` and `Source/metadata.json` preserve the source data for each song.
`Source/chart-erect.json` and `Source/metadata-erect.json` preserve the remix source files without changes.
`vanilla-import.json` records the revision, note counts, and instrumental hashes.
The instrumentals are unchanged.
Tutorial and Week 1 mix the vocal stems at their original gain into one Vorbis file at quality 8.
DadBattle Erect advances the Dad vocal stem by 8 milliseconds, as specified by the source metadata.
Erect and Nightmare share remix audio and events.
Each difficulty retains its source note timings, lanes, sustain lengths, tempo, and fractional scroll speed.
The difficulty selector updates the preview, song title, and credits when the variation changes.

`VanillaSongPlayback` processes camera focus, camera zoom, character animation, and scroll-speed events from `Vanilla.json`.
Tutorial and Week 1 use the source main stages and character atlases.
Week 1 remixes use the source Erect stage artwork, crowd animation, parallax, additive lights, and character color adjustments.
The remixes support Girlfriend camera focus, camera offsets, eased movement, stage-relative zoom, and camera bop events.
Tutorial uses the source singing Girlfriend at the stage Girlfriend position.
`Week1Assets/source-manifest.json` records source paths and SHA-256 hashes for the imported assets.
Character dances follow song steps, source `danceEvery`, and animation completion during countdowns and playback.
Singing, sustains, and protected animations take priority over idle dances.
Sing timers use the source character duration and current tempo.
Manual input keeps the timer running while a direction remains held.
The character returns to idle after release when the timer has expired.
Opponent and autoplay sustains reset the timer while active.
Bopeebo and Tutorial include the Boyfriend greeting animation.
Bundled songs start after the countdown.
Custom songs keep the existing start-key behavior.

Week 2 uses the source Boyfriend, Girlfriend, Spooky Kids, and Monster atlases.
The Erect mixes use the source dark characters and their normal counterparts during lightning.
The importer resolves Animate symbols, atlas rotation, color transforms, frame indices, and animation offsets.
Monster hides the Christmas hat layers, as specified by the source character script.
Both mansion stages preserve source positions, camera targets, parallax, animation frames, and lightning timing rules.
Normal Skid and Pump render 24 pixels above the source position.
Spookeez triggers silent lightning on beat 4.
Lightning does not interrupt Boyfriend's singing or miss animations.
New hit and miss animations can interrupt Boyfriend's scared animation.
Random lightning uses a 10 percent chance per beat after a random delay of 8 to 24 beats.
The Erect stage includes the animated trees, rain shader, and two-part lightning flash.
Pausing freezes the stage, lightning, and thunder audio.
Retry resets lightning and character state.
Game over uses the source Boyfriend death atlas and camera offsets.
Spookeez preserves the source `noanim` notes and cheer events.
Monster preserves all 14 tempo markers for character, camera, and HUD timing.

Week 2 gameplay plays the unchanged player and opponent vocal files separately.
Misses mute only the affected vocal stem.
The combined vocal files remain available for bundle compatibility.
`Week2Assets/source-manifest.json` records source paths and hashes for the copied visual assets and scripts.
`graphic.json` contains generated mesh geometry and animation indices derived from the source atlases.

Week 3 lists Pico, Philly Nice, and Blammed in that order.
Each song includes Easy, Normal, Hard, Erect, and Nightmare.
Week 3 uses the source Boyfriend, Pico, and Girlfriend animation atlases.
Both Philly stages preserve source art, placement, parallax, camera targets, and window colors.
Window lights change every four beats and use the source fade shader.
The train starts after the source cooldown and random beat check.
Train movement starts at 4700 milliseconds and advances 400 pixels per update at a maximum of 24 updates per second.
Eight train cars pass before Girlfriend plays the hair-fall animation.
The Erect stage applies the source hue, saturation, and brightness to the train and composited characters.
Philly Nice includes the Boyfriend greeting and Girlfriend cheer events.
Pico uses quartic camera easing, and Blammed Erect includes sine camera easing and camera bop events.
Pause freezes the train, lights, characters, and train audio.
Retry restores stage state, and game over uses the source Boyfriend death atlas.
Week 3 plays unchanged player and opponent vocal stems separately.
Its combined compatibility vocals use Vorbis quality 10.
`Week3Assets/source-manifest.json` records source artwork, character data, train audio, and script hashes.

Week 4 lists Satin Panties, High, and MILF in that order.
Week 5 lists Cocoa, Eggnog, and Winter Horrorland in that order.
Week 6 lists Senpai, Roses, and Thorns in that order.
Each song includes Easy, Normal, and Hard.
Seven songs also include Erect and Nightmare.
The source does not contain MILF or Winter Horrorland Erect mixes.
These weeks add 41 charts with 21,722 note heads and 16 audio variants.
The importer preserves source charts, instrumentals, and separate vocal stems.

Weeks 4 through 6 use the source character atlases, stage artwork, placement, and camera events.
The limo includes its dancers, fast car, and remix mist and shooting stars.
The mall includes its crowds, Santa, and the alternate parents and Boyfriend censor animations.
The school uses pixel notes, holds, hit effects, ratings, countdowns, pause music, and death audio.
Roses uses the scared crowd, and Thorns uses stage distortion and Spirit trails.
School Story Mode includes the source dialogue.
Winter Horrorland includes its lights intro, and Eggnog Erect includes its ending cutscene.
`Week4Assets`, `Week5Assets`, and `Week6Assets` record copied files in their source manifests.
Cross-engine frame and shader parity still requires comparison with a running upstream build.

Week 7 lists Ugh, Guns, and Stress in that order.
Each song includes Easy, Normal, and Hard.
Ugh also includes Erect and Nightmare.
The source does not contain Guns or Stress Erect mixes.
Week 7 adds 11 charts, 7,253 note heads, and four audio variants.
Its instrumentals and separate vocal stems remain unchanged.
Both battlefield stages use the source artwork, positions, parallax, camera targets, and animations.
The original battlefield includes moving clouds, the rotating tank, smoke, and dancing spectators.
Tankman plays the source `ugh` and `hehPrettyGood` note animations.
Ugh Erect includes the source character color adjustments, directional rim lighting, masked Girlfriend lighting, and sniper animations.
Stress uses Boyfriend holding Girlfriend and Pico on the speakers.
Its 546 internal `picospeaker` cues control shooting and randomly selected running tankmen.
This internal animation chart remains separate from selectable difficulties and Pico mixes.
Runners use the source movement, four-sprite pool, shot animations, and flicker timing.
Story Mode plays the three original video cutscenes with their English subtitles before the countdown.
Video streams remain unchanged, and PCM audio preserves the source decoding without another lossy encode.
The cutscene pause menu supports resume, skip, restart, and exit.
Freeplay and retries skip the cutscenes.
Week 7 uses the source death atlases and 25 Tankman game-over quotes.
Pause freezes effects, and retry resets speaker cues, runners, and character state.
`Week7Assets/source-manifest.json` records the original asset and script hashes.

Pico and BF mixes use separate song folders and retain the original campaign entries.
Story Mode plays the installed weeks as sequential campaigns on Easy, Normal, or Hard.
Freeplay and the bundle picker expose Erect and Nightmare where available.

## Rebuild the bundles

Install Python and FFmpeg.
Obtain the pinned `funkin.assets` revision.
Run `python Scripts/ImportVanillaSongs.py --assets "path/to/funkin.assets"` from the project directory.
The importer also accepts the `assets` directory from the matching 0.8.6 Windows build.
Run `python Scripts/ImportVanillaCharacters.py --assets "path/to/funkin.assets"` to rebuild Tutorial and Week 1 visual assets.
Add `--erect-only` to extend existing bundles without regenerating their original audio and charts.
Add `--week2-only` to import only Week 2.
Add `--week3-only` to import only Week 3.
Add `--weeks456-only` to import only Weeks 4 through 6.
Add `--week7-only` to import only Week 7.
The importer overwrites these generated bundles.
Select `Tools > Friday Fight Funkin' > Build Tutorial Character` to regenerate the character animation assets.

## Validation

Set `UNITY_PARTY_SONG_TEST_PATH` to an empty output directory.
Set `FRIDAY_FIGHT_FUNKIN_BUILD_PATH` to the output executable path.
Run Unity with `-batchmode -executeMethod VanillaSongValidation.Begin`.
The probe checks all 166 charts through the gameplay parser.
It rejects controls for swapped note sides, rounded scroll speeds, and incorrect linear camera easing.
The default probe completes the six Week 1 remix charts and the Tutorial and Week 1 Hard charts in autoplay.
It checks note counts, misses, audio selection, opponents, camera events, stage loading, and menu return.
It saves a screenshot for each run.
The probe builds Windows after the gameplay checks pass.
Set `UNITY_PARTY_SKIP_BUILD=1` to run gameplay validation without a Windows build.
Set `UNITY_PARTY_WEEK2_TEST=1` to run the three Week 2 Hard charts and all four remix charts.
Set `UNITY_PARTY_WEEK3_TEST=1` to run the three Week 3 Hard charts and all six remix charts.
Set `UNITY_PARTY_WEEKS456_TEST=1` to check nine original Hard charts and all 14 remix charts.
Set `UNITY_PARTY_WEEK7_TEST=1` to check three original Hard charts and both Ugh remix charts.
Set `UNITY_PARTY_SONG_TEST_START` to skip earlier entries for a focused check.
Set `UNITY_PARTY_SONG_TEST_LIMIT=1` to run only the first chart for a focused regression check.
Set `UNITY_PARTY_SONG_STAGE_ONLY=1` to check stage behavior and menu return without waiting for full song playback.
Run this probe in an isolated batch editor.
The probe uses separate validation preferences.

Run `Scripts/TestFunkinRules.ps1` to simulate all 90,437 note heads at 30, 60, and 144 frames per second.
Run `python Scripts/TestVanillaCharacterAssets.py --assets "path/to/assets"` to verify character files and animation indices.
Set `UNITY_PARTY_CHARACTER_TEST_PATH` to an output directory.
Run `VanillaCharacterValidation.Begin` in an isolated batch editor to check character timing, pause, and retry.
The probe covers Tutorial, both main stages, and Weeks 2 through 6.
Controls reject interrupted idles, incorrect dance intervals, interrupted singing, and duplicate Tutorial characters.
Timing checks cover three controllers at 30, 60, and 144 FPS and 80, 100, and 180 BPM.
They check manual release, automated sustains, misses, held input, tempo changes, and character durations.
Frame-label indices stay within their source animation and skip invalid entries.
The character asset check rejects frames from adjacent animations.
`VanillaCharacterValidation.CheckIdlePoseFrames` checks Dad's idle hold and uses an explicit singing animation as a control.
Run `python Scripts/TestVanillaSongAssets.py --assets "path/to/assets"` to compare the imported remixes with their source files.
The asset check verifies chart data, audio hashes, vocal alignment, stage hashes, and a reversed-offset control.
Run `python Scripts/TestWeek2Assets.py --assets "path/to/assets"` to verify all 13 Week 2 charts and their source assets.
Run Unity with `-batchmode -quit -executeMethod VanillaWeek2Validation.CheckAssets` for parser, playlist, and character mesh checks.
Week 2 controls reject swapped note sides, missing animations, a nonexistent Monster remix, and constant-tempo Monster playback.
Rotated and unrotated opponent frames must retain their original proportions.
Set `UNITY_PARTY_WEEK2_LIFECYCLE_PATH` to an output directory.
Run Unity with `-batchmode -executeMethod VanillaWeek2LifecycleValidation.Begin` in an isolated project copy.
The lifecycle probe checks death animations, the death camera, vocal cleanup, retry state, and menu return.
It captures each death animation and rejects a blank-render control.

Run `python Scripts/TestWeek3Assets.py --assets "path/to/assets"` to verify all 15 Week 3 charts and their source assets.
Week 3 controls reject swapped note sides, Pico variations, premature train movement, missing animations, and blank stage renders.
Set `UNITY_PARTY_WEEK3_TEST=1` and `UNITY_PARTY_WEEK3_LIFECYCLE_PATH` before running `VanillaWeek2LifecycleValidation.Begin` to check Week 3 death and retry.

Run `python Scripts/TestWeeks456Assets.py --assets "path/to/assets"` to verify all 41 new charts and copied assets.
The check rejects swapped note sides, Pico variations, missing remix content, and invalid animation indices.

Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG` to `Satin Panties,Cocoa,Senpai` and `UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH` to an output directory.
Run `VanillaWeek2LifecycleValidation.Begin` to check the three player death atlases and retries.
Set `UNITY_PARTY_PRESENTATION_TEST_PATH` and run `VanillaCampaignPresentationValidation.Begin` to check dialogue and the Eggnog Erect ending.
Presentation controls reject missing canvas meshes and backdrop-only renders.

Run `python Scripts/TestWeek7Assets.py --assets "path/to/assets"` to verify Week 7 charts, source hashes, shooting cues, and cutscenes.
Controls reject swapped note sides, Pico mixes, unavailable remixes, missing animations, and changed media.
Run `VanillaWeek7Validation.CheckAssets` for playlists, meshes, runner frame sizes, and rendered rim lighting.
The rim probe rejects light on internal atlas seams and requires light on the outer silhouette.
Set `UNITY_PARTY_WEEK7_VIDEO_PATH` and run `VanillaWeek7VideoValidation.Begin` in an isolated batch editor.
The video probe checks decoding, subtitles, pause, resume, restart, skip, completion, and retry suppression.
Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG` to `Ugh,Stress` to check both Week 7 death atlases.

The content remains subject to `Vanilla-LICENSE.md`.
The upstream asset license restricts public redistribution.

## Weekend 1

Weekend 1 includes Darnell, Lit Up, 2hot, and Blazin'.
Darnell also includes its Erect and Nightmare charts.
Darnell and Lit Up also include separate BF mixes.
The import adds 14 charts, 10,089 note heads, and five instrumental variants.
Blazin' has no separate vocal track.

The stages use the source Pico, Darnell, Nene, A-Bot, traffic, mist, rain, and combat graphics.
2hot uses the gun preparation window and spray-can hit and miss behavior.
Spray cans, explosions, and casings reuse preloaded graphics and animation meshes.
The gun afterimage expands around the captured frame center without character animation offsets.
Blazin' uses paired combat animations and a centered player strumline.
Story Mode includes the Darnell intro and the 2hot and Blazin' ending videos.
Runtime videos use H.264 with 8-bit YUV 4:2:0 for Windows and Xbox UWP playback.
The importer preserves source files, video dimensions, frame timing, and PCM audio.
Video validation checks decoded frame counts, duration, and SSIM of at least 0.99 against the source.
Pico uses his source pause music and game-over animations.

Add `--weekend1-only` to `Scripts/ImportVanillaSongs.py` to import Weekend 1 separately.
Run `Scripts/ImportVanillaCharacterSelect.py` to import the character-select screen.
Pass `--assets` for the release assets and `--source` for the 0.8.6 checkout.

Run `python Scripts/TestWeekend1Assets.py --assets "path/to/assets"` to check the Weekend 1 source files and charts.
Set `UNITY_PARTY_WEEKEND_TEST=1` to use the Weekend 1 song probes.
Set `UNITY_PARTY_SONG_TEST_PATH` to the probe output directory.
Run `VanillaSongValidation.Begin` in an isolated Unity batch editor.
Set `UNITY_PARTY_SONG_STAGE_ONLY=1` to check loading and stage behavior without full playback.

Set `UNITY_PARTY_CHARACTER_TEST_PATH` before running `VanillaCharacterSelectValidation.Begin`.
Set `UNITY_PARTY_WEEKEND_VIDEO_PATH` before running `VanillaWeekend1PresentationValidation.Begin`.
Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG` to `Darnell,2hot,Blazin'` for the three Pico death paths.
Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH` before running `VanillaWeek2LifecycleValidation.Begin`.

## Pico and BF mixes

All 17 released mixes from version 0.8.6 are included.
Pico has Bopeebo, Fresh, DadBattle, Spookeez, South, Pico, Philly Nice, Blammed, Cocoa, Eggnog, Senpai, Roses, Ugh, Guns, and Stress.
BF has Darnell and Lit Up.
Each mix includes Easy, Normal, and Hard.
The import adds 51 charts and 29,804 playable note heads.
Stress has 584 separate Otis animation cues.

Folders ending in `-Pico` or `-BF` hold the mixes.
Each folder preserves its source chart, metadata, instrumental, and separate player and opponent vocal files.
The compatibility vocal file combines the original stems with Vorbis quality 10.
Mix scores and favorites use their separate titles and paths.
Story Mode selects the original song paths.

The mix importer adds the source characters, alternate animations, masks, companion graphics, dialogue, and cutscene media.
Stress Pico uses the Otis cues, bloody Tankman events, video intro, and scripted ending.
The school Pico mixes use their Freeplay dialogue.
The Week 3 Pico mixes use the doppelganger intro.
An exploded opponent remains hidden and muted on retries.
An exploded player completes the song before the countdown.
Retries preserve the source intro suppression.

Run `python Scripts/ImportVanillaMixes.py --assets "path/to/assets"` after the base song importer.
Add `--extras-only` to import missing characters and refresh presentation assets without rewriting song audio.
Run `python Scripts/TestMixAssets.py --assets "path/to/assets"` to verify source hashes, charts, dependency files, and cutscene media.
The test rejects swapped lanes and original instrumentals used for mix audio.

Set `UNITY_PARTY_MIX_TEST=1` and `UNITY_PARTY_SONG_TEST_PATH` before running `VanillaSongValidation.Begin`.
The probe checks the 44 catalog entries, original Story playlists, all 166 parsed charts, and the mix stages.
Set `UNITY_PARTY_SONG_STAGE_ONLY=1` for stage loading checks.
Clear that variable to check complete playback and event consumption.
The gameplay probe suppresses intros so random cutscene outcomes cannot skip its chart checks.
Run `Scripts/TestVanillaABotAnalyzer.ps1` to compare A-Bot levels with 14 original Haxe fixtures.
The analyzer tests also check silence, stereo cancellation, playhead position, clip boundaries, and retry reset.
Cross-engine frame and shader parity requires comparison with a running upstream build.

Set `UNITY_PARTY_MIX_IDS` to a comma-separated song ID list to select targeted mix probes.
Stage probes also check source event transitions, special animations, vocal restoration, and reset controls.
Run `VanillaMixPresentationValidation.Begin` with `UNITY_PARTY_MIX_PRESENTATION_PATH` to check Freeplay dialogue, video, endings, and randomized intro outcomes.
Run `VanillaWeek2LifecycleValidation.Begin` with `UNITY_PARTY_MIX_TEST=1` and `UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH` to check eight mix death and retry paths.

## Spaghetti

Spaghetti includes Easy, Normal, and Hard in Freeplay, Story Mode, and the bundle picker.
The import preserves 1,701 note heads, 468 events, source scroll speeds, and both original Ogg files.
The final non-scoreable note does not affect score, combo, misses, or result totals.

The diner includes all six performers, the perspective floor, dust, truck lights, pulse lights, and source color adjustments.
Performer events select which characters follow each strumline.
Mouth animations follow the song clock at 24 frames per second.
Sakura retains the joint, BF1, and BF2 hit and miss animations.
Girlfriend retains the alternate singing animations.
Health icons follow the chart events.
Opponent receptors, ratings, and combo popups remain hidden.

The intro plays before the first countdown in each session.
The first advance input arms skipping after half a second.
The second input starts the skip fade.
Retries restore the diner and skip the intro.
The ending displays both source cards and completes the song after nine seconds.
Desktop guitar vibration events have no effect, as in the source desktop build.

Run `Scripts/ImportSpaghetti.py` with `--assets`, `--audio`, and `--source` to import the release files.
Use the release assets directory, the Spaghetti Ogg directory, and the Funkin 0.8.6 source checkout.
Run `Scripts/SpaghettiReference/Build.ps1` with HaxeFlixel 6.2.0 and flixel-animate 1.4.0 installed.
Run `python Scripts/ImportSpaghettiMasks.py` to pack the source renderer's clipped frames.
The masked atlases preserve the original source files alongside their generated graphics.

Run `Scripts/TestSpaghettiAssets.py` with the same three source arguments to check charts, events, audio, masks, and asset hashes.
Run Unity with `-batchmode -quit -executeMethod SpaghettiValidation.CheckAssets` for parser, playlist, atlas, and lighting checks.
Set `UNITY_PARTY_SPAGHETTI_TEST=1` before running `VanillaSongValidation.Begin` in an isolated batch editor.
The probe checks all three difficulties, performer routing, note variants, pause, intro skipping, ending cards, and completion.
Controls reject inactive performers singing, scored ending notes, early cutscene completion, and blank stage renders.

Set `UNITY_PARTY_SPAGHETTI_REFERENCE_PATH` to the absolute `Builds/SpaghettiReference` directory.
Run `SpaghettiRendererValidation.Capture` in Unity batch mode.
Run `python Scripts/TestSpaghettiRenderer.py` to compare 90 Unity poses with the Flixel captures.
Controls reject shifted characters, frozen mouths, and mouths drawn over Yunjin's foreground hair.
The pose comparison uses the installed Flixel renderer.
Full-song pixel parity with the released executable has not been verified.

Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG` to `SPAGHETTI (feat. j-hope of BTS) (Clean ver.)` for death and retry checks.
Set `UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH` before running `VanillaWeek2LifecycleValidation.Begin`.

## Cutscene boundary checks

Set `UNITY_PARTY_CUTSCENE_PATH` before running `VanillaCutsceneBoundaryValidation.Begin` in an isolated batch editor.
The probe checks 15 intros, five outros, and five retry or no-intro controls.
Intro checks require an opaque cover before the loading screen clears.
They also check HUD restoration and reject a transparent cover.
Outro checks require presentation ownership and blocked song completion until the cutscene ends.
The checks preserve the visible stage in in-game outros, including Eggnog Erect and 2hot.
Use `UNITY_PARTY_CUTSCENE_CASES` to select semicolon-separated `song|variation|story|mode` cases.
The story field is `1` or `0`.
Mode is `intro`, `outro`, `retry`, or `control`.
