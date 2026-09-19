param(
    [string]$AnalyzerPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets/Scripts/VanillaABotAnalyzer.cs'),
    [string]$FixturePath = (Join-Path $PSScriptRoot 'VanillaABotFixtures.json')
)
$ErrorActionPreference = 'Stop'
$runtime = Get-Content -LiteralPath $AnalyzerPath -Raw
$stubs = @"
namespace UnityEngine {
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }
    public enum AudioClipLoadType { DecompressOnLoad, CompressedInMemory, Streaming }
    public class AudioSource { public AudioClip clip; public int timeSamples; }
    public class AudioClip {
        public AudioDataLoadState loadState;
        public AudioClipLoadType loadType;
        public int channels;
        public int samples;
        public float[] data;
        public bool GetData(float[] output, int offset) {
            if (data == null || data.Length == 0) return false;
            for (int i = 0; i < output.Length; i++) output[i] = data[(offset * channels + i) % data.Length];
            return true;
        }
    }
}
"@
Add-Type -TypeDefinition ($runtime + [Environment]::NewLine + $stubs)
$fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
if ($fixture.frames -ne 256 -or $fixture.channels -ne 2) { throw 'Unexpected fixture format.' }
$buffers = @{}
foreach ($item in $fixture.fixtures) {
    $pcm = [float[]]::new(512)
    for ($sample = 0; $sample -lt 256; $sample++) {
        for ($channel = 0; $channel -lt 2; $channel++) {
            $value = switch ($item.kind) {
                'silence' { 0 }
                'dc' { .05 }
                'tone' { $item.amplitude * [Math]::Sin(2 * [Math]::PI * $item.bin * $sample / 256) }
                'opposite-phase' { (1 - 2 * $channel) * [Math]::Sin(2 * [Math]::PI * 14 * $sample / 256) }
                'impulse' { if ($sample -eq 127) { .8 } else { 0 } }
                'samples' { $item.pcm[$sample * 2 + $channel] }
                default { throw "Unknown fixture kind: $($item.kind)" }
            }
            $pcm[$sample * 2 + $channel] = [float]$value
        }
    }
    $buffers[$item.name] = $pcm
    $analyzer = [VanillaABotAnalyzer]::new()
    for ($step = 0; $step -lt 16; $step++) { $analyzer.Analyze($pcm, 2) }
    for ($bar = 0; $bar -lt 7; $bar++) {
        if ([Math]::Abs($analyzer.Levels[$bar] - $item.levels[$bar]) -gt .000001) {
            throw "Haxe parity failed for $($item.name), bar $bar. Expected $($item.levels[$bar]), got $($analyzer.Levels[$bar])."
        }
    }
}
$analyzer = [VanillaABotAnalyzer]::new()
$analyzer.Analyze($buffers['tone14'], 2)
$peak = ($analyzer.Levels | Measure-Object -Maximum).Maximum
if ($peak -gt .100001 -or $peak -lt .099999) { throw 'The first audible update must slew to 0.1.' }
$analyzer.Reset()
if (($analyzer.Levels | Measure-Object -Sum).Sum -ne 0) { throw 'Reset did not clear levels.' }
if ($analyzer.Read($null)) { throw 'Missing audio passed the reader control.' }
$source = [UnityEngine.AudioSource]::new()
$source.clip = [UnityEngine.AudioClip]::new()
$source.clip.loadState = [UnityEngine.AudioDataLoadState]::Loaded
$source.clip.loadType = [UnityEngine.AudioClipLoadType]::Streaming
if ($analyzer.Read($source)) { throw 'Streaming audio passed the unreadable control.' }
$source.clip.loadType = [UnityEngine.AudioClipLoadType]::DecompressOnLoad
$source.clip.channels = 2
$source.clip.samples = 512
$source.clip.data = [float[]]::new(1024)
[Array]::Copy($buffers['tone14'], 0, $source.clip.data, 512, 512)
$source.timeSamples = 256
if (-not $analyzer.Read($source)) { throw 'Loaded PCM was rejected.' }
$expected = [VanillaABotAnalyzer]::new()
$expected.Analyze($buffers['tone14'], 2)
for ($bar = 0; $bar -lt 7; $bar++) {
    if ([Math]::Abs($analyzer.Levels[$bar] - $expected.Levels[$bar]) -gt .000001) { throw 'The reader used the wrong playhead position.' }
}
$analyzer.Reset()
$source.timeSamples = 511
if (-not $analyzer.Read($source)) { throw 'The final PCM frame was rejected.' }
$tail = [float[]]::new(512)
$tail[0] = $source.clip.data[1022]
$tail[1] = $source.clip.data[1023]
$expected.Reset()
$expected.Analyze($tail, 2)
for ($bar = 0; $bar -lt 7; $bar++) {
    if ([Math]::Abs($analyzer.Levels[$bar] - $expected.Levels[$bar]) -gt .000001) { throw 'The reader wrapped past the clip end.' }
}
Write-Output "ABOT ANALYZER PASSED: $($fixture.fixtures.Count) original Haxe fixtures, slew, audible control, stereo cancellation, reset, missing audio, streaming audio, playhead, and clip end."
