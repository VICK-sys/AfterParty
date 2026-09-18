import flixel.graphics.frames.FlxAtlasFrames;

class Paths
{
    public static function getSparrowAtlas(path:String):FlxAtlasFrames
    {
        if (path == "fonts/default") path = "fonts/bold";
        return FlxAtlasFrames.fromSparrow("assets/" + path + ".png", "assets/" + path + ".xml");
    }
}
