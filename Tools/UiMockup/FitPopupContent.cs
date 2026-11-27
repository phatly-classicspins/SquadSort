using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json.Linq;

public static class FitPopupContent
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var plan=JArray.Parse(File.ReadAllText("docs/art/popup-content-review-20261127/content-plan.json"));
        int changes=0;
        foreach(var group in plan.GroupBy(o=>(string)o["prefab"]))
        {
            var root=PrefabUtility.LoadPrefabContents(group.Key);
            var go=new GameObject("LayoutMeasurement",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(go,root.scene);
            ((RectTransform)go.transform).sizeDelta=new Vector2(720,1280);go.GetComponent<Canvas>().referencePixelsPerUnit=1;root.transform.SetParent(go.transform,false);
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach(var op in group)
                {
                    var node=root.transform.Find((string)op["node"]);
                    if(node==null)throw new Exception("Missing "+group.Key+"/"+op["node"]);
                    var text=node.GetComponent<TMP_Text>();var role=(string)op["role"];
                    if(op["rect"] is JArray box)
                    {
                        var b=box.ToObject<float[]>();var r=text.rectTransform;
                        r.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,b[2]-b[0]);r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,b[3]-b[1]);
                        r.position+=root.transform.TransformPoint(new Vector3((b[0]+b[2])/2,(b[1]+b[3])/2,0))-r.TransformPoint(r.rect.center);
                        text.alignment=TextAlignmentOptions.Midline;
                    }
                    float max=text.enableAutoSizing?text.fontSizeMax:text.fontSize;
                    text.enableAutoSizing=true;text.fontSizeMax=max;text.fontSizeMin=Math.Min(max*.65f,Math.Max(24,text.fontSizeMin));
                    text.textWrappingMode=role=="body"?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
                    text.overflowMode=TextOverflowModes.Overflow;
                    if(role=="title")text.alignment=TextAlignmentOptions.Midline;
                    changes++;
                }
                var images=root.GetComponentsInChildren<Image>(true);
                var frame=images.FirstOrDefault(i=>i.sprite!=null&&i.sprite.name=="popup-body"&&i.enabled);
                if(frame!=null)
                {
                    float center=RootRect(root,frame.rectTransform).center.x;
                    foreach(var icon in images.Where(i=>i.sprite!=null&&
                        (i.name=="BoosterIcon"&&!Ancestors(i.transform).Any(t=>t.name.Contains("Button"))||i.name=="Icon"&&(group.Key.Contains("PurchaseFailed")||group.Key.Contains("RestartBooster")||group.Key.Contains("SkipLevel")))))
                    {
                        CenterX(root,icon.rectTransform,center);icon.preserveAspect=true;
                        if(group.Key.Contains("PurchaseFailed")){icon.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,180);icon.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,180);}
                    }
                    var heart=images.FirstOrDefault(i=>i.name=="HeartImage");
                    if(heart!=null){CenterX(root,heart.rectTransform,center);heart.preserveAspect=true;}
                    var visual=images.FirstOrDefault(i=>i.name=="RentVisual");
                    if(visual!=null)
                    {
                        var bounds=visual.GetComponentsInChildren<Image>().Where(i=>i.enabled).Select(i=>RootRect(root,i.rectTransform)).ToArray();
                        float groupCenter=(bounds.Min(b=>b.xMin)+bounds.Max(b=>b.xMax))/2;
                        visual.transform.position+=root.transform.TransformVector(new Vector3(center-groupCenter,0,0));
                    }
                    if(group.Key.Contains("GarageGift"))
                    {
                        var undo=images.Single(i=>i.name=="FreeMoveIcon");var coins=images.Single(i=>i.name=="CoinsIcon");
                        undo.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,128);undo.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,128);undo.preserveAspect=true;
                        CenterX(root,undo.rectTransform,center-110);CenterX(root,coins.rectTransform,center+110);coins.preserveAspect=true;
                        var undoCenter=RootRect(root,undo.rectTransform).center;undo.transform.position+=root.transform.TransformVector(new Vector3(0,64-undoCenter.y,0));
                        var ui=root.GetComponent<Game.Views.RewardedAdPromptUI>();var so=new SerializedObject(ui);var labels=so.FindProperty("_rewardTexts");
                        for(int i=0;i<Math.Min(2,labels.arraySize);i++){var label=labels.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;if(label!=null)CenterX(root,label.rectTransform,center+(i==0?-110:110));}
                    }
                }
                root.transform.SetParent(null,false);PrefabUtility.SaveAsPrefabAsset(root,group.Key);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);UnityEngine.Object.DestroyImmediate(go);}
        }
        return "Adjusted "+changes+" text blocks and primary icon alignment through prefab authoring";
    }
    static IEnumerable<Transform> Ancestors(Transform t){for(;t!=null;t=t.parent)yield return t;}
    static Rect RootRect(GameObject root,RectTransform t){var a=root.transform.InverseTransformPoint(t.TransformPoint(t.rect.min));var b=root.transform.InverseTransformPoint(t.TransformPoint(t.rect.max));return new Rect(a.x,a.y,b.x-a.x,b.y-a.y);}
    static void CenterX(GameObject root,RectTransform t,float x){float current=RootRect(root,t).center.x;t.position+=root.transform.TransformVector(new Vector3(x-current,0,0));}
}
