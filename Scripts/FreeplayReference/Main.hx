import flixel.FlxGame;
import flixel.FlxState;
import flixel.FlxG;
import flixel.text.FlxText;
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
        prepare();
        FlxG.signals.postDraw.add(function()
        {
            if (++ticks % 3 != 0) return;
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
        var color = item.style == "pico" ? 0xFFCC6600 : 0xFF00CCFF;
        back = new FlxText(20, 35, 0, item.text, 32);
        back.font = "assets/5by7.ttf";
        var shader = new FlxRuntimeShader(File.getContent(Sys.getEnv("FREEPLAY_BLUR_SHADER")));
        shader.setFloat("_amount", 1.0);
        back.shader = shader;
        back.color = color;
        back.visible = item.selected;
        add(back);
        front = new FlxText(20, 35, 0, item.text, 32);
        front.font = "assets/5by7.ttf";
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
        add(front);
    }
}
