import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import flixel.text.FlxText;
import funkin.ui.AtlasText;
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
    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxSprite.defaultAntialiasing = true;
        FlxG.camera.bgColor = 0xFF808080;
        var bg = new FlxSprite().makeGraphic(1280, 720, 0xFF000000);
        bg.alpha = 0.6;
        add(bg);
        var labels = ["Resume", "Restart Song", "Change Difficulty", "Enable Practice Mode", "Exit to Menu"];
        for (index in 0...labels.length)
        {
            var text = new AtlasText(90 + index * 26, 720 * 0.48 + index * 156, labels[index], AtlasFont.BOLD);
            text.alpha = index == 0 ? 1 : 0.6;
            for (letter in text)
            {
                letter.width *= 2;
                letter.height *= 2;
                letter.animation.paused = true;
                letter.animation.curAnim.curFrame = 0;
            }
            add(text);
        }
        var metadata = ["Bopeebo", "Artist: Kawai Sprite", "Difficulty: Normal", "1 Blue Balls"];
        for (index in 0...metadata.length)
            label(metadata[index], 20, 20 + index * 32, 1240, 32);
        var offset = label("Global Offset: 0ms", 20, 0, 1250, 16);
        offset.y = 720 - (offset.height + offset.height + 40) + 5;
        var info = label("Hold SHIFT-UP/DOWN,\nto change the offset.", 20, offset.y + offset.height + 4, 1250, 16);
        var ticks = 0;
        FlxG.signals.postDraw.add(function()
        {
            ticks++;
            if (ticks != 20) return;
            var output = Sys.getEnv("UNITY_PARTY_PAUSE_REFERENCE_PATH");
            var pixels = lime.app.Application.current.window.readPixels();
            File.saveBytes(output + "/standard-reference.png", pixels.encode(PNG));
            File.saveContent(output + "/metrics.json", haxe.Json.stringify({offsetY:offset.y, offsetHeight:offset.height, infoY:info.y}, null, "  "));
            Sys.exit(0);
        });
    }

    function label(value:String, x:Float, y:Float, width:Float, size:Int):FlxText
    {
        var text = new FlxText(x, y, width, value);
        text.setFormat("assets/vcr.ttf", size, 0xFFFFFFFF, RIGHT);
        add(text);
        return text;
    }
}
