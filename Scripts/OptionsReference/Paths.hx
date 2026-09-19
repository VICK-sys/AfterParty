import flixel.graphics.frames.FlxAtlasFrames;

class Paths
{
    public static function getSparrowAtlas(path:String):FlxAtlasFrames
    {
        path = StringTools.replace(path, "fonts/", "");
        return FlxAtlasFrames.fromSparrow("assets/" + path + ".png", "assets/" + path + ".xml");
    }
}
