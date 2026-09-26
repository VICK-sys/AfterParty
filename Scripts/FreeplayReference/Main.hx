import flixel.FlxGame;
import flixel.FlxState;
import flixel.FlxG;
import flixel.text.FlxText;
import flixel.math.FlxRect;
import flixel.addons.display.FlxRuntimeShader;
import openfl.display.Sprite;
import openfl.filters.GlowFilter;
import openfl.filters.BitmapFilterQuality;
import openfl.display.BlendMode;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(640, 120, ReferenceState, 60, 60, true));
    }
}

class ReferenceState extends FlxState
{
    var cases:Array<Dynamic> = [];
    var index = 0;
    var ticks = 0;
    var front:FlxText;
    var back:FlxText;

    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxG.camera.bgColor = 0xFF121824;
        for (text in ["Tutorial", "DadBattle", "Winter Horrorland", "SPAGHETTI (feat. j-hope)"])
            for (style in ["bf", "pico"])
                for (selected in [true, false]) cases.push({text:text, style:style, selected:selected});
        for (style in ["bf", "pico"])
            for (phase in ["first", "white", "dim"]) cases.push({text:"Tutorial", style:style, selected:true, phase:phase});
        for (style in ["bf", "pico"])
            for (selected in [true, false]) cases.push({text:"Winter Horrorland", style:style, selected:selected, clipWidth:91});
        var nativeCases = cases.copy();
        cases = [];
        for (scale in [1.0, 1.5])
            for (fractional in [false, true])
                for (item in nativeCases)
                {
                    var sample = Reflect.copy(item);
                    sample.scale = scale;
                    sample.x = fractional ? 20.37 : 20;
                    sample.y = fractional ? 35.6 : 35;
                    cases.push(sample);
                }
        prepare();
        FlxG.signals.postDraw.add(function()
        {
            if (++ticks < 8) return;
            var pixels = lime.app.Application.current.window.readPixels();
            File.saveBytes(Sys.getEnv("FREEPLAY_REFERENCE_OUTPUT") + "/title-" + index + ".png", pixels.encode(PNG));
            if (++index == cases.length)
            {
                File.saveContent(Sys.getEnv("FREEPLAY_REFERENCE_OUTPUT") + "/cases.json", haxe.Json.stringify(cases));
                Sys.exit(0);
            }
            prepare();
        });
    }

    function prepare():Void
    {
        if (front != null) { remove(front); front.destroy(); }
        if (back != null) { remove(back); back.destroy(); }
        var item = cases[index];
        ticks = 0;
        if (FlxG.stage.window.width != Std.int(640 * item.scale))
            FlxG.stage.window.resize(Std.int(640 * item.scale), Std.int(120 * item.scale));
        var color = item.style == "pico" ? 0xFFCC6600 : 0xFF00CCFF;
        back = new FlxText(item.x, item.y, 0, item.text, 32);
        back.font = "assets/5by7.ttf";
        back.antialiasing = true;
        var shader = new FlxRuntimeShader(File.getContent(Sys.getEnv("FREEPLAY_BLUR_SHADER")));
        shader.setFloat("_amount", 1.0);
        back.shader = shader;
        back.color = color;
        back.visible = item.selected;
        add(back);
        front = new FlxText(item.x, item.y, 0, item.text, 32);
        front.font = "assets/5by7.ttf";
        front.antialiasing = true;
        front.textField.filters = [new GlowFilter(color, 1, 5, 5, 210, BitmapFilterQuality.MEDIUM)];
        front.alpha = item.selected ? 1 : .6;
        if (item.phase != null)
        {
            var tint = item.phase == "white" ? 0xFFFFFFFF : 0xFFDDDDDD;
            front.color = tint;
            front.textField.filters = [new GlowFilter(tint, 1, 5, 5, 210, BitmapFilterQuality.MEDIUM)];
            if (item.phase == "white") back.color = 0xFFFFFFFF;
            if (item.phase != "first") front.blend = back.blend = BlendMode.ADD;
        }
        if (item.clipWidth != null)
        {
            front.clipRect = new FlxRect(0, 0, item.clipWidth, front.height);
            back.clipRect = new FlxRect(0, 0, item.clipWidth, back.height);
        }
        add(front);
    }
}
