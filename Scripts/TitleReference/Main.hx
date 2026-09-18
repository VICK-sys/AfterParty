import animate.FlxAnimate;
import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
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
        FlxG.camera.bgColor = 0xFF000000;
        var logo = new FlxSprite(-150, -100);
        logo.frames = Paths.getSparrowAtlas('logoBumpin');
        logo.animation.addByPrefix('bump', 'logo bumpin', 24);
        logo.animation.play('bump');
        logo.animation.paused = true;
        logo.updateHitbox();
        add(logo);
        var gf = new FlxSprite(512, 50.4);
        gf.frames = Paths.getSparrowAtlas('gfDanceTitle');
        gf.animation.addByIndices('dance', 'gfDance', [15,16,17,18,19,20,21,22,23,24,25,26,27,28,29], '', 24, false);
        gf.animation.play('dance');
        gf.animation.paused = true;
        add(gf);
        var prompt = new FlxAnimate(100, 576, 'assets/title-screen-text');
        prompt.anim.addByFrameLabel('idle', 'Idle', 24);
        prompt.anim.addByFrameLabel('press', 'Confirm', 24);
        prompt.animation.play('idle');
        prompt.animation.paused = true;
        prompt.updateHitbox();
        add(prompt);
        var bg = new FlxSprite().makeGraphic(1280, 720, 0xFF000000);
        add(bg);
        var texts = [];
        for (i in 0...2)
        {
            var text = new AtlasText(0, 0, i == 0 ? 'The' : 'Funkin Crew Inc', AtlasFont.BOLD);
            text.screenCenter(X);
            text.y += i * 60 + 200;
            for (letter in text) letter.animation.paused = true;
            texts.push(text);
            add(text);
        }
        var ticks = 0;
        FlxG.signals.postDraw.add(function()
        {
            ticks++;
            if (ticks == 20)
            {
                capture('credits-reference.png');
                bg.visible = false;
                for (text in texts) text.visible = false;
            }
            if (ticks == 25)
            {
                capture('title-reference.png');
                prompt.animation.play('press');
                prompt.animation.paused = true;
            }
            if (ticks == 30)
            {
                capture('confirm-reference.png');
                File.saveContent(Sys.getEnv('UNITY_PARTY_TITLE_REFERENCE_PATH') + '/metrics.json', haxe.Json.stringify({promptWidth:prompt.width, promptHeight:prompt.height, promptOffsetX:prompt.offset.x, promptOffsetY:prompt.offset.y, creditX:texts[1].x, creditWidth:texts[1].width}, null, '  '));
                Sys.exit(0);
            }
        });
    }

    function capture(name:String):Void
    {
        var pixels = lime.app.Application.current.window.readPixels();
        File.saveBytes(Sys.getEnv('UNITY_PARTY_TITLE_REFERENCE_PATH') + '/' + name, pixels.encode(PNG));
    }
}
