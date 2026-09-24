import funkin.ui.debug.FunkinDebugDisplay;
import openfl.display.Sprite;
import openfl.display.BitmapData;
import openfl.display.PNGEncoderOptions;
import openfl.events.Event;
import openfl.geom.Point;
import openfl.text.TextField;
import openfl.text.TextFormat;
import sys.io.File;

@:access(funkin.ui.debug.FunkinDebugDisplay)
class Main extends Sprite
{
    public function new()
    {
        super();
        var display = new FrozenDisplay(10, 10, 0xFFFFFF);
        display.fps = 60;
        display.fpsPeak = 60;
        display.gcMem = 134217728;
        display.gcMemPeak = 268435456;
        display.taskMem = 805306368;
        display.taskMemPeak = 1073741824;
        display.backgroundOpacity = 0.5;
        var mode = Sys.getEnv("UNITY_PARTY_DEBUG_REFERENCE_MODE");
        display.isAdvanced = mode == "advanced";
        if (display.isAdvanced)
        {
            display.fpsGraph.history = [];
            display.gcMemGraph.history = [];
            display.taskMemGraph.history = [];
            for (i in 0...100) display.updateAdvancedDisplay();
        }
        addChild(display);
        var ticks = 0;
        addEventListener(Event.ENTER_FRAME, function(_)
        {
            if (++ticks != 10) return;
            var output = Sys.getEnv("UNITY_PARTY_DEBUG_REFERENCE_PATH");
            if (mode == "advanced") exportFont(output);
            var bitmap = new BitmapData(1280, 720, false, 0x506478);
            bitmap.draw(this);
            File.saveBytes(output + "/" + mode + ".png", bitmap.encode(bitmap.rect, new PNGEncoderOptions()));
            Sys.exit(0);
        });
    }

    static function exportFont(output:String):Void
    {
        var atlas = new BitmapData(512, 192, true, 0);
        var text = new TextField();
        text.width = 32;
        text.height = 32;
        text.defaultTextFormat = new TextFormat("Monsterrat", 12, 0xFFFFFF, JUSTIFY);
        text.antiAliasType = NORMAL;
        text.sharpness = 100;
        var glyphs = [];
        for (code in 32...127)
        {
            text.text = String.fromCharCode(code);
            var bitmap = new BitmapData(32, 32, true, 0);
            bitmap.draw(text);
            var x = ((code - 32) % 16) * 32;
            var y = Std.int((code - 32) / 16) * 32;
            atlas.copyPixels(bitmap, bitmap.rect, new Point(x, y));
            var advance = text.textWidth;
            if (code == 32)
            {
                text.text = "| |";
                var spaced = text.textWidth;
                text.text = "||";
                advance = spaced - text.textWidth;
            }
            glyphs.push({x:x, y:y, advance:advance});
        }
        var kerning = [];
        text.width = 100;
        for (first in 33...127)
            for (second in 33...127)
            {
                text.text = String.fromCharCode(first) + String.fromCharCode(second);
                var offset = text.textWidth - glyphs[first - 32].advance - glyphs[second - 32].advance;
                if (offset != 0) kerning.push({pair:first * 128 + second, offset:offset});
            }
        File.saveBytes(output + "/font.png", atlas.encode(atlas.rect, new PNGEncoderOptions()));
        File.saveContent(output + "/font.json", haxe.Json.stringify({glyphs:glyphs, kerning:kerning}));
    }
}

class FrozenDisplay extends FunkinDebugDisplay
{
    override public function __enterFrame(deltaTime:Float):Void {}
}
