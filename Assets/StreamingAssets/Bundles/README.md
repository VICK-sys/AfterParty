# AfterParty Tutorial and Weeks 1 through 6

These bundles contain the original Tutorial, Bopeebo, Fresh, and DadBattle charts from Friday Night Funkin' 0.8.6.
Each song includes Easy, Normal, and Hard.
Bopeebo, Fresh, and DadBattle also include Erect and Nightmare.
The song picker lists Tutorial and Weeks 1 through 6 in that order.
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

Pico variations are not included.
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
The importer overwrites these generated bundles.
Select `Tools > AfterParty > Build Tutorial Character` to regenerate the character animation assets.

## Validation

Set `UNITY_PARTY_SONG_TEST_PATH` to an empty output directory.
Set `AFTERPARTY_BUILD_PATH` to the output executable path.
Run Unity with `-batchmode -executeMethod VanillaSongValidation.Begin`.
The probe checks all 87 charts through the gameplay parser.
It rejects controls for swapped note sides, rounded scroll speeds, and incorrect linear camera easing.
The default probe completes the six Week 1 remix charts and the Tutorial and Week 1 Hard charts in autoplay.
It checks note counts, misses, audio selection, opponents, camera events, stage loading, and menu return.
It saves a screenshot for each run.
The probe builds Windows after the gameplay checks pass.
Set `UNITY_PARTY_SKIP_BUILD=1` to run gameplay validation without a Windows build.
Set `UNITY_PARTY_WEEK2_TEST=1` to run the three Week 2 Hard charts and all four remix charts.
Set `UNITY_PARTY_WEEK3_TEST=1` to run the three Week 3 Hard charts and all six remix charts.
Set `UNITY_PARTY_WEEKS456_TEST=1` to check nine original Hard charts and all 14 remix charts.
Set `UNITY_PARTY_SONG_TEST_START` to skip earlier entries for a focused check.
Set `UNITY_PARTY_SONG_TEST_LIMIT=1` to run only the first chart for a focused regression check.
Set `UNITY_PARTY_SONG_STAGE_ONLY=1` to check stage behavior and menu return without waiting for full song playback.
Run this probe in an isolated batch editor.
The probe uses separate validation preferences.

Run `Scripts/TestFunkinRules.ps1` to simulate all 41,590 note heads at 30, 60, and 144 frames per second.
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

The content remains subject to `Vanilla-LICENSE.md`.
The upstream asset license restricts public redistribution.
