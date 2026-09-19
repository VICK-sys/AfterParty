import animate.FlxAnimate;
import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import flixel.graphics.frames.FlxAtlasFrames;
import openfl.display.Sprite;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(1280, 720, ReferenceState, 60, 60, true));
    }
}

class ReferenceState extends FlxState
{
    var objects:Array<FlxSprite> = [];
    var index:Int = 0;
    var ticks:Int = 0;
    var metrics:Array<Dynamic> = [];
    var cases:Array<Dynamic> = [];

    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxSprite.defaultAntialiasing = true;
        FlxG.camera.bgColor = 0xFFFECC5C;
        for (i in 0...12) cases.push({id:i, time:12.0, variant:"intro", safe:false, name:(i < 6 ? "bf" : "pico") + "-" + i % 6});
        cases.push({id:7, time:5.8, variant:"intro", safe:false, name:"pico-flash"});
        cases.push({id:7, time:9.0, variant:"intro fat gf", safe:false, name:"pico-fat"});
        cases.push({id:7, time:9.0, variant:"intro cass", safe:false, name:"pico-cass"});
        cases.push({id:5, time:12.0, variant:"intro", safe:true, name:"bf-safe"});
        cases.push({id:8, time:5.8, variant:"intro", safe:false, name:"pico-great-flash"});
        exportFilters();
        exportMasks();
        prepare();
        FlxG.signals.postDraw.add(function()
        {
            ticks++;
            if (ticks % 5 != 0) return;
            var output = Sys.getEnv("UNITY_PARTY_RESULTS_REFERENCE_PATH");
            var pixels = lime.app.Application.current.window.readPixels();
            File.saveBytes(output + "/" + cases[index].name + ".png", pixels.encode(PNG));
            index++;
            if (index == cases.length)
            {
                File.saveContent(output + "/metrics.json", haxe.Json.stringify(metrics, null, "  "));
                Sys.exit(0);
            }
            prepare();
        });
    }

    function exportFilters():Void
    {
        var sprite = new FlxAnimate(0, 0, "assets/images/results-pico/resultsGOOD");
        var output = Sys.getEnv("UNITY_PARTY_RESULTS_REFERENCE_PATH");
        var entries:Array<Dynamic> = [];
        @:privateAccess
        for (name in sprite.library.dictionary.keys())
        {
            var timeline = sprite.library.getSymbol(name).timeline;
            for (layerIndex in 0...timeline.layers.length)
            {
                var layer = timeline.layers[layerIndex];
                for (frame in layer.frames)
                for (elementIndex in 0...frame.elements.length)
                {
                    var element = frame.elements[elementIndex];
                    if (!Std.isOfType(element, animate.internal.elements.MovieClipInstance)) continue;
                    var movie:animate.internal.elements.MovieClipInstance = cast element;
                    if (movie._filters == null || movie._filters.length == 0) continue;
                    movie._bakeFilters(movie._filters, 0);
                    var baked = movie._bakedFrames.findFrame(0);
                    var bitmap = baked.frame.parent.bitmap;
                    var file = "filter-" + entries.length + ".png";
                    File.saveBytes(output + "/" + file, bitmap.encode(bitmap.rect, new openfl.display.PNGEncoderOptions()));
                    var bounds = movie.getBounds(0);
                    var matrix = baked.matrix;
                    entries.push({key:name + "|" + layerIndex + "|" + frame.index + "|" + elementIndex, file:file,
                        matrix:[matrix.a, matrix.b, matrix.c, matrix.d, matrix.tx, matrix.ty],
                        bounds:[bounds.x, bounds.y, bounds.width, bounds.height]});
                }
            }
        }
        File.saveContent(output + "/filters.json", haxe.Json.stringify(entries, null, "  "));
        sprite.destroy();
    }

    function exportMasks():Void
    {
        var sprite = new FlxAnimate(0, 0, "assets/images/results-bf/resultsPERFECT/tickleFight");
        sprite.anim.addByTimeline("", sprite.library.timeline, 24, false);
        sprite.animation.play("");
        var output = Sys.getEnv("UNITY_PARTY_RESULTS_REFERENCE_PATH");
        var entries:Array<String> = [];
        for (i in 0...sprite.animation.curAnim.numFrames)
        {
            sprite.animation.curAnim.curFrame = i;
            sprite.drawFrame(true);
            var bitmap = sprite.framePixels;
            var file = "mask-" + i + ".png";
            File.saveBytes(output + "/" + file, bitmap.encode(bitmap.rect, new openfl.display.PNGEncoderOptions()));
            entries.push(file);
        }
        var bounds = sprite.library.timeline.getWholeBounds(false);
        File.saveContent(output + "/masks.json", haxe.Json.stringify({frames:entries, bounds:[bounds.x, bounds.y, bounds.width, bounds.height]}, null, "  "));
        sprite.destroy();
    }

    function prepare():Void
    {
        for (object in objects) { remove(object); object.destroy(); }
        objects = [];
        var sample = cases[index];
        var character = sample.id < 6 ? "bf" : "pico";
        var rank = sample.id % 6;
        var player:Dynamic = haxe.Json.parse(openfl.Assets.getText("assets/players/" + character + ".json"));
        var data:Array<Dynamic> = Reflect.field(player.results, ["loss", "good", "great", "excellent", "perfect", "perfectGold"][rank]);
        data.sort(function(a, b) { return a.zIndex - b.zIndex; });
        for (item in data)
        {
            if (item.filter == (sample.safe ? "naughty" : "safe")) continue;
            var path:String = item.assetPath;
            var script:String = item.scriptClass;
            path = switch (script)
            {
                case "BFBedPerfectResults": "resultScreen/results-bf/resultsPERFECT/bed";
                case "BFShitResults": "resultScreen/results-bf/resultsSHIT";
                case "PicoPerfectResults": "resultScreen/results-pico/resultsPERFECT";
                case "PicoGreatResults": "resultScreen/results-pico/resultsGREAT";
                case "PicoGoodResults": "resultScreen/results-pico/resultsGOOD";
                default: path;
            }
            path = StringTools.replace(StringTools.replace(path, "shared:", ""), "resultScreen/", "assets/images/");
            var delay:Float = (rank == 3 ? 97 : 95) / 24 + (item.delay == null ? 0 : item.delay);
            var age = Std.int((sample.time - delay) * 24 + 0.00001);
            if (age < 0) continue;
            if (item.renderType == "sparrow")
            {
                var sprite = new FlxSprite(item.offsets[0], item.offsets[1]);
                sprite.frames = FlxAtlasFrames.fromSparrow(path + ".png", path + ".xml");
                sprite.animation.addByPrefix("idle", "", 24, false);
                sprite.animation.play("idle");
                var length = sprite.animation.curAnim.numFrames;
                if (age >= length && item.loopFrame != null) age = item.loopFrame + (age - length) % (length - item.loopFrame);
                sprite.animation.curAnim.curFrame = age;
                sprite.animation.paused = true;
                objects.push(sprite);
                add(sprite);
                continue;
            }
            var sprite = new FlxAnimate(item.offsets[0], item.offsets[1], path);
            if (script == "PicoGreatResults" || script == "PicoGoodResults")
            {
                scale(sprite, script == "PicoGoodResults" ? "white small" : "white", 10);
                scale(sprite, script == "PicoGoodResults" ? "blacksmall" : "black", 15);
            }
            if (item.scale != null) sprite.scale.set(item.scale, item.scale);
            var start:String = item.startFrameLabel == null ? "" : item.startFrameLabel;
            if (script == "PicoGoodResults") start = sample.variant;
            if (start == "") sprite.anim.addByTimeline("", sprite.library.timeline, 24, false);
            else sprite.anim.addByFrameLabel(start, start, 24, false);
            sprite.animation.play(start);
            var length = sprite.animation.curAnim.numFrames;
            if (age >= length)
            {
                var loopLabel:String = script == "PicoGoodResults" ? "loop" : item.loopFrameLabel;
                if (loopLabel != null)
                {
                    sprite.anim.addByFrameLabel(loopLabel, loopLabel, 24, false);
                    sprite.animation.play(loopLabel, true);
                    age = (age - length) % sprite.animation.curAnim.numFrames;
                }
                else if (item.loopFrame != null) age = item.loopFrame + (age - length) % (length - item.loopFrame);
                else age = length - 1;
            }
            sprite.animation.curAnim.curFrame = age;
            sprite.animation.paused = true;
            metrics.push({character:character, rank:rank, path:path, x:sprite.x, y:sprite.y, width:sprite.width, height:sprite.height, originX:sprite.origin.x, originY:sprite.origin.y, offsetX:sprite.offset.x, offsetY:sprite.offset.y, frame:sprite.animation.frameIndex});
            objects.push(sprite);
            add(sprite);
        }
    }

    function scale(sprite:FlxAnimate, symbol:String, amount:Float):Void
    {
        var element = sprite.library.getSymbol(symbol).timeline.getElementsAtIndex(0)[0];
        var matrix = element.matrix;
        var instance = element.parentFrame.convertToSymbol(0, 1);
        matrix.a += amount;
        matrix.d += amount;
        matrix.tx -= instance.transformationPoint.x * amount + 500;
        matrix.ty -= instance.transformationPoint.y * amount + 500;
    }
}
