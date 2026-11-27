using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Editor-side fitting of existing UI, using measured mockup coordinates, never a runtime layout override.
public static class AlignUiToMockup
{
    const string Art="Assets/SqadSort/Content/UI/SquadTheme/Aligned";
    const string Out="docs/art/ui-squad-theme-20261126/alignment";
    const string Vendor="Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/";
    static readonly Dictionary<string,Texture2D> Raw=new Dictionary<string,Texture2D>();
    static readonly Dictionary<string,string> Files=new Dictionary<string,string>();
    static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
    static readonly Dictionary<string,Vector4> BorderSettings=new Dictionary<string,Vector4>();
    static readonly List<object> Fits=new List<object>();
    static RectTransform CanvasRect;
    static GameObject ActiveRoot;
    static string Page;
    static Material White,Body,Green,Blue;
    static TMP_FontAsset RoundedFont;
    static readonly Dictionary<string,Material> FontMaterials=new Dictionary<string,Material>();

    public static string Main()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Idle Edit Mode required");
        Directory.CreateDirectory(Art+"/Native");Directory.CreateDirectory(Out+"/fitted");
        Raw["shop"]=Read(Art+"/Source/shop-plate.png");Raw["settings"]=Read(Art+"/Source/settings-plate.png");
        Raw["empty"]=Read(Art+"/Source/empty-popup.png");Raw["brand"]=Read(Art+"/Source/brand-nav.png");
        Raw["home"]=Read(Art+"/Source/home-ground.png");Raw["play"]=Read(Art+"/Source/play-button.png");
        Raw["footer-home"]=Read(Art+"/Source/footer-home.png");Raw["ref-settings"]=Read(Out+"/reference-Settings.png");
        Raw["ref-shop"]=Read(Out+"/reference-Shop.png");
        Raw["garden"]=Read("Assets/SqadSort/Content/Textures/courtyard-background.png");
        // Masters are extracted, not re-invented. Named coordinates are measured in the 720×1280 review reference.
        Save("home-ground",Scale(Raw["home"],720,1280),Vector4.zero);
        Save("footer-home",Scale(Trim(Raw["footer-home"],0,0,Raw["footer-home"].width,Raw["footer-home"].height,90),720,145),new Vector4(40,28,40,28));
        var clear=new Texture2D(1,1,TextureFormat.RGBA32,false);clear.SetPixel(0,0,Color.clear);clear.Apply();Save("transparent",clear,Vector4.zero);
        var cross=Crop(Raw["ref-settings"],new Rect(572,319,44,43),44,43,720,1280);var crossPixels=cross.GetPixels();
        for(int i=0;i<crossPixels.Length;i++){float brightness=Mathf.Min(crossPixels[i].r,Mathf.Min(crossPixels[i].g,crossPixels[i].b));crossPixels[i]=new Color(1,1,1,Mathf.Clamp01((brightness-.65f)/.25f));}
        cross.SetPixels(crossPixels);cross.Apply();Save("close-glyph",cross,Vector4.zero);
        Save("garden",Scale(Raw["garden"],720,1280),Vector4.zero);
        Save("play",Scale(Trim(Raw["play"],0,0,Raw["play"].width,Raw["play"].height,90),436,153),new Vector4(65,60,65,60));
        Brand("logo",new Rect(40,0,880,560),535,360);
        Brand("home-icon",new Rect(960,90,420,420),112,112);
        Brand("shop-icon",new Rect(180,555,550,455),110,100);
        Brand("lock",new Rect(950,530,450,470),95,110);
        ShopCrop("awning",new Rect(0,106,720,91),720,91);
        CardPlate("offer-remove",new Rect(14,210,690,366));
        CardPlate("offer-starter",new Rect(14,592,690,366));
        ShopCrop("buy-green",new Rect(425,474,244,70),244,70);
        ShopCrop("section",new Rect(29,974,662,84),662,84);
        ShopCrop("pill",new Rect(401,16,196,72),196,72);
        // Keep the original gear's exact silhouette and glyph; the corners are an extraction mask.
        var gear=Crop(Raw["ref-shop"],new Rect(24,12,75,75),75,75,720,1280);RoundMask(gear,20);Save("gear",gear,Vector4.zero);
        // Action controls are separate assets, not baked captions or prices.
        SettingsCrop("green",new Rect(194,836,742,210),454,128);
        SettingsCrop("blue",new Rect(194,1047,742,205),454,125);
        SettingsCrop("toggle-on",new Rect(674,431,228,112),137,67);
        SettingsCrop("toggle-off",new Rect(674,690,228,112),137,67);
        BuildPopup();BuildFooter();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var file in Files)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(file.Value);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=1;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=4096;
            importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Bilinear;
            importer.spriteBorder=BorderSettings[file.Key];
            importer.SaveAndReimport();Sprites[file.Key]=AssetDatabase.LoadAssetAtPath<Sprite>(file.Value);
        }
        MakeMaterials();
        MakeRoundedFont();
        var paths=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("docs/art/ui-chip-away-20261125/before-layout.json"))["pages"];
        foreach(var p in paths)
        {
            Page=(string)p["name"];string path=(string)p["prefab"];
            Fit(path);
        }
        File.WriteAllText(Out+"/measured-fits.json",Newtonsoft.Json.JsonConvert.SerializeObject(Fits,Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();
        foreach(var tex in Raw.Values.Distinct())if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);
        Raw.Clear();
        return "Fitted 25 existing prefabs to measured mockup landmarks; "+Fits.Count+" measured visual nodes. Native assets: "+Files.Count+". No runtime layout code added.";
    }

    static void Fit(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);ActiveRoot=root;
        var scene=root.scene;
        var canvasGo=new GameObject("AlignmentCanvas",typeof(RectTransform),typeof(Canvas));
        SceneManager.MoveGameObjectToScene(canvasGo,scene);
        CanvasRect=(RectTransform)canvasGo.transform;CanvasRect.sizeDelta=new Vector2(720,1280);
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.referencePixelsPerUnit=1;
        try
        {
            root.transform.SetParent(CanvasRect,false);
            Canvas.ForceUpdateCanvases();
            foreach(var layout in root.GetComponentsInChildren<VerticalLayoutGroup>(true))LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
            if(Page=="Home")Home();
            else if(Page=="Shop")Shop();
            else if(Page=="Settings")Settings();
            else if(Page!="GameplayHud"&&Page!="Loading"&&Page!="LevelUp"&&Page!="PurchaseReward")CommonDialog();
            if(Page=="GameplayHud")Hud();
            if(Page=="Home"||Page=="Shop")Footer();
            // Material/text roles are deliberately authored on prefabs; no View overwrites look values.
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string n=text.name.ToLowerInvariant();
                if(n.Contains("description")||n=="message"){text.fontSharedMaterial=ForFont(text,Body);text.color=Color.white;text.enableVertexGradient=false;}
            }
            root.transform.SetParent(null,false);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally
        {
            if(root!=null)root.transform.SetParent(null,false);
            UnityEngine.Object.DestroyImmediate(canvasGo);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
    static Transform Find(string path)=>ActiveRoot.transform.Find(path);
    static RectTransform Rt(string path)=>Find(path) as RectTransform;
    static Image Im(string path)=>Find(path)?.GetComponent<Image>();
    static void Image(string path,string key,Rect box,bool measure=true)
    {
        var image=Im(path);if(image==null)throw new InvalidOperationException(Page+" missing "+path);
        image.sprite=Sprites[key];image.overrideSprite=null;image.color=Color.white;image.type=UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect=false;image.enabled=true;
        Bounds((RectTransform)image.transform,box,path,measure);
    }
    static void Label(string path,Rect box,float size,Material material)
    {
        var text=Find(path)?.GetComponent<TMP_Text>();if(text==null)return;
        text.font=RoundedFont;text.fontStyle=FontStyles.Normal;text.fontWeight=FontWeight.Regular;
        text.margin=Vector4.zero;text.characterSpacing=0;text.wordSpacing=0;text.alignment=TextAlignmentOptions.Center;
        if(path.EndsWith("/Title") && Page=="Shop")text.alignment=TextAlignmentOptions.MidlineLeft;
        if(Page=="Settings" && path.EndsWith("Toggle/Label"))text.alignment=TextAlignmentOptions.MidlineLeft;
        Bounds(text.rectTransform,box,path,false);text.fontSize=size*(path.EndsWith("/Price")?1f:1.18f);text.fontSharedMaterial=ForFont(text,material);text.enableVertexGradient=false;
        text.enableAutoSizing=false;text.color=Color.white; text.textWrappingMode=TextWrappingModes.NoWrap; if(path.EndsWith("/IconLabel")) { text.textWrappingMode=TextWrappingModes.Normal;text.fontSize=size; } float preferred=text.GetPreferredValues(text.text).x; if(preferred>box.width && !text.text.Contains("\n"))text.fontSize*=box.width/preferred;
        if(text.name=="MoneyText"||text.name=="LivesText"||text.name=="LifeTimerText") { text.fontSize=36; text.fontSizeMin=36; text.fontSizeMax=36; text.enableAutoSizing=false; }
        if(text.name=="MoneyText") {
            text.alignment=TextAlignmentOptions.Midline;
            var frame=(RectTransform)text.transform.parent;var coin=frame.Find("CoinIcon") as RectTransform;
            var pos=text.rectTransform.localPosition;pos.y=frame.rect.center.y;
            if(coin!=null) { var c=new Vector3[4];coin.GetWorldCorners(c);pos.x=(c.Max(v=>frame.InverseTransformPoint(v).x)+8+frame.rect.xMax-10)*.5f; }
            text.rectTransform.localPosition=pos;
        }
        text.ForceMeshUpdate(true,true);
    }
    static void Bounds(RectTransform t,Rect box,string name,bool measure)
    {
        if(t==null)return;
        t.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,box.width);
        t.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,box.height);
        Canvas.ForceUpdateCanvases();
        var corners=new Vector3[4];t.GetWorldCorners(corners);
        Vector3 current=CanvasRect.InverseTransformPoint((corners[0]+corners[2])*.5f);
        Vector3 desired=new Vector3(box.x+box.width*.5f-360,640-box.y-box.height*.5f,current.z);
        t.position+=CanvasRect.TransformVector(desired-current);
        if(measure)
        {
            t.GetWorldCorners(corners);var lo=CanvasRect.InverseTransformPoint(corners[0]);var hi=CanvasRect.InverseTransformPoint(corners[2]);
            var observed=new[]{lo.x+360,640-hi.y,hi.x-lo.x,hi.y-lo.y};
            var target=new[]{box.x,box.y,box.width,box.height};float error=observed.Zip(target,(a,b)=>Mathf.Abs(a-b)).Max();
            if(error>.1f)throw new InvalidOperationException("Fit drift "+Page+"/"+name+" "+error);
            Fits.Add(new{page=Page,node=name,target,observed,maxError=error});
        }
    }
    static void Off(string path){var image=Im(path);if(image!=null)image.enabled=false;}
    static void Home()
    {
        const string s="MoneySort_HomeUI/";const string a=s+"SafeArea/";
        Image(s.TrimEnd('/'),"home-ground",new Rect(0,0,720,1280));
        Image(a+"ChipAwayLogo","logo",new Rect(95,116,535,360));
        Image(a+"PlayButton","play",new Rect(144,934,436,153));
        Label(a+"PlayButton/Label",new Rect(177,956,370,87),64,Green);
        Image(a+"SettingsButton","gear",new Rect(52,26,77,83));Off(a+"SettingsButton/GearIcon");
        Resources(a+"ResoureGroup/",true);
    }
    static void Resources(string p,bool home)
    {
        if(home)
        {
            Image(p+"MoneyHud","pill",new Rect(530,36,143,64));
            Bounds(Rt(p+"MoneyHud/CoinIcon"),new Rect(493,27,82,82),p+"coin",true);
            Bounds(Rt(p+"MoneyHud/CoinIcon/PlusBadge"),new Rect(539,81,27,27),p+"plus",false);
            Label(p+"MoneyHud/MoneyText",new Rect(577,43,86,45),40,White);
            Image(p+"LivesHud","pill",new Rect(309,37,149,64));
            Bounds(Rt(p+"LivesHud/HeartIcon"),new Rect(268,25,91,87),p+"heart",true);
            Label(p+"LivesHud/HeartIcon/LivesText",new Rect(285,47,49,42),38,White);
            Label(p+"LivesHud/LifeTimerText",new Rect(356,47,92,43),35,White);
        }
        else
        {
            Image(p+"MoneyHud","pill",new Rect(446,25,137,58));
            Bounds(Rt(p+"MoneyHud/CoinIcon"),new Rect(407,16,82,82),p+"coin",true);
            Bounds(Rt(p+"MoneyHud/CoinIcon/PlusBadge"),new Rect(447,56,29,29),p+"plus",false);
            Label(p+"MoneyHud/MoneyText",new Rect(491,32,80,44),42,White);
            Image(p+"LivesHud","pill",new Rect(174,28,130,54));
            Bounds(Rt(p+"LivesHud/HeartIcon"),new Rect(126,17,87,77),p+"heart",true);
            Label(p+"LivesHud/HeartIcon/LivesText",new Rect(145,32,49,42),38,White);
            Label(p+"LivesHud/LifeTimerText",new Rect(205,37,86,39),32,White);
        }
    }
    static void Shop()
    {
        Image("SafeArea/ShopHeaderBackground","garden",new Rect(0,0,720,1280));
        if(Find("ShopPinkBackground")!=null)Im("SafeArea/ShopHeaderBackground").enabled=false;
        var panel=Im("SafeArea/Panel");if(panel!=null){panel.sprite=null;panel.color=Color.clear;}
        Image("SafeArea/SettingsButton","gear",new Rect(24,12,75,75));Off("SafeArea/SettingsButton/GearIcon");
        Resources("SafeArea/ShopHeaderBackground/OverlayResources/",false);
        Image("SafeArea/Panel/Awning","awning",new Rect(0,106,720,91));
        var shopGround=Im("ShopPinkBackground");if(shopGround!=null&&shopGround.sprite!=null&&shopGround.sprite.name.Contains("pink-awning"))Off("SafeArea/Panel/Awning");
        const string c="SafeArea/Panel/Scroll/Viewport/Content";
        var content=Rt(c);var group=content.GetComponent<VerticalLayoutGroup>();group.padding.top=62;group.spacing=16;
        group.padding.left=15;group.padding.right=15;group.childAlignment=TextAnchor.UpperCenter;group.childControlWidth=true;group.childForceExpandWidth=false;
        foreach(Transform card in content)
        {
            var le=card.GetComponent<LayoutElement>();
            if(le!=null&&card.GetComponent<Game.Views.ShopOfferItemUI>()!=null){le.preferredWidth=690;le.preferredHeight=366;}
        }
        var section=Rt(c+"/SpecialPackHeader").GetComponent<LayoutElement>();section.preferredWidth=662;section.preferredHeight=84;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);Canvas.ForceUpdateCanvases();
        foreach(Transform card in content)
        {
            if(card.GetComponent<Game.Views.ShopOfferItemUI>()==null)continue;
            bool starter=card.name=="starter",remove=card.name=="remove_ads";
            Card(c+"/"+card.name,starter,remove);
        }
        var sr=ScreenRect(Rt(c+"/SpecialPackHeader"));
        Image(c+"/SpecialPackHeader/Background","section",new Rect(sr.x,sr.y,662,84));
        Label(c+"/SpecialPackHeader/Label",new Rect(sr.x+20,sr.y+8,622,63),46,White);
    }
    static Rect ScreenRect(RectTransform t)
    {var p=new Vector3[4];t.GetWorldCorners(p);var lo=CanvasRect.InverseTransformPoint(p[0]);var hi=CanvasRect.InverseTransformPoint(p[2]);return new Rect(lo.x+360,640-hi.y,hi.x-lo.x,hi.y-lo.y);}
    static void Card(string p,bool starter,bool remove)
    {
        Rect r=ScreenRect(Rt(p));float x=r.x,y=r.y;
        Rect At(float a,float b,float w,float h)=>new Rect(x+a,y+b,w,h);
        Image(p+"/Background",starter?"offer-starter":"offer-remove",At(0,0,690,366));
        // These three backplates are already in the source card. Drawing them again made double rims.
        Off(p+"/BoosterPanel");Off(p+"/LivesPanel");Off(p+"/RedBand");
        Bounds(Rt(p+"/Icon"),At(59,70,162,126),p+"/coin-art",false);
        Bounds(Rt(p+"/FreeMove"),At(262,42,90,83),p+"/undo",false);
        Bounds(Rt(p+"/Shuffle"),At(393,35,90,85),p+"/shuffle",false);
        Bounds(Rt(p+"/Hammer"),At(326,140,85,77),p+"/remove",false);
        Label(p+"/FreeMoveCount",At(267,111,65,38),32,Body);
        Label(p+"/ShuffleCount",At(401,111,65,38),32,Body);
        Label(p+"/HammerCount",At(338,210,65,38),32,Body);
        Label(p+"/Coins",At(27,184,210,50),remove?29:46,Body);
        Label(p+"/Title",At(35,266,starter?225:340,58),starter?39:42,White);
        if(remove)
        {
            Bounds(Rt(p+"/RemoveAdsIcon"),At(530,44,111,122),p+"/TV",false);
            Label(p+"/IconLabel",At(529,169,120,69),31,Body);
            Bounds(Rt(p+"/HotBadge"),At(5,-9,104,92),p+"/hot",false);
        }
        else
        {
            Bounds(Rt(p+"/Heart"),At(527,68,117,108),p+"/heart",false);
            Label(p+"/Infinity",At(542,96,85,59),48,White);
            Label(p+"/InfiniteLives",At(539,182,95,47),40,Body);
            Bounds(Rt(p+"/DiscountBadge"),At(5,-10,104,92),p+"/discount",false);
        }
        var buy=At(starter?474:411,264,starter?179:244,70);
        Image(p+"/BuyButton","buy-green",buy);
        Label(p+"/BuyButton/Price",new Rect(buy.x+8,buy.y+5,buy.width-16,58),46,Green);
        if(starter)
        {
            // Timer stays data-driven; only its visual slots are fitted.
            Off(p+"/Timer/PillInner");
            Bounds(Rt(p+"/Timer/Pill"),At(287,255,166,77),p+"/timer",false);
            Bounds(Rt(p+"/Timer/Clock"),At(273,281,42,47),p+"/clock",false);
            Label(p+"/Timer/LimitedTimeOffer",At(298,258,151,22),14,White);
            Label(p+"/Timer/Countdown",At(308,280,139,36),26,White);
        }
    }
    static void Footer()
    {
        bool home=Page=="Home";string p=home?"MoneySort_HomeUI/HomeFooter":"ShopFooter";
        Image(p,"footer-home",new Rect(0,1135,720,145));
        string main=p+"/MainTab/Content",shop=p+"/ShopTab/Content";
        if(Im(main+"/Selected/Tile")!=null)Image(main+"/Selected/Tile","selected",new Rect(244,1112,238,168));
        if(Im(shop+"/Selected/Tile")!=null)Image(shop+"/Selected/Tile","selected",new Rect(0,1128,250,152));
        if(Im(main+"/Selected/Icon")!=null)Image(main+"/Selected/Icon","home-icon",new Rect(304,1082,112,112));
        if(Im(main+"/Icon")!=null)Image(main+"/Icon","home-icon",new Rect(304,1150,112,112));
        if(Find(main+"/Unselected")!=null) { Off(main+"/Icon");Image(main+"/Unselected/Icon","home-icon",new Rect(305,1136,110,100));Im(main+"/Unselected/Icon").preserveAspect=true;Label(main+"/Unselected/Label",new Rect(291.5f,1220,137,51),34,Green); }
        if(Find(p+"/Frame")!=null)Im(p).enabled=false;
        if(Im(shop+"/Selected/Icon")!=null)Image(shop+"/Selected/Icon","shop-icon",new Rect(70,1062,110,100));
        if(Im(shop+"/Icon")!=null)Image(shop+"/Icon","shop-icon",new Rect(98,1136,110,100));
        if(Im(p+"/LockedTab/Content/Lock")!=null)Image(p+"/LockedTab/Content/Lock","lock",new Rect(home?530:530,1138,95,110));
        Label(main+"/Selected/Label",new Rect(271,1193,180,59),44,Green);
        var caption=Find(shop+"/Selected/Label")??Find(shop+"/Label");
        if(caption!=null&&caption.parent!=Find(shop+"/Selected"))caption.SetParent(Find(shop+"/Selected"),false);
        Label(shop+"/Selected/Label",new Rect(34,1173,183,51),44,Green);
        var group=Find(shop+"/Unselected");
        if(group==null)group=new GameObject("Unselected",typeof(RectTransform)).transform;
        group.SetParent(Find(shop),false);var gr=(RectTransform)group;gr.anchorMin=Vector2.zero;gr.anchorMax=Vector2.one;gr.offsetMin=Vector2.zero;gr.offsetMax=Vector2.zero;
        var icon=Find(shop+"/Icon"); if(icon!=null) { if(group.Find("Icon")==null) { var copy=UnityEngine.Object.Instantiate(icon,group,false);copy.name="Icon";copy.gameObject.SetActive(true); } icon.gameObject.SetActive(false); } if(Im(shop+"/Unselected/Icon")!=null)Image(shop+"/Unselected/Icon","shop-icon",new Rect(98,1136,110,100));
        var duplicate=group.Find("Label");
        if(duplicate==null&&caption!=null){duplicate=UnityEngine.Object.Instantiate(caption,group,false);duplicate.name="Label";}
        Label(shop+"/Unselected/Label",new Rect(86,1220,137,51),34,Green);
        var footer=Find(p).GetComponent<Game.Views.HomeFooterUI>();var so=new SerializedObject(footer);var tabs=so.FindProperty("_tabs");
        for(int i=0;i<tabs.arraySize;i++)
        {
            var tab=tabs.GetArrayElementAtIndex(i);if(tab.FindPropertyRelative("tab").enumValueIndex!=0)continue;
            tab.FindPropertyRelative("deselected").objectReferenceValue=group.gameObject;
            tab.FindPropertyRelative("unselectedLabel").objectReferenceValue=duplicate?.GetComponent<TMP_Text>();
        }
        so.ApplyModifiedPropertiesWithoutUndo();group.gameObject.SetActive(home);
    }
    static void Settings()
    {
        Image("SettingsCard","popup-body",new Rect(50,328,620,680));
        Off("SettingsCard/Inner");Off("SettingsCard/Header");
        Topper(Find("SettingsCard/Header"),new Rect(115,194,515,236));
        Label("SettingsCard/Header/Title",new Rect(169,317,410,73),52,White);
        Bounds(Rt("SettingsCard/CloseButton"),new Rect(555,301,78,79),"close",true);
        Close(Find("SettingsCard/CloseButton"),new Vector2(590,339),1);
        string p="SettingsCard/Inner/";
        string[] rows={"SFXToggle","MusicToggle","VibrationToggle"};
        for(int i=0;i<3;i++)
        {
            Label(p+rows[i]+"/Label",new Rect(155,438+i*84,255,65),44,Body);
            Image(p+rows[i]+"/Background",i==2?"toggle-off":"toggle-on",new Rect(445,445+i*83,137,67));
        }
        Image(p+"GameplayButtons/RestartButton","green",new Rect(138,704,454,128));
        Image(p+"GameplayButtons/HomeButton","blue",new Rect(138,833,454,125));
        Label(p+"GameplayButtons/RestartButton/Label",new Rect(160,721,410,77),54,Green);
        Label(p+"GameplayButtons/HomeButton/Label",new Rect(160,844,410,77),54,Blue);
        foreach(string name in new[]{"TermsOfServicesButton","PrivacyPolicyButton"})
        {
            var button=ActiveRoot.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==name);
            if(button==null)continue;
            bool first=name=="TermsOfServicesButton";var box=new Rect(138,first?704:833,454,125);
            button.image.sprite=Sprites["blue"];button.image.type=UnityEngine.UI.Image.Type.Simple;
            Bounds((RectTransform)button.transform,box,name,false);
        }
        var settings=ActiveRoot.GetComponent<Game.Views.SettingsUI>();var so=new SerializedObject(settings);
        so.FindProperty("_toggleOnSprite").objectReferenceValue=Sprites["toggle-on"];
        so.FindProperty("_toggleOffSprite").objectReferenceValue=Sprites["toggle-off"];so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void CommonDialog()
    {
        var card=ActiveRoot.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite!=null&&i.sprite.name.StartsWith("popup-"));
        if(card==null)return;
        card.sprite=Sprites["popup-body"];card.type=UnityEngine.UI.Image.Type.Sliced;card.pixelsPerUnitMultiplier=1;card.color=Color.white;
        var header=card.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="Header");
        if(header!=null)
        {
            var b=ScreenRect(header.rectTransform);header.enabled=false;
            float scale=b.width/415f;Topper(header.transform,new Rect(b.x-50*scale,b.y-121*scale,515*scale,236*scale));
            foreach(var text in header.GetComponentsInChildren<TMP_Text>(true)){text.fontSharedMaterial=ForFont(text,White);text.color=Color.white;text.enableVertexGradient=false;}
            var close=card.GetComponentsInChildren<Button>(true).FirstOrDefault(bu=>bu.name=="CloseButton");
            if(close!=null)Close(close.transform,new Vector2(b.x-50*scale+475*scale,b.y-121*scale+145*scale),scale);
        }
    }
    static void Hud()
    {
        var button=ActiveRoot.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="SettingsButton");
        if(button!=null){button.image.sprite=Sprites["gear"];button.image.preserveAspect=true;}
    }
    static void Topper(Transform parent,Rect box)
    {
        if(parent==null)return;
        var child=parent.Find("MockupTopper");
        if(child==null)child=new GameObject("MockupTopper",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).transform;
        child.SetParent(parent,false);child.SetAsFirstSibling();
        var image=child.GetComponent<Image>();image.sprite=Sprites["popup-topper"];image.raycastTarget=false;
        Bounds((RectTransform)child,box,"popup-topper",true);
    }
    static void Close(Transform button,Vector2 center,float scale)
    {
        var image=button.GetComponent<Image>();image.sprite=Sprites["transparent"];image.overrideSprite=null;image.color=Color.white;image.enabled=true;image.raycastTarget=true;
        Bounds((RectTransform)button,new Rect(center.x-39*scale,center.y-39*scale,78*scale,78*scale),"close-hit",false);
        var child=button.Find("MockupCloseGlyph");if(child==null)child=new GameObject("MockupCloseGlyph",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).transform;
        child.SetParent(button,false);var glyph=child.GetComponent<Image>();glyph.sprite=Sprites["close-glyph"];glyph.raycastTarget=false;
        Bounds((RectTransform)child,new Rect(center.x-22*scale,center.y-21.5f*scale,44*scale,43*scale),"close-glyph",false);
    }

    static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
    static void Save(string name,Texture2D t,Vector4 border)
    {
        string path=Art+"/Native/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());
        Files[name]=path;Raw["native-"+name]=t;
        BorderSettings[name]=border;
    }
    static Texture2D Scale(Texture2D t,int w,int h)=>Crop(t,new Rect(0,0,t.width,t.height),w,h,t.width,t.height);
    static Texture2D Crop(Texture2D source,Rect box,int w,int h,float referenceW,float referenceH)
    {
        var t=new Texture2D(w,h,TextureFormat.RGBA32,false);var p=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=(box.x+(x+.5f)*box.width/w)/referenceW;
            float v=1-(box.y+(h-y-.5f)*box.height/h)/referenceH;
            Color c=source.GetPixelBilinear(u,v);if(c.a<.08f)c=Color.clear;p[y*w+x]=c;
        }
        t.SetPixels(p);t.Apply();return t;
    }
    static void ShopCrop(string name,Rect box,int w,int h) { var t=Crop(Raw["shop"],box,w,h,720,1280); if(name=="buy-green")RoundMask(t,30); Save(name,t,Vector4.zero); }
    static void SettingsCrop(string name,Rect box,int w,int h) { var t=Crop(Raw["settings"],box,w,h,1122,1402); RoundMask(t,h*.48f); Save(name,t,Vector4.zero); }
    static void Brand(string name,Rect r,int w,int h)
    {
        var source=Raw["brand"];var trim=Trim(source,Mathf.RoundToInt(r.x),source.height-Mathf.RoundToInt(r.y+r.height),Mathf.RoundToInt(r.width),Mathf.RoundToInt(r.height),200);
        // Preserve character aspect inside the measured visual box, with transparent padding.
        float s=Mathf.Min((float)w/trim.width,(float)h/trim.height);int sw=Mathf.RoundToInt(trim.width*s),sh=Mathf.RoundToInt(trim.height*s);
        var t=new Texture2D(w,h,TextureFormat.RGBA32,false);t.SetPixels(new Color[w*h]);
        var scaled=Scale(trim,sw,sh);t.SetPixels((w-sw)/2,(h-sh)/2,sw,sh,scaled.GetPixels());t.Apply();Save(name,t,Vector4.zero);
        UnityEngine.Object.DestroyImmediate(trim);UnityEngine.Object.DestroyImmediate(scaled);
    }
    static Texture2D Trim(Texture2D t,int x0,int y0,int w,int h,int alpha)
    {
        var all=t.GetPixels32();int minx=w,miny=h,maxx=-1,maxy=-1;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(all[(y+y0)*t.width+x+x0].a>=alpha){minx=Mathf.Min(minx,x);maxx=Mathf.Max(maxx,x);miny=Mathf.Min(miny,y);maxy=Mathf.Max(maxy,y);}
        if(maxx<minx)throw new InvalidOperationException("Empty extracted shape");
        var result=new Texture2D(maxx-minx+3,maxy-miny+3,TextureFormat.RGBA32,false);var p=new Color32[result.width*result.height];
        for(int y=miny;y<=maxy;y++)for(int x=minx;x<=maxx;x++){var c=all[(y+y0)*t.width+x+x0];if(c.a>=alpha)p[(y-miny+1)*result.width+x-minx+1]=c;}
        result.SetPixels32(p);result.Apply();return result;
    }
    static void RoundMask(Texture2D t,float radius)
    {
        var p=t.GetPixels();for(int y=0;y<t.height;y++)for(int x=0;x<t.width;x++)
        {
            float dx=Mathf.Max(radius-x,x-(t.width-radius)),dy=Mathf.Max(radius-y,y-(t.height-radius));
            if(dx>0&&dy>0&&dx*dx+dy*dy>radius*radius)p[y*t.width+x]=Color.clear;
        }
        t.SetPixels(p);t.Apply();
    }
    static void BuildPopup()
    {
        // Blank body and intact topper are independent assets: no switches or buttons can leak into another dialog.
        var plain=Trim(Raw["empty"],0,0,Raw["empty"].width,Raw["empty"].height,110);
        Save("popup-body",Scale(plain,620,676),new Vector4(55,65,55,75));UnityEngine.Object.DestroyImmediate(plain);
        var top=Crop(Raw["settings"],new Rect(150,20,830,380),515,236,1122,1402);
        var pixels=top.GetPixels();
        for(int y=0;y<top.height;y++)for(int x=0;x<top.width;x++)
        {
            float sx=150+(x+.5f)*830/top.width,sy=20+(top.height-y-.5f)*380/top.height;
            bool close=(sx-916)*(sx-916)+(sy-261)*(sy-261)<70*70;
            bool shape=sy<228||(sx>=230&&sx<=900&&sy<=392)||close;
            if(!shape)pixels[y*top.width+x]=Color.clear;
        }
        top.SetPixels(pixels);top.Apply();Save("popup-topper",top,Vector4.zero);
    }
    static void BuildFooter()
    {
        // One continuous footer frame, never eight independently rounded tiles.
        var right=Crop(Raw["shop"],new Rect(455,1140,263,139),263,139,720,1280);
        var footer=new Texture2D(720,145,TextureFormat.RGBA32,false);var data=new Color[720*145];
        for(int y=0;y<145;y++)for(int x=0;x<720;x++)
        {
            float sx=x<45?263-x:x>675?263-(720-x):120;
            data[y*720+x]=right.GetPixelBilinear(sx/263,(y+.5f)/145);
        }
        footer.SetPixels(data);footer.Apply();UnityEngine.Object.DestroyImmediate(right);
        Save("footer",footer,new Vector4(36,30,36,30));
        Save("selected",Read("Assets/SqadSort/Content/UI/SquadTheme/Generated/mint-250x157.png"),new Vector4(28,28,28,28));
    }
    static void CardPlate(string name,Rect roi)
    {
        var card=Crop(Raw["shop"],roi,690,366,720,1280);
        var strip=Crop(Raw["shop"],new Rect(29,974,662,84),662,84,720,1280);
        // A blank footer is one layer; the real Button paints its green face once above it.
        var p=card.GetPixels();
        for(int y=0;y<91;y++)for(int x=0;x<638;x++)
        {
            int dx=x+26,dy=366-252-91+y;Color top=strip.GetPixelBilinear((x+.5f)/638,(y+.5f)/91);Color under=p[dy*690+dx];
            p[dy*690+dx]=Color.Lerp(under,top,top.a);
        }
        card.SetPixels(p);card.Apply();Save(name,card,Vector4.zero);UnityEngine.Object.DestroyImmediate(strip);
    }
    static void MakeMaterials()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SqadSort/Content/Fonts/LilitaOne/LilitaOne-Regular SDF.asset");
        White=Material(font,"White",Color.white,new Color(80f/255f,4f/255f,80f/255f),.14f);
        Body=Material(font,"Body",new Color(.33f,.10f,.04f),new Color(1,.97f,.89f),.14f);
        Green=Material(font,"Green",Color.white,new Color(0,64f/255f,0),.14f);
        Blue=Material(font,"Blue",Color.white,new Color(0,4f/255f,196f/255f),.14f);
    }
    static void MakeRoundedFont()
    {
        const string path="Assets/SqadSort/Content/Fonts/Baloo2/Baloo2-ExtraBold SDF.asset";
        RoundedFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(RoundedFont!=null)return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/SqadSort/Content/Fonts/Baloo2/Baloo2-ExtraBold.ttf");
        RoundedFont=TMP_FontAsset.CreateFontAsset(source,90,12,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        RoundedFont.name="Baloo2-ExtraBold SDF";
        RoundedFont.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 $.,:!?+-/()%×∞áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ");
        AssetDatabase.CreateAsset(RoundedFont,path);
        AssetDatabase.AddObjectToAsset(RoundedFont.material,RoundedFont);
        foreach(var atlas in RoundedFont.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,RoundedFont);
        RoundedFont.atlasPopulationMode=AtlasPopulationMode.Static;
        EditorUtility.SetDirty(RoundedFont);AssetDatabase.SaveAssets();
    }
    static Material Material(TMP_FontAsset font,string name,Color face,Color outline,float width)
    {
        string path=Art+"/Native/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(font.material);AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_FaceColor",face);m.SetColor("_OutlineColor",outline);m.SetFloat("_OutlineWidth",width);m.EnableKeyword("OUTLINE_ON");EditorUtility.SetDirty(m);return m;
    }
    static Material ForFont(TMP_Text text,Material role)
    {
        string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(text.font));string key=role.name+"-"+id;
        if(FontMaterials.TryGetValue(key,out var cached))return cached;
        string path=Art+"/Native/"+key+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(text.font.material);AssetDatabase.CreateAsset(m,path);}
        m.CopyPropertiesFromMaterial(text.font.material);m.SetColor("_FaceColor",role.GetColor("_FaceColor"));m.SetColor("_OutlineColor",role.GetColor("_OutlineColor"));m.SetFloat("_OutlineWidth",role.GetFloat("_OutlineWidth"));
        m.DisableKeyword("UNDERLAY_ON");m.DisableKeyword("UNDERLAY_INNER");m.DisableKeyword("GLOW_ON");m.SetFloat("_FaceDilate",.15f);m.SetFloat("_OutlineSoftness",0);m.EnableKeyword("OUTLINE_ON");
        if(role!=Body) { m.EnableKeyword("UNDERLAY_ON");m.SetColor("_UnderlayColor",role.GetColor("_OutlineColor"));m.SetFloat("_UnderlayOffsetX",0);m.SetFloat("_UnderlayOffsetY",-.75f);m.SetFloat("_UnderlayDilate",.30f);m.SetFloat("_UnderlaySoftness",0); }
        EditorUtility.SetDirty(m);FontMaterials[key]=m;return m;
    }
}















