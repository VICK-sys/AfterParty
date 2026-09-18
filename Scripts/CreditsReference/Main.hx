import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxState;
import flixel.text.FlxText;
import flixel.addons.transition.TransitionFade;
import flixel.addons.transition.TransitionData;
import flixel.math.FlxPoint;
import flixel.math.FlxRect;
import funkin.ui.credits.CreditsState;
import funkin.ui.credits.CreditsDataHandler;
import openfl.display.Sprite;
import openfl.geom.Point;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(1280, 720, ReferenceState, 60, 60, true));
    }
}

@:access(flixel.text.FlxText)
@:access(funkin.ui.credits.CreditsState)
@:access(flixel.addons.transition.TransitionFade)
class ReferenceState extends FlxState
{
    var credits:CreditsState;
    var phase = 0;
    var ticks = 0;
    var output:String;
    var allEntries:Array<Dynamic>;
    var fade:TransitionFade;

    override public function create():Void
    {
        super.create();
        output = Sys.getEnv('UNITY_PARTY_CREDITS_REFERENCE_PATH');
        var root = Sys.getEnv('UNITY_PARTY_ROOT');
        var creditPath = root + '/Assets/Resources/VanillaCredits/credits.json';
        if (!sys.FileSystem.exists(creditPath)) creditPath = root + '/Assets/VanillaMenu/credits.json';
        var original:Dynamic = haxe.Json.parse(File.getContent(creditPath));
        var party:Dynamic = haxe.Json.parse(File.getContent(root + '/Assets/Resources/VanillaCredits/unity-party.json'));
        allEntries = (cast original.entries:Array<Dynamic>).concat(cast party.entries);
        CreditsDataHandler.CREDITS_DATA = cast {entries:allEntries};
        FlxG.mouse.visible = false;
        flixel.FlxSprite.defaultAntialiasing = true;
        FlxG.camera.bgColor = 0xFF000000;
        FlxG.sound.volume = 0;
        if (Sys.getEnv('UNITY_PARTY_CREDITS_BAKE') == '1') bake();
        for (cover in [true, false])
        {
            var transition = createFade(cover);
            var pixels = transition.back.pixels;
            File.saveBytes(output + '/raw/fade-' + (cover ? 'cover' : 'reveal') + '.png',
                pixels.encode(pixels.rect, new openfl.display.PNGEncoderOptions()));
            transition.destroy();
        }
        makeCredits(1280);
        FlxG.signals.postDraw.add(captureNext);
    }

    function bake():Void
    {
        var lines:Array<Dynamic> = [];
        var index = 0;
        for (entry in allEntries)
        {
            var values:Array<Dynamic> = [];
            if (entry.header != null) values.push({text:entry.header, header:true});
            if (entry.body != null)
                for (body in (cast entry.body:Array<Dynamic>)) values.push({text:body.line, header:false});
            for (value in values)
            {
                var text = new FlxText();
                text.text = value.text;
                text.bold = value.header;
                text.setFormat('Consolas', value.header ? 32 : 24, 0xFFFFFFFF, LEFT, OUTLINE, 0xFF000000, true);
                var variants:Array<Dynamic> = [];
                var last = '';
                for (width in 1280...1601)
                {
                    text.fieldWidth = (width - 48) / 2;
                    var signature = '';
                    for (i in 0...text.textField.numLines) signature += text.textField.getLineText(i) + '\n';
                    if (signature == last) continue;
                    last = signature;
                    text.regenGraphic();
                    var pixels = text.graphic.bitmap;
                    var bounds = pixels.getColorBoundsRect(0xFF000000, 0, false);
                    var cropped = new openfl.display.BitmapData(Std.int(Math.max(1,bounds.width)), Std.int(Math.max(1,bounds.height)), true, 0);
                    cropped.copyPixels(pixels, bounds, new Point());
                    var filename = 'line-' + index + '-' + width + '.png';
                    File.saveBytes(output + '/raw/' + filename, cropped.encode(cropped.rect, new openfl.display.PNGEncoderOptions()));
                    variants.push({minWidth:width, file:filename, trimX:bounds.x, trimY:bounds.y,
                        height:text.height, numLines:text.textField.numLines});
                    cropped.dispose();
                }
                lines.push({text:value.text, header:value.header, endEntry:value == values[values.length - 1], variants:variants});
                text.destroy();
                index++;
            }
        }
        File.saveContent(output + '/raw/lines.json', haxe.Json.stringify({lines:lines}, null, '  '));
    }

    function makeCredits(width:Int):Void
    {
        if (credits != null) { remove(credits); credits.destroy(); }
        lime.app.Application.current.window.resize(width, 720);
        FlxG.scaleMode = new flixel.system.scaleModes.StageSizeScaleMode();
        FlxG.resizeGame(width, 720);
        FlxG.camera.setSize(width, 720);
        FlxG.camera.setPosition(0, 0);
        credits = new CreditsState();
        credits.create();
        credits.active = false;
        add(credits);
        ticks = 0;
    }

    function step(count:Int):Void
    {
        for (_ in 0...count) credits.update(1.0 / 60);
    }

    function captureNext():Void
    {
        ticks++;
        if (ticks < 4) return;
        ticks = 0;
        switch (phase)
        {
            case 0: step(300);
            case 1: capture('opening-1280'); step(1);
            case 2: capture('fractional-1280'); step(899);
            case 3: capture('middle-1280'); step(2400);
            case 4: capture('later-1280'); makeCredits(1600); step(1200);
            case 5: capture('middle-1600'); step(2400);
            case 6: capture('later-1600'); step(2040);
            case 7: capture('party-1600'); makeCredits(1440); step(1200);
            case 8: capture('middle-1440'); step(2400);
            case 9: capture('later-1440'); makeCredits(1280); step(5640);
            case 10: capture('party-1280'); showFade(true);
            case 11: captureFade('fade-cover-1280'); remove(fade); fade.destroy(); showFade(false);
            case 12: captureFade('fade-reveal-1280'); Sys.exit(0);
        }
        phase++;
    }

    function createFade(cover:Bool):TransitionFade
    {
        return new TransitionFade(new TransitionData(FADE, 0xFF000000, cover ? 0.7 : 1,
            new FlxPoint(0, cover ? 1 : -1), null, new FlxRect(-200, -200, FlxG.width * 1.4, FlxG.height * 1.4)));
    }

    function showFade(cover:Bool):Void
    {
        credits.visible = false;
        FlxG.camera.bgColor = 0xFFFFFFFF;
        fade = createFade(cover);
        var values:flixel.addons.transition.TransitionFade.TweenEndValues = {};
        fade.setTweenValues(cover, 0, cover ? 1 : -1, fade.back, values);
        fade.back.y = (fade.back.y + values.y) / 2;
        add(fade);
    }

    function captureFade(name:String):Void
    {
        var pixels = lime.app.Application.current.window.readPixels();
        File.saveBytes(output + '/' + name + '.png', pixels.encode(PNG));
    }

    function capture(name:String):Void
    {
        var pixels = lime.app.Application.current.window.readPixels();
        File.saveBytes(output + '/' + name + '.png', pixels.encode(PNG));
        var visible = [];
        credits.creditsGroup.forEachExists(function(line)
        {
            var text:FlxText = cast line;
            visible.push({text:text.text, x:text.x, y:text.y, height:text.height, numLines:text.textField.numLines});
        });
        File.saveContent(output + '/' + name + '.json', haxe.Json.stringify({groupY:credits.creditsGroup.y,
            nextY:credits.creditsLineY, remaining:credits.entriesToBuild.length, visible:visible}, null, '  '));
    }
}
