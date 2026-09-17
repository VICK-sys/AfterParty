import flixel.FlxGame;
import flixel.FlxState;
import flixel.text.FlxText;
import openfl.display.BitmapData;
import openfl.display.PNGEncoderOptions;
import openfl.display.Sprite;
import openfl.geom.Point;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(1280, 720, FontState, 60, 60, true));
    }
}

class FontState extends FlxState
{
    override public function create():Void
    {
        super.create();
        flixel.FlxSprite.defaultAntialiasing = true;
        var output = Sys.getEnv("UNITY_PARTY_STORY_FONT_PATH");
        if (output == null) throw "Set UNITY_PARTY_STORY_FONT_PATH to the Story Mode asset directory.";
        var atlas = new BitmapData(512, 256, true, 0);
        var glyphs = [];
        var text = new FlxText(0, 0, 0, "X", 32);
        text.setFormat("assets/vcr.ttf", 32);
        text.drawFrame(true);
        var advance = text.textField.textWidth;
        var lineHeight = text.textField.getLineMetrics(0).height;
        for (code in 32...127)
        {
            text.text = String.fromCharCode(code);
            text.drawFrame(true);
            var x = ((code - 32) % 16) * 32;
            var y = Std.int((code - 32) / 16) * 40;
            atlas.copyPixels(text.pixels, text.pixels.rect, new Point(x, y));
            glyphs.push({code:code, x:x, y:y, width:text.pixels.width, height:text.pixels.height});
        }
        File.saveBytes(output + "/vcr32.png", atlas.encode(atlas.rect, new PNGEncoderOptions()));
        File.saveContent(output + "/vcr32.json", haxe.Json.stringify({size:32, advance:advance, lineHeight:lineHeight, glyphs:glyphs}, null, "  ") + "\n");
        Sys.exit(0);
    }
}
