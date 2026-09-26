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
        var output = Sys.getEnv("UNITY_PARTY_FREEPLAY_FONT_PATH");
        if (output == null) throw "Set UNITY_PARTY_FREEPLAY_FONT_PATH.";
        var reference = Sys.getEnv("UNITY_PARTY_FREEPLAY_REFERENCE_PATH");
        if (reference == null) throw "Set UNITY_PARTY_FREEPLAY_REFERENCE_PATH.";
        for (entry in [{name:"vcr48", font:"vcr", size:48}, {name:"5by7-32", font:"5by7", size:32}])
        {
            var atlas = new BitmapData(1024, 512, true, 0);
            var glyphs = [];
            var text = new FlxText(0, 0, 0, "X", entry.size);
            text.setFormat("assets/" + entry.font + ".ttf", entry.size);
            text.antialiasing = true;
            text.drawFrame(true);
            var advance = text.textField.textWidth;
            var lineHeight = text.textField.getLineMetrics(0).height;
            for (code in 32...127)
            {
                text.text = String.fromCharCode(code);
                text.drawFrame(true);
                var x = ((code - 32) % 16) * 64;
                var y = Std.int((code - 32) / 16) * 80;
                atlas.copyPixels(text.pixels, text.pixels.rect, new Point(x, y));
                glyphs.push({code:code, x:x, y:y, width:text.pixels.width, height:text.pixels.height, advance:text.textField.textWidth});
            }
            if (entry.size == 32)
            {
                File.saveBytes(output + "/" + entry.name + ".png", atlas.encode(atlas.rect, new PNGEncoderOptions()));
                File.saveContent(output + "/" + entry.name + ".json", haxe.Json.stringify({advance:advance, lineHeight:lineHeight, glyphs:glyphs}, null, "  ") + "\n");
            }
            for (sample in (entry.size == 48 ? ["FREEPLAY", "OFFICIAL OST"] : ["Press [ TAB ] to change characters", "Press [ SPACE ] to change characters"]))
            {
                text.fieldWidth = sample == "FREEPLAY" ? 0 : 1264;
                text.alignment = entry.size == 32 ? CENTER : sample == "FREEPLAY" ? LEFT : RIGHT;
                text.text = sample;
                text.drawFrame(true);
                File.saveBytes(reference + "/reference-" + sample.split(" ").join("_") + ".png", text.pixels.encode(text.pixels.rect, new PNGEncoderOptions()));
                if (entry.size == 48)
                {
                    var name = sample == "FREEPLAY" ? "heading" : "ost";
                    File.saveBytes(output + "/" + name + ".png", text.pixels.encode(text.pixels.rect, new PNGEncoderOptions()));
                    var pixels = text.pixels.clone();
                    for (y in 0...pixels.height)
                        for (x in 0...pixels.width)
                            if (text.pixels.getPixel32(x, y) >>> 24 == 0
                                && (text.pixels.getPixel32(x - 2, y) >>> 24 != 0
                                || text.pixels.getPixel32(x + 2, y) >>> 24 != 0
                                || text.pixels.getPixel32(x, y - 2) >>> 24 != 0
                                || text.pixels.getPixel32(x, y + 2) >>> 24 != 0)) pixels.setPixel32(x, y, 0xFFFFFFFF);
                    File.saveBytes(output + "/" + name + "-bold.png", pixels.encode(pixels.rect, new PNGEncoderOptions()));
                }
            }
        }
        Sys.exit(0);
    }
}
