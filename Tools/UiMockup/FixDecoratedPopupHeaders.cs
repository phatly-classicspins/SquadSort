using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class FixDecoratedPopupHeaders
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var body=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/popup-body.png");
        var topper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/popup-topper.png");
        var glyphSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/close-glyph.png");
        foreach(string name in new[]{"StarterBundle","PurchaseFailed"})
        {
            string path="Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/"+name+"UI.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            var go=new GameObject("MeasurementCanvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(go,root.scene);
            ((RectTransform)go.transform).sizeDelta=new Vector2(720,1280);go.GetComponent<Canvas>().referencePixelsPerUnit=1;root.transform.SetParent(go.transform,false);
            try
            {
                Canvas.ForceUpdateCanvases();
                var images=root.GetComponentsInChildren<Image>(true);
                var frame=images.Single(i=>i.name=="Background");
                var ribbon=images.Single(i=>i.name=="Ribbon");
                var r=frame.rectTransform;var h=ribbon.rectTransform;
                float width=ribbon.sprite==topper?h.rect.width*415f/515f:h.rect.width;
                frame.sprite=body;frame.type=Image.Type.Sliced;frame.pixelsPerUnitMultiplier=1;
                ribbon.sprite=topper;ribbon.type=Image.Type.Simple;
                h.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width*515f/415f);
                h.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,width*236f/415f);
                float target=r.rect.yMax-15;
                var purple=h.TransformPoint(new Vector3(h.rect.center.x,Mathf.Lerp(h.rect.yMax,h.rect.yMin,167f/236f),0));
                h.position+=r.TransformPoint(new Vector3(r.rect.center.x,target,0))-purple;
                var title=root.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Title"&&t.gameObject.activeInHierarchy);
                title.transform.position+=r.TransformPoint(new Vector3(r.rect.center.x,target,0))-title.rectTransform.TransformPoint(title.rectTransform.rect.center);
                var close=root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CloseButton"&&b.gameObject.activeInHierarchy);
                var closeRect=(RectTransform)close.transform;
                var buttonPoint=h.TransformPoint(new Vector3(Mathf.Lerp(h.rect.xMin,h.rect.xMax,475f/515f),Mathf.Lerp(h.rect.yMax,h.rect.yMin,145f/236f),0));
                close.transform.position+=buttonPoint-closeRect.TransformPoint(closeRect.rect.center);
                // The shared topper already draws the red button; retain only one white close glyph.
                close.image.color=Color.clear;
                var glyph=close.transform.Find("HeaderCloseGlyph") as RectTransform;
                if(glyph==null){var glyphGo=new GameObject("HeaderCloseGlyph",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));glyph=(RectTransform)glyphGo.transform;glyph.SetParent(close.transform,false);}
                glyph.anchorMin=glyph.anchorMax=glyph.pivot=new Vector2(.5f,.5f);glyph.anchoredPosition=Vector2.zero;glyph.sizeDelta=new Vector2(44,43)*width/415f;
                var glyphImage=glyph.GetComponent<Image>();glyphImage.sprite=glyphSprite;glyphImage.color=Color.white;glyphImage.raycastTarget=false;
                root.transform.SetParent(null,false);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);UnityEngine.Object.DestroyImmediate(go);}
        }
        return "Aligned StarterBundle and PurchaseFailed with existing shared body/topper assets";
    }
}
