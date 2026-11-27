using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Editor diagnostic executed only through Unity-MCP; never imported into Assets.
public static class AlignPopupHeaders
{
    public static string Main()=>Run(false);
    public static string Apply()=>Run(true);
    static string Run(bool apply)
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var rows=new List<object>();
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Vendor/MoneySort/Source/Game/Prefabs/UI","Assets/Vendor/MoneySort/Settings/Prefabs","Assets/SqadSort/Prefabs/UI"}).Select(AssetDatabase.GUIDToAssetPath);
        foreach(var path in paths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            var canvasGo=new GameObject("MeasurementCanvas",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasGo,root.scene);
            ((RectTransform)canvasGo.transform).sizeDelta=new Vector2(720,1280);
            canvasGo.GetComponent<Canvas>().referencePixelsPerUnit=1;
            root.transform.SetParent(canvasGo.transform,false);
            try
            {
                Canvas.ForceUpdateCanvases();
                var images=root.GetComponentsInChildren<Image>(true);
                var headers=images.Where(i=>Visible(i)&&i.sprite!=null&&(i.name=="MockupTopper"||i.sprite.name.StartsWith("header-")&&(i.name=="BoosterHeader"||i.name=="TitleRibbon"||i.name=="Ribbon"))).ToArray();
                foreach(var topper in headers)
                {
                    Image panel=null;
                    for(var parent=topper.transform.parent;parent!=null;parent=parent.parent){var image=parent.GetComponent<Image>();if(image!=null&&Visible(image)&&image.sprite!=null&&image.sprite.name.StartsWith("popup-")){panel=image;break;}}
                    if(panel==null)panel=images.Where(i=>Visible(i)&&i.sprite!=null&&i.sprite.name.StartsWith("popup-")).OrderBy(i=>Vector3.Distance(i.transform.position,topper.transform.position)).FirstOrDefault();
                    if(panel==null){rows.Add(new{path,warning="No popup body",topper=topper.name});continue;}
                    var r=panel.rectTransform;
                    bool combined=topper.name=="MockupTopper";
                    var p=r.InverseTransformPoint(topper.rectTransform.TransformPoint(new Vector3(topper.rectTransform.rect.center.x,Mathf.Lerp(topper.rectTransform.rect.yMax,topper.rectTransform.rect.yMin,combined?167f/236f:.5f),0)));
                    // The top rim is 30 native pixels deep at its straight horizontal section.
                    float rim=15f/(panel.pixelsPerUnit*panel.pixelsPerUnitMultiplier);
                    float target=r.rect.yMax-rim;
                    float delta=target-p.y;
                    var header=combined?topper.transform.parent:topper.transform;
                    float deltaX=r.rect.center.x-p.x;
                    rows.Add(new{path,panel=panel.name,header=header.name,panelTop=r.rect.yMax,purpleCenter=p.y,rimCenter=target,moveY=delta,moveX=deltaX,ppu=panel.pixelsPerUnit});
                    if(apply)
                    {
                        if(Mathf.Abs(delta)>180||Mathf.Abs(deltaX)>60)throw new Exception("Unexpected offset: "+path);
                        var move=r.TransformVector(new Vector3(deltaX,delta,0));
                        header.position+=move;
                        var close=panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="CloseButton");
                        if(close!=null&&!close.transform.IsChildOf(header))close.transform.position+=move;
                        // Some authored popups keep Title and Close as siblings of the ribbon.
                        var detached=root.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.gameObject.activeInHierarchy
                            &&(t.name=="Title"||t.name=="BoosterName")&&!t.transform.IsChildOf(header)
                            &&!t.GetComponentsInParent<Transform>(true).Any(p=>p.CompareTag("EditorOnly"))).ToArray();
                        foreach(var label in detached)
                        {
                            var center=label.rectTransform.TransformPoint(label.rectTransform.rect.center);
                            var labelMove=r.TransformPoint(new Vector3(r.rect.center.x,target,0))-center;
                            label.transform.position+=labelMove;
                            if(close==null&&labelMove.sqrMagnitude>1)
                            {
                                var detachedClose=root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name=="CloseButton"&&!b.transform.IsChildOf(header));
                                if(detachedClose!=null)detachedClose.transform.position+=labelMove;
                            }
                        }
                    }
                }
                if(!images.Any(i=>i.name=="MockupTopper")&&images.Any(i=>i.sprite!=null&&i.sprite.name=="popup-body"))
                    rows.Add(new{path,other=images.Where(i=>i.sprite!=null).Select(i=>new{name=i.name,sprite=i.sprite.name}).ToArray()});
                if(apply&&headers.Length>0)
                {
                    root.transform.SetParent(null,false);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);UnityEngine.Object.DestroyImmediate(canvasGo);}
        }
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("docs/art/popup-header-alignment-20261127");
        File.WriteAllText("docs/art/popup-header-alignment-20261127/"+(apply?"applied":"inspection")+".json",json);
        return json;
    }
    static bool Visible(Image image)
    {
        if(!image.enabled||!image.gameObject.activeInHierarchy)return false;
        for(var t=image.transform;t!=null;t=t.parent)if(t.CompareTag("EditorOnly"))return false;
        return true;
    }
}
