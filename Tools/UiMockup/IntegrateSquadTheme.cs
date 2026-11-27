using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

// Run through Unity-MCP in Edit Mode. Art and serialized references only; no runtime skin override.
public static class IntegrateSquadTheme
{
    const string Root="Assets/SqadSort/Content/UI/SquadTheme";
    const string Report="docs/art/ui-squad-theme-20261126/integration";
    static readonly Dictionary<string,Texture2D> Seeds=new Dictionary<string,Texture2D>();
    static readonly Dictionary<string,string> Generated=new Dictionary<string,string>();
    static readonly Dictionary<string,Vector4> Borders=new Dictionary<string,Vector4>();
    static readonly Dictionary<string,Sprite> Cache=new Dictionary<string,Sprite>();
    static readonly Dictionary<string,string> OldRoles=new Dictionary<string,string>();
    static readonly Dictionary<string,string> RefPaths=new Dictionary<string,string>();
    static readonly List<object> Changes=new List<object>();
    static readonly string[] Colors={"green","blue","yellow","red","header","pill","cream","mint","pink"};
    static readonly string[] Icons={"undo","shuffle","remove","home-icon","shop-icon","lock","buddies","hidden","house"};
    static readonly Dictionary<string,float[]> Rects=new Dictionary<string,float[]>();
    static readonly Dictionary<string,string> PreviousNames=new Dictionary<string,string>();
    static List<string> Prefabs;

    public static string Main()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Requires idle Edit Mode");
        Directory.CreateDirectory(Root+"/Generated");Directory.CreateDirectory(Report);
        var baseline=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("docs/art/ui-chip-away-20261125/before-layout.json"));
        if(File.Exists(Report+"/changes.json"))foreach(var c in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Report+"/changes.json")))
            PreviousNames[(string)c["prefab"]+"|"+(string)c["node"]]=(string)c["old"];
        Prefabs=new List<string>();
        foreach(var p in baseline["pages"])
        {
            Prefabs.Add((string)p["prefab"]);
            foreach(var n in p["nodes"])
            {
                string node=(string)n["path"];int slash=node.IndexOf('/');node=slash<0?"":node.Substring(slash+1);
                Rects[(string)p["prefab"]+"|"+node]=n["native"].ToObject<float[]>();
                if(n["sprite"]!=null&&n["sprite"].Type!=Newtonsoft.Json.Linq.JTokenType.Null)
                {
                    OldRoles[(string)n["sprite"]]=(string)n["role"];
                    PreviousNames[(string)p["prefab"]+"|"+node]=(string)n["sprite"];
                }
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        LoadAtlas("controls",Colors);LoadAtlas("icons",Icons);
        var illustrations=Read(Root+"/Source/illustrations.png");
        string[] extra={"revive-art","extra-tray","gift-art","perfect"};
        for(int i=0;i<4;i++)Seeds[extra[i]]=Trim(illustrations,(i%2)*illustrations.width/2,(1-i/2)*illustrations.height/2,illustrations.width/2,illustrations.height/2);
        UnityEngine.Object.DestroyImmediate(illustrations);
        Seeds["gear"]=Read("Assets/Vendor/MoneySort/Source/Game/Sprite/FlicksortSprite/icn_setting 1.png");
        var replay=Read(Root+"/Source/replay.png");Seeds["restart-control"]=Trim(replay,0,0,replay.width,replay.height);UnityEngine.Object.DestroyImmediate(replay);
        foreach(string name in new[]{"home","loading","logo"})Seeds[name]=Read(Root+"/Source/"+name+".png");
        Bake("hidden",112,120);Bake("house",112,120);
        // Prepare every replacement before saving any prefab.
        foreach(string path in Prefabs.Distinct())
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach(var image in root.GetComponentsInChildren<Image>(true))
                {
                    if(image.sprite==null)continue;
                    string role=Role(OriginalName(path,image,root),image.name);
                    if(role==null)continue;
                    var size=NativeSize(path,image,root);
                    string asset=Bake(role,size[0],size[1]);
                    string old=AssetDatabase.GetAssetPath(image.sprite);
                    if(!RefPaths.ContainsKey(old))RefPaths[old]=asset;
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        // Stable, single-sprite assets at each target size. Border protects rims if a host expands.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var pair in Generated)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(pair.Value);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=1;importer.spriteBorder=Borders[pair.Value];
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        foreach(string path in Prefabs.Distinct())Apply(path);
        // Runtime-configured icons and button state sprites must use the same skin too.
        int configCount=0;
        foreach(string guid in AssetDatabase.FindAssets("t:BoosterConfigSO"))
        {
            var asset=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var so=new SerializedObject(asset);
            bool changed=false;var it=so.GetIterator();
            while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue is Sprite sp)
            {
                string role=Role(sp.name,it.name);if(role==null)continue;
                string replacement=Bake(role,112,120);
                if(AssetDatabase.LoadAssetAtPath<Sprite>(replacement)==null)throw new InvalidOperationException("Unprepared icon "+replacement);
                it.objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(replacement);changed=true;
            }
            if(changed){so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);configCount++;}
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report+"/changes.json",Newtonsoft.Json.JsonConvert.SerializeObject(Changes,Newtonsoft.Json.Formatting.Indented));
        File.WriteAllText(Report+"/sprite-reference-map.json",Newtonsoft.Json.JsonConvert.SerializeObject(RefPaths,Newtonsoft.Json.Formatting.Indented));
        foreach(var texture in Seeds.Values.Distinct())if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
        Seeds.Clear();
        return "Applied SquadTheme to "+Prefabs.Distinct().Count()+" prefabs; "+Changes.Count+" images; "+Generated.Count+" native textures; "+configCount+" icon configs; all RectTransforms checked equal.";
    }

    static void Apply(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Canvas.ForceUpdateCanvases();
            var before=root.GetComponentsInChildren<RectTransform>(true).Select(t=>new {t,rect=Geometry(t)}).ToArray();
            foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                if(image.sprite==null)continue;
                string old=OriginalName(path,image,root);string role=Role(old,image.name);if(role==null)continue;
                var size=NativeSize(path,image,root);
                string asset=Bake(role,size[0],size[1]);
                image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(asset);
                if(image.sprite==null)throw new InvalidOperationException("Missing "+asset);
                if(image.overrideSprite!=null&&image.overrideSprite.name==old)image.overrideSprite=null;
                image.color=Color.white;
                image.type=Image.Type.Simple;
                image.pixelsPerUnitMultiplier=1;
                image.preserveAspect=!IsFrame(role)&&role!="home"&&role!="loading";
                Changes.Add(new{prefab=path,node=AnimationUtility.CalculateTransformPath(image.transform,root.transform),old,role,asset,width=image.rectTransform.rect.width,height=image.rectTransform.rect.height});
            }
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                if(component==null||component is Image||component is Transform)continue;
                var so=new SerializedObject(component);var it=so.GetIterator();bool dirty=false;
                while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue is Sprite sprite)
                {
                    if(it.propertyPath.StartsWith("_featureIcons.Array.data["))
                    {
                        string icon=it.propertyPath.Contains("[0]")?"hidden":"house";
                        it.objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(Bake(icon,112,120));dirty=true;continue;
                    }
                    string old=AssetDatabase.GetAssetPath(sprite);
                    if(RefPaths.TryGetValue(old,out string target))
                    {it.objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(target);dirty=true;}
                }
                if(dirty)so.ApplyModifiedPropertiesWithoutUndo();
            }
            if(File.Exists(Report+"/static-copy.json"))
            {
                var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Report+"/static-copy.json"));
                foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                    if(copy.TryGetValue(label.text,out string replacement))label.text=replacement;
            }
            foreach(var b in before)if(!b.rect.SequenceEqual(Geometry(b.t)))throw new InvalidOperationException("Layout drift "+path+"/"+b.t.name);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static float[] Geometry(RectTransform t)=>new[]{t.anchorMin.x,t.anchorMin.y,t.anchorMax.x,t.anchorMax.y,t.pivot.x,t.pivot.y,t.sizeDelta.x,t.sizeDelta.y,t.anchoredPosition.x,t.anchoredPosition.y,t.localScale.x,t.localScale.y,t.localScale.z,t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w};
    static string OriginalName(string path,Image image,GameObject root)
    {
        string key=path+"|"+AnimationUtility.CalculateTransformPath(image.transform,root.transform);
        return PreviousNames.TryGetValue(key,out string name)?name:image.sprite.name;
    }
    static float[] NativeSize(string path,Image image,GameObject root)
    {
        string key=path+"|"+AnimationUtility.CalculateTransformPath(image.transform,root.transform);
        return Rects.TryGetValue(key,out float[] size)?size:new[]{image.rectTransform.rect.width,image.rectTransform.rect.height};
    }

    static string Role(string name,string node)
    {
        string n=name.ToLowerInvariant();
        if(n=="bg_home 2")return "home";
        if(n=="background 1")return "loading";
        if(n=="title 2")return "logo";
        if(n=="hand 1")return "undo";
        if(n=="shuffle 1")return "shuffle";
        if(n=="magnet 1")return "remove";
        if(n=="boosters buttons"||n=="panel_3 1")return "mint";
        if(n=="settings")return "settings-control";
        if(n=="replay")return "restart-control";
        if(n=="image 143")return "green";
        if(n=="txt_excellent 4")return "perfect";
        if(n=="bg 3")return "victory-ground";
        if(n=="mockup1 3")return "buddies";
        if(n=="f57105c2-c4d6-48cb-b1d9-8bd84d4df900 1")return "revive-art";
        if(n=="rent_panel 1")return "extra-tray";
        if(n=="41d244d6ddb8afe0fa02cc469d49cbeffde607a2"||n=="image 72"||n=="7d7ba46021612e81ba838adfa048cd4b92044eb9")return "header";
        if(n=="background1")return "green";
        if(n=="mockup2 1"||n=="c5ad8c32-eaf5-4659-bfba-43998b7304fc 1")return "hidden";
        if(n=="mockup3 1"||n=="mockup4 1"||n=="mockup5 1"||n=="mockup6 1")return "house";
        if(n=="group 47"||n=="group 223"||n=="d2e7fdf51afe98ba9e69cadc9087ecbd00632924"||n=="6912889d689a77c979223e33ff11f1dfb2a07bb3"||n=="type_three_one 1")return "popup";
        if(n=="red_tittle 1"||n=="background1"||n=="image 143"||n=="rectangle 38")return "header";
        if(n=="yellow_button_2 1"||n=="yellow_button 3"||n=="background")return "yellow";
        if(n.StartsWith("c5a6fba0"))return "home-icon";
        if(n.StartsWith("dfb3a98d"))return "shop-icon";
        if(n.StartsWith("chip_away_lock"))return "lock";
        if(n=="rectangle 30"||n=="rectangle 39"||n=="rectangle 29")return "mint";
        if(n=="group 267")return "mint";
        if(n=="progress_bar 3")return "pill";
        if(n=="red_tittle 1")return "header";
        if(!OldRoles.TryGetValue(name,out string role))return null;
        switch(role)
        {
            case "panel":
                if(n=="booster panel"||n=="rectangle 32"||n.StartsWith("win_feature"))return "mint";
                if(n.StartsWith("panel1")||n.StartsWith("panel2"))return "offer";
                if(n=="shop_small_panel 2 1")return "cream";
                if(n=="6912889d689a77c979223e33ff11f1dfb2a07bb3")return null;
                return "popup";
            case "header":return "header";
            case "lime":return "green";
            case "purple":return n.StartsWith("yellow")?"yellow":n.StartsWith("button_red")?"red":n=="bluebutton"?"blue":"pill";
            case "pill":return "pill";
            case "footer":return n=="shop_ui 2"?"awning":"pink";
            case "selected":return "mint";
            default:return null;
        }
    }
    static bool IsFrame(string r)=>Colors.Contains(r)||r=="popup"||r=="awning"||r=="offer";

    static Texture2D Read(string path)
    {var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
    static void LoadAtlas(string file,string[] names)
    {
        var t=Read(Root+"/Source/"+file+".png");
        for(int i=0;i<9;i++)
        {
            int col=i%3,row=i/3;
            int x0=col*t.width/3,x1=(col+1)*t.width/3;
            int y0=(2-row)*t.height/3,y1=(3-row)*t.height/3;
            if(file=="controls")
            {
                float[] top={.09f,.395f,.62f},bottom={.275f,.575f,.985f};
                y0=Mathf.RoundToInt((1-bottom[row])*t.height);y1=Mathf.RoundToInt((1-top[row])*t.height);
            }
            Seeds[names[i]]=Trim(t,x0,y0,x1-x0,y1-y0);
        }
        UnityEngine.Object.DestroyImmediate(t);
    }
    // Extract the largest connected opaque shape, discarding generation dust in the gutters.
    static Texture2D Trim(Texture2D t,int x0,int y0,int w,int h)
    {
        var all=t.GetPixels32();var seen=new bool[w*h];var best=new List<int>();
        for(int start=0;start<seen.Length;start++)
        {
            if(seen[start]||all[(y0+start/w)*t.width+x0+start%w].a<32)continue;
            var q=new List<int>{start};seen[start]=true;
            for(int k=0;k<q.Count;k++)
            {
                int at=q[k],x=at%w,y=at/w;
                foreach(int d in new[]{-1,1,-w,w})
                {
                    int next=at+d;
                    if(next<0||next>=w*h||(d==-1&&x==0)||(d==1&&x==w-1)||seen[next])continue;
                    if(all[(y0+next/w)*t.width+x0+next%w].a<32)continue;
                    seen[next]=true;q.Add(next);
                }
            }
            if(q.Count>best.Count)best=q;
        }
        int minX=best.Min(v=>v%w),maxX=best.Max(v=>v%w),minY=best.Min(v=>v/w),maxY=best.Max(v=>v/w);
        var result=new Texture2D(maxX-minX+3,maxY-minY+3,TextureFormat.RGBA32,false);
        var pixels=new Color32[result.width*result.height];
        foreach(int v in best)pixels[(v/w-minY+1)*result.width+v%w-minX+1]=all[(y0+v/w)*t.width+x0+v%w];
        result.SetPixels32(pixels);result.Apply();return result;
    }
    static string Bake(string role,float width,float height)
    {
        int w=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(width))),h=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(height)));
        string key=role+"-"+w+"x"+h;
        if(Generated.TryGetValue(key,out string existing))return existing;
        string path=Root+"/Generated/"+key+".png";
        var result=new Texture2D(w,h,TextureFormat.RGBA32,false);
        var px=new Color[w*h];
        if(role=="popup")
        {
            int top=Mathf.Min(100,Mathf.RoundToInt(w*.16f));
            Blit(px,w,h,Seeds["cream"],0,0,w,h-top/2,true);
            var trio=Seeds["buddies"];float factor=Mathf.Min(w*.64f/trio.width,(float)(top+45)/trio.height);
            int tw=Mathf.RoundToInt(trio.width*factor),th=Mathf.RoundToInt(trio.height*factor);
            Blit(px,w,h,trio,(w-tw)/2,h-th,tw,th,false);
        }
        else if(role=="offer")
        {
            Blit(px,w,h,Seeds["cream"],0,0,w,h,true);
            Blit(px,w,h,Seeds["header"],12,12,w-24,Mathf.RoundToInt(h*.25f),true);
        }
        else if(role=="victory-ground")
        {
            Color top=new Color(.26f,.70f,.55f),bottom=new Color(.06f,.37f,.29f);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=Color.Lerp(bottom,top,(float)y/h);
        }
        else if(role=="settings-control")
        {
            Blit(px,w,h,Seeds["pink"],0,0,w,h,true);
            Blit(px,w,h,Seeds["gear"],w/5,h/5,w*3/5,h*3/5,false);
        }
        else if(role=="awning")
        {
            // The source awning remains a single sprite; author each scallop without moving its Image.
            int count=8;
            for(int i=0;i<count;i++)Blit(px,w,h,Seeds[i%2==0?"pink":"cream"],i*w/count,0,(i+1)*w/count-i*w/count,h,true);
        }
        else
        {
            var seed=Seeds[role];bool frame=IsFrame(role);
            if(frame||role=="home"||role=="loading")Blit(px,w,h,seed,0,0,w,h,frame);
            else
            {
                float scale=Mathf.Min((float)w/seed.width,(float)h/seed.height);
                int sw=Mathf.Max(1,Mathf.RoundToInt(seed.width*scale)),sh=Mathf.Max(1,Mathf.RoundToInt(seed.height*scale));
                Blit(px,w,h,seed,(w-sw)/2,(h-sh)/2,sw,sh,false);
            }
        }
        result.SetPixels(px);result.Apply();File.WriteAllBytes(path,result.EncodeToPNG());UnityEngine.Object.DestroyImmediate(result);
        Generated[key]=path;float border=IsFrame(role)?Mathf.Min(24,Mathf.Min(w,h)*.2f):0;
        Borders[path]=new Vector4(border,border,border,border);
        return path;
    }
    static float Map(float p,int dest,int source,float sb,float db)
    {
        if(p<db)return p/db*sb;
        if(p>dest-db)return source-(dest-p)/db*sb;
        return sb+(p-db)/(dest-db*2)*(source-sb*2);
    }
    static void Blit(Color[] dst,int dw,int dh,Texture2D src,int x0,int y0,int w,int h,bool sliced)
    {
        float sb=src.width>src.height*1.6f?src.height*.49f:Mathf.Min(src.width,src.height)*.19f;
        float db=src.width>src.height*1.6f?Mathf.Min(sb,Mathf.Min(w,h)*.48f):Mathf.Min(28,Mathf.Min(w,h)*.23f);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int dx=x+x0,dy=y+y0;if(dx<0||dy<0||dx>=dw||dy>=dh)continue;
            float sx=sliced?Map(x+.5f,w,src.width,sb,db):(x+.5f)*src.width/w;
            float sy=sliced?Map(y+.5f,h,src.height,sb,db):(y+.5f)*src.height/h;
            Color c=src.GetPixelBilinear(sx/src.width,sy/src.height);int at=dy*dw+dx;Color b=dst[at];
            float a=c.a+b.a*(1-c.a);
            dst[at]=a<=0?Color.clear:new Color((c.r*c.a+b.r*b.a*(1-c.a))/a,(c.g*c.a+b.g*b.a*(1-c.a))/a,(c.b*c.a+b.b*b.a*(1-c.a))/a,a);
        }
    }
}
