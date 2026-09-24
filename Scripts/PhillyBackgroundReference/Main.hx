import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import flixel.addons.display.FlxBackdrop;
import flixel.addons.display.FlxTiledSprite;
import flixel.graphics.frames.FlxAtlasFrames;
import openfl.display.BitmapData;
import openfl.display.BlendMode;
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
    static var capture = 0;
    var age:Float = 0;
    var output:String;
    var prefix:String;

    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxG.camera.bgColor = 0xFF000000;
        FlxSprite.defaultAntialiasing = true;
        var root = Sys.getEnv("PHILLY_REFERENCE_BUNDLES") + "/Week8Assets";
        output = Sys.getEnv("PHILLY_REFERENCE_OUTPUT");
        var stage = Sys.getEnv("PHILLY_REFERENCE_STAGE");
        if (stage == null) stage = "phillyStreetsErect";
        var erect = stage == "phillyStreetsErect";
        var scenarios = [[2050.0, 900.0, 0.77, 0.0], [1450.0, 850.0, 0.85, 37.0], [1800.0, 1000.0, 0.65, 95.0]];
        var cameraOverride = Sys.getEnv("PHILLY_REFERENCE_CAMERA");
        if (cameraOverride != null) scenarios[0] = cameraOverride.split(",").map(Std.parseFloat);
        var scenario = scenarios[Std.int(capture / 2)];
        var skyOnly = capture % 2 == 1;
        var time = scenario[3];
        prefix = stage + "-" + Std.int(capture / 2) + (skyOnly ? "-sky" : "");
        FlxG.camera.zoom = scenario[2];
        FlxG.camera.scroll.set(scenario[0] - 640, scenario[1] - 360);
        var layers:Array<{sprite:FlxSprite, order:Int, index:Int}> = [];
        var sky = new FlxTiledSprite(BitmapData.fromFile(root + "/effects/" + (erect ? "phillySkybox" : "sky") + "/phillySkybox.png"), 2922, 718, true, false);
        sky.setPosition(-650, -375);
        sky.scrollFactor.set(0.1, 0.1);
        sky.scale.set(0.65, 0.65);
        sky.scrollX = -22 * time;
        layers.push({sprite:sky, order:10, index:0});
        if (!skyOnly)
        {
            var data:Dynamic = haxe.Json.parse(File.getContent(root + "/stages/" + stage + "/stage.json"));
            var props:Array<Dynamic> = data.props;
            for (prop in props)
            {
                var name:String = prop.name;
                if (StringTools.startsWith(name, "phillyCars") || name == "paper") continue;
                var sprite = new FlxSprite();
                var path:String = prop.assetPath;
                if (StringTools.startsWith(path, "#"))
                    sprite.makeGraphic(Std.int(prop.scale[0]), Std.int(prop.scale[1]), Std.parseInt("0xFF" + path.substr(1)));
                else
                {
                    var folder = root + "/stages/" + stage + "/" + name;
                    var file = path.split("/").pop();
                    var png = BitmapData.fromFile(folder + "/" + file + ".png");
                    if (sys.FileSystem.exists(folder + "/" + file + ".xml"))
                    {
                        sprite.frames = FlxAtlasFrames.fromSparrow(png, File.getContent(folder + "/" + file + ".xml"));
                        var animations:Array<Dynamic> = prop.animations;
                        if (animations.length > 0)
                        {
                            sprite.animation.addByPrefix("idle", animations[0].prefix, 0);
                            sprite.animation.play("idle");
                        }
                    }
                    else sprite.loadGraphic(png);
                    sprite.scale.set(prop.scale[0], prop.scale[1]);
                }
                sprite.updateHitbox();
                sprite.setPosition(prop.position[0], prop.position[1]);
                sprite.scrollFactor.set(prop.scroll[0], prop.scroll[1]);
                if (prop.alpha != null) sprite.alpha = prop.alpha;
                if (prop.angle != null) sprite.angle = prop.angle;
                if (prop.flipX != null) sprite.flipX = prop.flipX;
                if (StringTools.endsWith(name, "_lightmap")) { sprite.blend = BlendMode.ADD; sprite.alpha = 0.6; }
                if (name == "grey1") sprite.blend = BlendMode.ADD;
                if (name == "grey2") sprite.blend = BlendMode.MULTIPLY;
                layers.push({sprite:sprite, order:prop.zIndex, index:layers.length});
            }
            if (erect)
            {
                var ys = [660, 500, 540, 230, 170, -80];
                var frequencies = [0.35, 0.3, 0.4, 0.3, 0.35, 0.08];
                var amplitudes = [70, 80, 60, 70, 50, 100];
                var speeds = [172, 150, -80, -50, 40, 20];
                var scales = [1.0, 1.0, 1.0, 0.8, 0.7, 1.1];
                var scrolls = [1.2, 1.1, 1.2, 0.95, 0.8, 0.5];
                var alphas = [0.6, 0.6, 0.8, 0.5, 1.0, 1.0];
                var orders = [1000, 1000, 1001, 99, 88, 39];
                for (i in 0...6)
                {
                    var name = i == 2 || i == 4 ? "mistBack" : "mistMid";
                    var mist = new FlxBackdrop(BitmapData.fromFile(root + "/effects/" + name + "/" + name + ".png"), 0x01);
                    mist.setPosition(-650 + time * speeds[i], ys[i] + Math.sin(time * frequencies[i]) * amplitudes[i]);
                    mist.scrollFactor.set(scrolls[i], scrolls[i]);
                    mist.scale.set(scales[i], scales[i]);
                    mist.alpha = alphas[i];
                    mist.color = 0xFF5C5C5C;
                    mist.blend = BlendMode.ADD;
                    layers.push({sprite:mist, order:orders[i], index:layers.length});
                }
            }
        }
        layers.sort((a, b) -> a.order == b.order ? a.index - b.index : a.order - b.order);
        for (layer in layers) { layer.sprite.active = false; add(layer.sprite); }
    }

    override public function update(elapsed:Float):Void
    {
        super.update(elapsed);
        age += elapsed;
        if (age < 0.3) return;
        File.saveBytes(output + "/" + prefix + ".png", lime.app.Application.current.window.readPixels().encode(PNG));
        capture++;
        if (capture == 6) Sys.exit(0);
        FlxG.switchState(() -> new ReferenceState());
    }
}
