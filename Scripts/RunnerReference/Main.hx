import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import flixel.graphics.frames.FlxAtlasFrames;
import flixel.group.FlxGroup.FlxTypedGroup;
import flixel.math.FlxMatrix;
import openfl.display.BitmapData;
import openfl.display.Sprite;
import openfl.geom.Point;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(1280, 720, RunnerState, 60, 60, true));
    }
}

class RunnerState extends FlxState
{
    override public function create():Void
    {
        super.create();
        var root = Sys.getEnv("RUNNER_REFERENCE_ASSETS");
        var atlas = FlxAtlasFrames.fromSparrow(BitmapData.fromFile(root + "/week7/images/tankmanKilled1.png"), File.getContent(root + "/week7/images/tankmanKilled1.xml"));
        var records:Array<Dynamic> = [];
        for (scale in [1.0, 1.1]) for (fresh in [true, false]) for (flip in [false, true]) for (name in ["run", "shot1", "shot2"])
        {
            var sprite = new ReferenceRunner();
            sprite.frames = atlas;
            sprite.animation.addByPrefix("run", "tankman running", 24, true);
            sprite.animation.addByPrefix("shot1", "John Shot 1", 24, false);
            sprite.animation.addByPrefix("shot2", "John Shot 2", 24, false);
            sprite.animation.play("run");
            sprite.offset.set(0, 0);
            sprite.setGraphicSize(Std.int(sprite.width * 0.4));
            sprite.updateHitbox();
            if (!fresh) sprite.offset.set(0, 0);
            sprite.setPosition(1000, 350);
            sprite.scale.set(scale, scale);
            sprite.flipX = flip;
            sprite.animation.play(name);
            sprite.animation.curAnim.curFrame = 0;
            if (name != "run") sprite.offset.set(300, 200);
            for (frameIndex in 0...sprite.animation.curAnim.numFrames)
            {
                sprite.animation.curAnim.curFrame = frameIndex;
                records.push({scale:scale, fresh:fresh, flip:flip, animation:name, frame:frameIndex, origin:[sprite.origin.x, sprite.origin.y], offset:[sprite.offset.x, sprite.offset.y], bounds:sprite.measure()});
            }
        }
        File.saveContent(Sys.getEnv("RUNNER_REFERENCE_OUTPUT") + "/reference.json", haxe.Json.stringify(records, null, "  "));
        var pool = new FlxTypedGroup<FlxSprite>(4);
        var cycles:Array<Dynamic> = [];
        for (count in [9, 6, 5])
        {
            pool.clear();
            var slots:Array<Int> = [];
            for (spawn in 0...count)
            {
                var sprite = pool.recycle(FlxSprite, function() return new FlxSprite(), false, true);
                slots.push(pool.members.indexOf(sprite));
                sprite.kill();
            }
            cycles.push({spawns:count, slots:slots});
        }
        File.saveContent(Sys.getEnv("RUNNER_REFERENCE_OUTPUT") + "/pool-reference.json", haxe.Json.stringify(cycles, null, "  "));
        Sys.exit(0);
    }
}

class ReferenceRunner extends FlxSprite
{
    public function measure():Array<Float>
    {
        pixelPerfectRender = false;
        var matrix = new FlxMatrix();
        prepareComplexMatrix(matrix, frame, FlxG.camera);
        var corners = [new Point(0, 0), new Point(frame.frame.width, 0), new Point(frame.frame.width, frame.frame.height), new Point(0, frame.frame.height)].map(matrix.transformPoint);
        var xs = corners.map(function(p) return p.x);
        var ys = corners.map(function(p) return p.y);
        xs.sort(function(a, b) return a < b ? -1 : a > b ? 1 : 0);
        ys.sort(function(a, b) return a < b ? -1 : a > b ? 1 : 0);
        return [xs[0], ys[0], xs[3], ys[3]];
    }
}

