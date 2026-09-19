import animate.FlxAnimate;
import animate.internal.elements.FlxSpriteElement;
import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import openfl.display.Sprite;
import sys.io.File;

using StringTools;

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
    var body:FlxAnimate;
    var lip:FlxAnimate;
    var index:Int = 0;
    var ticks:Int = 0;
    var cases:Array<Dynamic> = [];
    var metrics:Array<Dynamic> = [];

    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxSprite.defaultAntialiasing = true;
        FlxG.camera.bgColor = 0xFF888888;
        exportMasks("characters/sserafim-gf");
        exportMasks("characters/sserafim-yunjin");
        exportMasks("characters/sserafim-yunjin", "base");
        exportMasks("characters/sserafim-yunjin", "foreground");
        exportMasks("gfGetUp");
        for (name in ["yunjin", "kazuha", "chaewon", "eunchae", "sakura"])
            for (animation in ["idle", "singUP", "singDOWN"])
                for (mouth in [0, 650])
                    cases.push({name:name, animation:animation, mouth:mouth});
        for (animation in ["idle", "singLEFT", "singRIGHT", "singUP", "singDOWN"])
            for (poseFrame in [0, 4, 8, 12])
                for (mouth in [0, 650, 1200])
                    cases.push({name:"yunjin", animation:animation, mouth:mouth, poseFrame:poseFrame});
        prepare();
        FlxG.signals.postDraw.add(function()
        {
            if (++ticks % 3 != 0) return;
            var sample = cases[index];
            var output = Sys.getEnv("UNITY_PARTY_SPAGHETTI_REFERENCE_PATH");
            var image = lime.app.Application.current.window.readPixels();
            File.saveBytes(output + "/" + sample.name + "-" + sample.animation + (sample.poseFrame == null ? "" : "-frame" + sample.poseFrame) + "-" + sample.mouth + ".png", image.encode(PNG));
            if (++index == cases.length)
            {
                File.saveContent(output + "/metrics.json", haxe.Json.stringify(metrics, null, "  "));
                Sys.exit(0);
            }
            prepare();
        });
    }

    function exportMasks(path:String, ?part:String):Void
    {
        var sprite = new FlxAnimate(0, 0, "assets/" + path);
        sprite.anim.addByTimeline("", sprite.library.timeline, 24, false);
        sprite.animation.play("");
        if (part != null)
        {
            @:privateAccess
            for (name in sprite.library.dictionary.keys())
            {
                var timeline = sprite.library.getSymbol(name).timeline;
                var mouthLayer = -1;
                @:privateAccess
                for (i in 0...timeline.layers.length)
                    timeline.layers[i].forEachFrame(frame -> frame.forEachElement(element ->
                    {
                        if (element.elementType == GRAPHIC && element.toSymbolInstance().symbolName == "mouth yunjin") mouthLayer = i;
                    }));
                if (mouthLayer < 0) continue;
                @:privateAccess
                for (i in 0...timeline.layers.length)
                    timeline.layers[i].forEachFrame(frame ->
                    {
                        var above = i < mouthLayer;
                        frame.forEachElement(element ->
                        {
                            element.visible = part == "foreground" ? above : !above;
                            if (element.elementType == GRAPHIC && element.toSymbolInstance().symbolName == "mouth yunjin") above = true;
                        });
                        frame.setDirty();
                    });
            }
        }
        var output = Sys.getEnv("UNITY_PARTY_SPAGHETTI_REFERENCE_PATH") + "/" + path.replace("/", "-") + (part == null ? "" : "-" + part);
        sys.FileSystem.createDirectory(output);
        var frames:Array<String> = [];
        for (i in 0...sprite.animation.curAnim.numFrames)
        {
            sprite.animation.curAnim.curFrame = i;
            sprite.drawFrame(true);
            var bitmap = sprite.framePixels;
            var file = "mask-" + i + ".png";
            File.saveBytes(output + "/" + file, bitmap.encode(bitmap.rect, new openfl.display.PNGEncoderOptions()));
            frames.push(file);
        }
        var bounds = sprite.library.timeline.getWholeBounds(false);
        File.saveContent(output + "/masks.json", haxe.Json.stringify({frames:frames, bounds:[bounds.x, bounds.y, bounds.width, bounds.height]}));
        if (part != null)
        {
            @:privateAccess
            for (name in sprite.library.dictionary.keys())
                sprite.library.getSymbol(name).timeline.forEachLayer(layer -> layer.forEachFrame(frame ->
                {
                    frame.forEachElement(element -> element.visible = true);
                    frame.setDirty();
                }));
        }
        sprite.destroy();
    }

    function prepare():Void
    {
        if (body != null) { remove(body); body.destroy(); }
        var sample = cases[index];
        var path = "assets/characters/sserafim-" + sample.name;
        var data:Dynamic = haxe.Json.parse(openfl.Assets.getText(path + "/character.json"));
        var lips:Dynamic = haxe.Json.parse(openfl.Assets.getText(path + "/lipsync.json"));
        body = new FlxAnimate(0, 0, path);
        lip = new FlxAnimate(0, 0, "assets/lipsync" + (sample.name == "yunjin" ? "-yunjin" : ""));
        lip.anim.addByTimeline("lipsync", lip.library.timeline, 24, false);
        lip.animation.play("lipsync");
        lip.animation.curAnim.curFrame = sample.mouth;
        lip.animation.paused = true;
        lip.flipX = lips.flipX;
        var pose:Dynamic = Reflect.field(lips.poses, sample.animation);
        if (pose != null)
        {
            lip.offset.set(pose.offset[0], pose.offset[1]);
            lip.angle = pose.angle;
        }
        var element = new FlxSpriteElement(lip);
        element.active = false;
        var keyword = sample.name == "yunjin" ? "mouth yunjin" : sample.name == "sakura" ? "mouth edit" : "mouth default";
        @:privateAccess
        for (name in body.library.dictionary.keys())
            if (name.contains(keyword))
                body.library.getSymbol(name).timeline.forEachLayer(layer -> layer.forEachFrame(frame -> frame.add(element)));
        var animations:Array<Dynamic> = data.animations;
        for (animation in animations)
            if (animation.name == sample.animation)
            {
                body.anim.addByFrameLabel(sample.animation, animation.prefix, 24, false);
                body.animation.play(sample.animation);
                body.animation.curAnim.curFrame = sample.poseFrame == null ? 2 : Std.int(Math.min(sample.poseFrame, body.animation.curAnim.numFrames - 1));
                body.animation.paused = true;
            }
        body.x = 640 - body.width / 2;
        body.y = 360 - body.height / 2;
        FlxG.camera.zoom = .65;
        add(body);
        metrics.push({name:sample.name, animation:sample.animation, mouth:sample.mouth, poseFrame:sample.poseFrame,
            x:body.x, y:body.y, width:body.width, height:body.height, frame:body.animation.frameIndex,
            lipWidth:lip.width, lipHeight:lip.height, lipOrigin:[lip.origin.x, lip.origin.y], lipOffset:[lip.offset.x, lip.offset.y]});
    }
}
