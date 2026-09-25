import animate.FlxAnimate;
import animate.FlxAnimateFrames;
import flixel.FlxGame;
import flixel.FlxState;
import openfl.display.Sprite;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(2048, 1024, ReferenceState, 60, 60, true));
    }
}

@:access(flixel.FlxCamera)
class ReferenceState extends FlxState
{
    var cases:Array<Dynamic> = [];
    var index:Int = 0;
    var ticks:Int = 0;
    var sample:FlxAnimate;
    var sampleKey:String;

    override public function create():Void
    {
        super.create();
        #if legacy_resolution
        for (name in (Sys.getEnv("FACE_OVERLAY_ONLY") == "1" || Sys.getEnv("NENE_REFERENCE_ONLY") == "1" ? [] : ["lockedChill", "lock"]))
        {
            var sprite = new FlxAnimate();
            sprite.frames = FlxAnimateFrames.fromAnimate("assets/" + name, {swfMode:true});
            sprite.anim.addByTimeline("", sprite.library.timeline, 24, false);
            sprite.animation.play("");
            var output = Sys.getEnv("UNITY_PARTY_CHARACTER_REFERENCE_PATH") + "/" + name;
            sys.FileSystem.createDirectory(output);
            var entries:Array<Dynamic> = [];
            for (i in 0...sprite.animation.curAnim.numFrames)
            {
                sprite.animation.curAnim.curFrame = i;
                sprite.drawFrame(true);
                var whole = sprite.library.timeline.getWholeBounds(false);
                var bitmap = sprite.framePixels;
                var drawnBounds = new openfl.geom.Rectangle(whole.x, whole.y, whole.width, whole.height);
                var crop = bitmap.getColorBoundsRect(0xFF000000, 0, false);
                if (crop.width == 0 || crop.height == 0) crop.setTo(0, 0, 1, 1);
                var cropped = new openfl.display.BitmapData(Std.int(crop.width), Std.int(crop.height), true, 0);
                cropped.copyPixels(bitmap, crop, new openfl.geom.Point());
                var file = "frame-" + i + ".png";
                File.saveBytes(output + "/" + file, cropped.encode(cropped.rect, new openfl.display.PNGEncoderOptions()));
                entries.push({file:file, x:drawnBounds.x + crop.x - whole.x, y:drawnBounds.y + crop.y - whole.y});
                cropped.dispose();
            }
            var bounds = sprite.library.timeline.getWholeBounds(false);
            File.saveContent(output + "/frames.json", haxe.Json.stringify({frames:entries, bounds:[bounds.x, bounds.y, bounds.width, bounds.height]}));
            sprite.destroy();
        }
        #end
        flixel.FlxG.camera.bgColor = 0xFF808080;
        flixel.FlxG.mouse.visible = false;
        if (Sys.getEnv("FACE_OVERLAY_ONLY") != "1") for (name in (Sys.getEnv("NENE_REFERENCE_ONLY") == "1" ? ["neneChill"] : ["lockedChill", "bfChill", "picoChill", "neneChill"]))
            for (frame in (name == "lockedChill" ? [0, 10, 27, 29, 34, 109] : name == "neneChill" ? [0, 7, 15, 29, 30, 38, 46, 47, 51] : [0, 15, 17, 22, 28, 30, 35]))
                cases.push({name:name, frame:frame});
        if (Sys.getEnv("FACE_OVERLAY_ONLY") != "1") for (name in (Sys.getEnv("NENE_REFERENCE_ONLY") == "1" ? ["neneChill"] : ["bfChill", "picoChill", "neneChill"]))
            for (frame in 0...(name == "bfChill" ? 56 : name == "neneChill" ? 52 : 79))
                for (backdrop in ["black", "white"])
                    cases.push({name:name, frame:frame, backdrop:backdrop});
        for (frame in 0...54)
            for (backdrop in ["black", "white"])
                cases.push({name:"gfChill", frame:frame, backdrop:backdrop});
        for (name in ["bfChill", "gfChill", "picoChill", "neneChill"])
        {
            sys.FileSystem.createDirectory(Sys.getEnv("UNITY_PARTY_CHARACTER_REFERENCE_PATH") + "/" + name);
            var start = name == "gfChill" ? 54 : name == "neneChill" ? 47 : 28;
            var end = name == "gfChill" ? 106 : name == "neneChill" ? 52 : name == "bfChill" ? 46 : 38;
            for (frame in start...end)
                for (pass in ["base", "overlay", "front"])
                    for (backdrop in ["black", "white"])
                        cases.push({name:name, frame:frame, backdrop:backdrop, pass:pass});
        }
        for (frame in 47...52)
            for (backdrop in ["black", "white"])
                cases.push({name:"neneChill", frame:frame, backdrop:backdrop, pass:"scene"});
        if (Sys.getEnv("FACE_SCENE_ONLY") == "1") cases = cases.filter(item -> item.pass == "scene");
        sys.FileSystem.createDirectory(Sys.getEnv("UNITY_PARTY_CHARACTER_REFERENCE_PATH") + "/neneChill");
        if (Sys.getEnv("FACE_FRONT_ONLY") == "1") cases = cases.filter(item -> item.pass == "front");
        #if legacy_resolution
        cases = cases.filter(item -> item.name == "lockedChill");
        #else
        cases = cases.filter(item -> item.name != "lockedChill");
        #end
        if (cases.length == 0) Sys.exit(0);
        flixel.FlxG.signals.preDraw.add(function() { animate.internal.elements.AtlasInstance.overlaySeen = false; });
        prepare();
        flixel.FlxG.signals.postDraw.add(function()
        {
            if (++ticks % 3 != 0) return;
            var item = cases[index];
            var pixels = lime.app.Application.current.window.readPixels();
            var file = item.backdrop == null ? item.name + "-" + item.frame : item.name + "/" + (item.pass == null ? "raw" : item.pass) + "-" + item.frame + "-" + item.backdrop;
            File.saveBytes(Sys.getEnv("UNITY_PARTY_CHARACTER_REFERENCE_PATH") + "/" + file + ".png", pixels.encode(PNG));
            if (++index == cases.length) Sys.exit(0);
            prepare();
        });
    }
    function prepare():Void
    {
        var item = cases[index];
        flixel.FlxG.camera.bgColor = item.backdrop == "black" ? 0xFF000000 : item.backdrop == "white" ? 0xFFFFFFFF : 0xFF808080;
        animate.internal.elements.AtlasInstance.overlayPass = (item.pass == "base" || item.pass == "scene") ? 1 : item.pass == "overlay" ? 2 : item.pass == "front" ? 3 : 0;
        var key = item.name + ":" + item.pass + ":" + (item.backdrop != null);
        if (sample == null || sampleKey != key)
        {
            if (sample != null) { remove(sample); sample.destroy(); }
            sampleKey = key;
            sample = new FlxAnimate();
            sample.applyStageMatrix = true;
            sample.frames = FlxAnimateFrames.fromAnimate("assets/" + item.name, {swfMode:true});
            if (item.name == "neneChill" && item.backdrop != null && item.pass != "scene")
                for (layer in sample.library.timeline.layers) layer.visible = layer.name == "Nene";
            sample.anim.addByTimeline("", sample.library.timeline, 24, false);
            sample.animation.play("");
            sample.antialiasing = true;
            add(sample);
        }
        sample.animation.curAnim.curFrame = item.frame;
        sample.animation.paused = true;
    }
}
