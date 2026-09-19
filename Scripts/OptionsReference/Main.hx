import flixel.FlxG;
import flixel.FlxGame;
import flixel.FlxSprite;
import flixel.FlxState;
import flixel.text.FlxText;
import funkin.ui.AtlasText;
import funkin.ui.options.items.CheckboxPreferenceItem;
import openfl.display.Sprite;
import sys.io.File;

class Main extends Sprite
{
    public function new()
    {
        super();
        addChild(new FlxGame(1280,720,ReferenceState,60,60,true));
    }
}

class ReferenceState extends FlxState
{
    var metrics:Array<Dynamic> = [];

    override public function create():Void
    {
        super.create();
        FlxG.mouse.visible = false;
        FlxSprite.defaultAntialiasing = true;
        var bg = new FlxSprite().loadGraphic("assets/menuBG.png");
        bg.shader = new OptionsShader();
        bg.setGraphicSize(Std.int(1280*1.1));
        bg.updateHitbox();
        bg.screenCenter();
        add(bg);
        var page=Sys.getEnv("UNITY_PARTY_OPTIONS_REFERENCE_PAGE");
        if(page=="preferences") preferences();
        else if(page=="controls") controls();
        else root();
        var ticks=0;
        FlxG.signals.postDraw.add(function()
        {
            ticks++;
            if(ticks!=20)return;
            var output=Sys.getEnv("UNITY_PARTY_OPTIONS_REFERENCE_PATH");
            File.saveBytes(output+"/"+page+"-reference.png",lime.app.Application.current.window.readPixels().encode(PNG));
            File.saveContent(output+"/"+page+"-metrics.json",haxe.Json.stringify(metrics,null,"  "));
            Sys.exit(0);
        });
    }

    function root():Void
    {
        var labels=["PREFERENCES","CONTROLS","LAG ADJUSTMENT","CLEAR SAVE DATA","EXIT"];
        for(i in 0...labels.length)
        {
            var text = new AtlasText(0,100+i*100,labels[i],AtlasFont.BOLD);
            text.screenCenter(X);
            text.alpha = i==0?1:0.6;
            for(letter in text) { letter.animation.paused=true; letter.animation.curAnim.curFrame=0; }
            metrics.push({label:labels[i],x:text.x,y:text.y,width:text.width,height:text.height});
            add(text);
        }
    }

    function text(x:Float,y:Float,label:String,bold=true,alpha:Float=0.6):AtlasText
    {
        var item=new AtlasText(x,y,label,bold?AtlasFont.BOLD:AtlasFont.DEFAULT);
        item.alpha=alpha;
        for(letter in item) { letter.animation.paused=true; letter.animation.curAnim.curFrame=0; }
        add(item);
        return item;
    }

    function preferences():Void
    {
        var labels=["Naughtyness","Downscroll","Strumline Background","Flashing Lights","Camera Zooms","Subtitles"];
        for(i in 0...labels.length)
        {
            var x:Float=i==0?150:120;
            if(i==2) { var value=text(15,270,"0%",false,1); x+=value.getWidth()-75; }
            var label=text(x,i*120+30,labels[i],true,i==0?1:0.6);
            metrics.push({label:labels[i],x:label.x,y:label.y,width:label.width});
        }
        for(i in [0,1,3,4,5])
        {
            var checkbox=new CheckboxPreferenceItem(0,i*120,i!=1);
            checkbox.animation.finish();
            add(checkbox);
            metrics.push({checkbox:i,x:checkbox.x,y:checkbox.y,width:checkbox.width,height:checkbox.height,originX:checkbox.origin.x,originY:checkbox.origin.y});
        }
        var description=new FlxText(0,0,1180,"When enabled, raunchy content (such as swearing, etc.) is displayed.",32);
        description.setFormat("assets/vcr.ttf",32,0xFFFFFFFF,CENTER,OUTLINE,0xFF000000);
        description.borderSize=3;
        description.screenCenter();
        description.y+=270;
        var box=new FlxSprite(description.x-10,description.y-10).makeGraphic(Std.int(description.width+20),Std.int(description.height+25),0xFF000000);
        box.alpha=0.6;
        add(box);
        add(description);
        metrics.push({descriptionY:description.y,descriptionHeight:description.height});
    }

    function controls():Void
    {
        var labels=["LEFT","DOWN","UP","RIGHT","LEFT","DOWN","UP","RIGHT"];
        var primary=["A","S","W","D","A","S","W","D"];
        var secondary=["Left","Down","Up","Right","Left","Down","Up","Right"];
        var y=30;
        for(i in 0...labels.length)
        {
            if(i==0 || i==4) { var header=text(0,y,i==0?"NOTES":"UI",true,1); header.screenCenter(X); y+=70; }
            text(50,y,labels[i]);
            text(750,y,primary[i],false,i==0?1:0.6);
            text(1050,y,secondary[i],false);
            y+=70;
        }
    }
}

class OptionsShader extends flixel.system.FlxAssets.FlxShader
{
    @:glFragmentSource('
        #pragma header
        vec3 rgb2hsv(vec3 c) {
            vec4 K=vec4(0.0,-1.0/3.0,2.0/3.0,-1.0);
            vec4 p=mix(vec4(c.bg,K.wz),vec4(c.gb,K.xy),step(c.b,c.g));
            vec4 q=mix(vec4(p.xyw,c.r),vec4(c.r,p.yzx),step(p.x,c.r));
            float d=q.x-min(q.w,q.y);
            return vec3(abs(q.z+(q.w-q.y)/(6.0*d+1.0e-10)),d/(q.x+1.0e-10),q.x);
        }
        vec3 hsv2rgb(vec3 c) {
            vec4 K=vec4(1.0,2.0/3.0,1.0/3.0,3.0);
            vec3 p=abs(fract(c.xxx+K.xyz)*6.0-K.www);
            return c.z*mix(K.xxx,clamp(p-K.xxx,0.0,1.0),c.y);
        }
        void main() {
            vec4 color=flixel_texture2D(bitmap,openfl_TextureCoordv);
            vec3 hsv=rgb2hsv(color.rgb)*vec3(-0.6,0.9,0.72);
            gl_FragColor=vec4(hsv2rgb(hsv),color.a);
        }
    ')
    public function new() { super(); }
}
