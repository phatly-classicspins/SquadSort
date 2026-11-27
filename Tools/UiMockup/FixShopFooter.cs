using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FixShopFooter
{
    public static string Main()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)return "BUSY";
        foreach(var path in new[]{"Assets/SqadSort/Prefabs/Home/HomeView.prefab","Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/ShopUI.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var footer=root.GetComponentInChildren<Game.Views.HomeFooterUI>(true);
                var frame=footer.GetComponent<Image>();
                var backing=footer.transform.Find("OpaqueBackdrop") as RectTransform;
                if(backing==null)backing=NewRect("OpaqueBackdrop",footer.transform);
                backing.anchorMin=Vector2.zero;backing.anchorMax=new Vector2(1,0);backing.pivot=new Vector2(.5f,0);backing.anchoredPosition=Vector2.zero;backing.sizeDelta=new Vector2(0,210);backing.SetAsFirstSibling();
                var raw=backing.GetComponent<RawImage>()??backing.gameObject.AddComponent<RawImage>();
                raw.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/shop-background-pink-720x1280.png")??AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/garden.png");raw.uvRect=new Rect(0,0,1,210f/1280f);raw.color=Color.white;raw.raycastTarget=false;
                var plate=footer.transform.Find("Frame") as RectTransform;
                if(plate==null)plate=NewRect("Frame",footer.transform);
                Stretch(plate);plate.SetSiblingIndex(1);
                var image=plate.GetComponent<Image>()??plate.gameObject.AddComponent<Image>();image.sprite=frame.sprite;image.color=frame.color;image.type=frame.type;image.raycastTarget=false;frame.enabled=false;

                var content=footer.transform.Find("MainTab/Content");
                var selected=content.Find("Selected");
                var group=content.Find("Unselected") as RectTransform;
                if(group==null)group=NewRect("Unselected",content);
                Stretch(group);
                foreach(string name in new[]{"Icon","Label"})
                {
                    var source=selected.Find(name);var clone=group.Find(name);
                    if(clone==null){clone=UnityEngine.Object.Instantiate(source,group,false);clone.name=name;}
                    var a=(RectTransform)source;var b=(RectTransform)clone;b.anchorMin=a.anchorMin;b.anchorMax=a.anchorMax;b.pivot=a.pivot;b.sizeDelta=a.sizeDelta;b.anchoredPosition3D=a.anchoredPosition3D;b.localScale=a.localScale;b.localRotation=a.localRotation;
                    clone.gameObject.SetActive(true);
                }
                var shopRest=footer.transform.Find("ShopTab/Content/Unselected");
                foreach(string name in new[]{"Icon","Label"})
                {
                    var source=(RectTransform)shopRest.Find(name);var target=(RectTransform)group.Find(name);
                    var center=Center(footer.transform,target);var reference=Center(footer.transform,source);
                    target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,source.rect.width);
                    target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,source.rect.height);
                    var current=Center(footer.transform,target);
                    target.position+=footer.transform.TransformVector(new Vector3(center.x-current.x,reference.y-current.y,0));
                    if(name=="Icon")target.GetComponent<Image>().preserveAspect=true;
                    else { var a=source.GetComponent<TMP_Text>();var b=target.GetComponent<TMP_Text>();b.fontSize=a.fontSize;b.enableAutoSizing=a.enableAutoSizing;b.fontSizeMin=a.fontSizeMin;b.fontSizeMax=a.fontSizeMax;b.alignment=a.alignment;b.margin=a.margin; }
                }
                var old=content.Find("Icon");if(old!=null)old.gameObject.SetActive(false);
                var so=new SerializedObject(footer);var tabs=so.FindProperty("_tabs");
                for(int i=0;i<tabs.arraySize;i++)
                {
                    var tab=tabs.GetArrayElementAtIndex(i);if(tab.FindPropertyRelative("tab").enumValueIndex!=1)continue;
                    tab.FindPropertyRelative("deselected").objectReferenceValue=group.gameObject;
                    tab.FindPropertyRelative("unselectedLabel").objectReferenceValue=group.Find("Label").GetComponent<TMP_Text>();
                }
                so.ApplyModifiedPropertiesWithoutUndo();group.gameObject.SetActive(path.Contains("ShopUI"));

                if(path.Contains("ShopUI"))
                {
                    var scroll=root.transform.Find("SafeArea/Panel/Scroll") as RectTransform;
                    scroll.offsetMin=new Vector2(scroll.offsetMin.x,69);
                    var viewport=root.transform.Find("SafeArea/Panel/Scroll/Viewport");
                    if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
                    var label=root.transform.Find("SafeArea/Panel/Scroll/Viewport/Content/RestoreButton/Label").GetComponent<TMP_Text>();
                    var rt=label.rectTransform;Stretch(rt);rt.offsetMin=new Vector2(12,5);rt.offsetMax=new Vector2(-12,-5);label.margin=Vector4.zero;label.alignment=TextAlignmentOptions.Midline;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Restore centered; localized Home caption added to deselected tab; opaque footer ground and bounded Shop viewport authored";
    }
    static RectTransform NewRect(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;r.localScale=Vector3.one;}
    static Vector3 Center(Transform root,RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return root.InverseTransformPoint((c[0]+c[2])*.5f);}
}


