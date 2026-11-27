using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Executed only as a Unity-MCP snippet. No script, prefab, scene or import is added to Assets.
public static class CaptureNativePreview
{
    const string Out = "docs/art/ui-native-20261125";
    const string Vendor = "Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/";
    static readonly string[] Pages = { "Home", "GameplayHud", "Settings", "Shop", "LevelUp", "Lose", "OutOfLives", "OutOfSpace", "BoosterPurchase", "BoosterUnlock", "ElementUnlock", "StarterBundle", "RemoveAds", "GarageGift", "SkipLevel", "RestartBooster", "RentSlot", "PurchaseReward", "PurchaseFailed", "NoInternet", "NotificationPermission", "Rating", "Loading", "Error", "RestorePurchase" };
    static readonly Dictionary<string, string> Paths = new Dictionary<string, string>();
    static readonly List<Texture2D> Textures = new List<Texture2D>();
    static readonly List<Sprite> Sprites = new List<Sprite>();
    static readonly Dictionary<Sprite, Sprite> Replacements = new Dictionary<Sprite, Sprite>();
    static readonly Dictionary<string, string> AssetRoles = new Dictionary<string, string>();

    [Serializable] public class Node { public string path; public string sprite; public string texture; public string role; public string text; public float[] rect; public float[] native; public float[] anchor; public float[] size; public float[] pivot; public float[] scale; public float fontSize; public bool active; }
    [Serializable] public class Page { public string name; public string prefab; public Node[] nodes; }
    [Serializable] public class Catalog { public int width=720; public int height=1280; public Page[] pages; }
    static string PathOf(Transform t, Transform root) { return t==root ? root.name : PathOf(t.parent,root)+"/"+t.name; }

    public static string Main(string mode = "before")
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return "BUSY";
        Paths["Home"]="Assets/SqadSort/Prefabs/Home/HomeView.prefab";
        Paths["GameplayHud"]="Assets/SqadSort/Prefabs/Gameplay/GameplayHud.prefab";
        Paths["Settings"]="Assets/Vendor/MoneySort/Settings/Prefabs/SettingsUI.prefab";
        Paths["RestorePurchase"]="Assets/SqadSort/Prefabs/UI/RestorePurchaseUI.prefab";
        Directory.CreateDirectory(Out+"/"+mode);
        var scene = EditorSceneManager.NewPreviewScene();
        var catalog = new List<Page>();
        var originals = new Dictionary<string,string>();
        var previous = RenderTexture.active;
        try
        {
            var cameraGo = new GameObject("Mockup Camera");
            SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=640;
            camera.transform.position=new Vector3(0,0,-100);
            camera.nearClipPlane=.1f;camera.farClipPlane=500;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.15f,.24f,1);
            var rt=new RenderTexture(720,1280,24,RenderTextureFormat.ARGB32);rt.antiAliasing=1;rt.Create();
            camera.targetTexture=rt;camera.scene=scene;
            var canvasGo=new GameObject("Mockup Canvas",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasGo,scene);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvas.referencePixelsPerUnit=1;
            ((RectTransform)canvasGo.transform).sizeDelta=new Vector2(720,1280);
            foreach(string name in Pages)
            {
                string path=Paths.ContainsKey(name)?Paths[name]:Vendor+name+"UI.prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)continue;
                var root=UnityEngine.Object.Instantiate(prefab,canvasGo.transform,false);
                root.SetActive(true);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(t.CompareTag("EditorOnly"))t.gameObject.SetActive(false);
                foreach(var g in root.GetComponentsInChildren<CanvasGroup>(true))g.alpha=1;
                Canvas.ForceUpdateCanvases();
                var before=Snapshot(root,camera);
                if(mode=="after")
                {
                    foreach(var image in root.GetComponentsInChildren<Image>(true))
                        if(image.sprite!=null)image.sprite=Replacement(image.sprite,image.rectTransform);
                    Canvas.ForceUpdateCanvases();
                    var after=Snapshot(root,camera);
                    for(int i=0;i<before.Length;i++)
                        if(!before[i].rect.SequenceEqual(after[i].rect)||!before[i].anchor.SequenceEqual(after[i].anchor)||!before[i].size.SequenceEqual(after[i].size)||!before[i].scale.SequenceEqual(after[i].scale))
                            throw new InvalidOperationException("Layout drift: "+name+"/"+before[i].path);
                }
                catalog.Add(new Page{name=name,prefab=path,nodes=before});
                camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;
                var tex=new Texture2D(720,1280,TextureFormat.RGBA32,false);
                tex.ReadPixels(new Rect(0,0,720,1280),0,0);tex.Apply();
                File.WriteAllBytes(Out+"/"+mode+"/"+name+".png",tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(root);
            }
            File.WriteAllText(Out+"/"+mode+"-layout.json",Newtonsoft.Json.JsonConvert.SerializeObject(new Catalog{pages=catalog.ToArray()},Newtonsoft.Json.Formatting.Indented));
            camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            return "Captured "+catalog.Count+" screens at 720x1280; mode="+mode+"; no persistent assets modified";
        }
        finally
        {
            RenderTexture.active=previous;
            EditorSceneManager.ClosePreviewScene(scene);
            foreach(var s in Sprites)if(s!=null)UnityEngine.Object.DestroyImmediate(s);
            foreach(var t in Textures)if(t!=null)UnityEngine.Object.DestroyImmediate(t);
            Sprites.Clear();Textures.Clear();Replacements.Clear();NativeCache.Clear();
        }
    }

    static Node[] Snapshot(GameObject root,Camera camera)
    {
        return root.GetComponentsInChildren<RectTransform>(true).Select(t=>{
            var corners=new Vector3[4];t.GetWorldCorners(corners);
            var lo=camera.WorldToScreenPoint(corners[0]);var hi=camera.WorldToScreenPoint(corners[2]);
            var image=t.GetComponent<Image>();var text=t.GetComponent<TMP_Text>();var sprite=image==null?null:image.sprite;
            return new Node{path=PathOf(t,root.transform),sprite=sprite==null?null:sprite.name,
                texture=sprite==null?null:AssetDatabase.GetAssetPath(sprite.texture),role=sprite==null?null:Role(sprite.name,t.name),text=text==null?null:text.text,
                rect=new[]{lo.x,1280-hi.y,hi.x-lo.x,hi.y-lo.y},native=new[]{t.rect.width,t.rect.height},anchor=new[]{t.anchorMin.x,t.anchorMin.y,t.anchorMax.x,t.anchorMax.y},
                size=new[]{t.sizeDelta.x,t.sizeDelta.y},pivot=new[]{t.pivot.x,t.pivot.y},scale=new[]{t.localScale.x,t.localScale.y,t.localScale.z},fontSize=text==null?0:text.fontSize,active=t.gameObject.activeInHierarchy};
        }).ToArray();
    }

    // Role mapping and generated texture input are finalized after inventory/visual inspection.
    static string Role(string sprite,string node)
    {
        string a=sprite.ToLowerInvariant(),n=node.ToLowerInvariant();
        if(a=="toggleon")return "toggle";
        if(a=="toggleoff")return "toggle-off";
        if(a=="settings")return "settings";
        if(a=="replay")return "restart";
        if(a=="hudicon 1"||a=="prompt_close"||a=="starter_popup_close")return "close";
        if(a=="btn_plus_icon 1"||a=="win_wallet_plus")return "plus";
        if(a=="heart")return "heart";
        if(a=="icn_coin"||a=="poker coin"||a=="win_wallet_coin")return "coin";
        if(a.StartsWith("chip_away_lock"))return "lock";
        if(a.StartsWith("c5a6fba0"))return "home";
        if(a.StartsWith("dfb3a98d"))return "shop";
        if(a=="checkmark")return "check";
        if(a=="icon_gifr 1")return "gift";
        if(a=="menu_bar 1"||a=="shop_ui 2")return "footer";
        if(a=="rectangle 7 1 1")return "selected";
        if(a=="win_wallet"||a=="coin slab"||a=="rectangle 25"||a=="level slab"||a.Contains("timer_pill"))return "pill";
        if(a=="booster count")return "badge";
        if(a=="boosters buttons"||a=="panel_3 1")return "purple";
        if(a.Contains("title")||a.Contains("ribbon")||a=="win_feature_tag")
        {
            if(a=="title 2"||a=="title 1")return a=="title 2"?"keep":"header";
            return "header";
        }
        if(a=="shop_btn_green"||a=="level_normal_button2 1"||a=="c8feb63ee9d6ee6562294b57c79e5e63815c5d95"||a.StartsWith("1c62b683"))return "lime";
        if(a=="bluebutton"||a.StartsWith("yellow_button")||a=="button_red_1 1")return "purple";
        if(a.StartsWith("popup")||a.StartsWith("panel1")||a.StartsWith("panel2")||a=="starter_popup_panel"||a=="starter_popup_card"||a=="slot_popup"||a=="rent_panel 1"||a=="no_internet_card"||a=="booster panel"||a=="shop_small_panel 2 1"||a=="rectangle 32"||a=="win_feature_box_border"||a=="win_feature_track_border"||a=="6912889d689a77c979223e33ff11f1dfb2a07bb3")return "panel";
        if(a=="rectangle 7"||a=="rectangle 50"||a=="rectangle 49")return "header";
        return "keep";
    }
    static readonly Dictionary<string,Sprite> NativeCache=new Dictionary<string,Sprite>();
    static Sprite Replacement(Sprite original,RectTransform rect)
    {
        string role=Role(original.name,rect.name);if(role=="keep")return original;
        string key=role+"_"+rect.rect.width.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"x"+rect.rect.height.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        if(NativeCache.ContainsKey(key))return NativeCache[key];
        string path=Out+"/assets/"+key+".png";
        if(!File.Exists(path))throw new FileNotFoundException(path);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        texture.LoadImage(File.ReadAllBytes(path));texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;Textures.Add(texture);
        if(texture.width!=Mathf.CeilToInt(rect.rect.width)||texture.height!=Mathf.CeilToInt(rect.rect.height))throw new InvalidOperationException("Native dimensions mismatch: "+key);
        var sprite=Sprite.Create(texture,new Rect(0,0,rect.rect.width,rect.rect.height),rect.pivot,1f,0,SpriteMeshType.FullRect,Vector4.zero);
        sprite.name=key;Sprites.Add(sprite);NativeCache[key]=sprite;return sprite;
    }
}
