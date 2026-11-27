using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using ClassicSpins.PrototypeFramework.Composition;
using ClassicSpins.PrototypeFramework.Presentation;
using ClassicSpins.PrototypeFramework.Application;
using Game.Views;
using Game.Gen;

// Transient gallery fixtures, compiled through Unity-MCP, never imported into Assets.
public static class UiReviewCapture
{
    static GameObject shown;
    static readonly Action Noop=()=>{};
    const string Vendor="Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/";
    public static string Main(){SessionState.SetString("SquadSort.UiReviewAssembly",typeof(UiReviewCapture).Assembly.FullName);return "Gallery ready; no persistent writes";}
    public static string Clear(){if(shown!=null)UnityEngine.Object.DestroyImmediate(shown);shown=null;return "Cleared preview";}
    static float[] Box(RectTransform rect,Rect box)
    {
        var a=shown.transform.InverseTransformPoint(rect.TransformPoint(box.min));
        var b=shown.transform.InverseTransformPoint(rect.TransformPoint(box.max));
        return new[]{a.x,a.y,b.x,b.y};
    }
    static string PathOf(Transform node)=>node==shown.transform?node.name:PathOf(node.parent)+"/"+node.name;
    public static string Audit(string name)
    {
        if(shown==null)throw new Exception("No preview");
        Canvas.ForceUpdateCanvases();
        var texts=shown.GetComponentsInChildren<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>{
            t.ForceMeshUpdate();var bounds=t.textBounds;
            return new{path=PathOf(t.transform),text=t.text,font=t.fontSize,auto=t.enableAutoSizing,min=t.fontSizeMin,max=t.fontSizeMax,alignment=t.alignment.ToString(),overflow=t.isTextOverflowing,rect=Box(t.rectTransform,t.rectTransform.rect),ink=Box(t.rectTransform,new Rect(bounds.min.x,bounds.min.y,bounds.size.x,bounds.size.y)),preferred=new[]{t.preferredWidth,t.preferredHeight}};
        }).ToArray();
        var graphics=shown.GetComponentsInChildren<Image>().Where(i=>i.enabled&&i.gameObject.activeInHierarchy&&i.sprite!=null).Select(i=>new{path=PathOf(i.transform),sprite=i.sprite.name,rect=Box(i.rectTransform,i.rectTransform.rect),aspect=i.preserveAspect}).ToArray();
        string folder=System.Environment.GetEnvironmentVariable("UI_REVIEW_AUDIT_DIR")??"docs/art/popup-content-review-20261127/metrics";
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(folder+"/"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{name,texts,graphics},Newtonsoft.Json.Formatting.Indented));
        return name+": measured "+texts.Length+" text blocks and "+graphics.Length+" graphics";
    }
    public static string Show(string page)
    {
        Clear();
        string source=page;
        if(page.StartsWith("Settings"))source="Settings";
        if(page.StartsWith("Rating"))source="Rating";
        if(page.StartsWith("BoosterUnlock"))source="BoosterUnlock";
        if(page.StartsWith("BoosterPurchase"))source="BoosterPurchase";
        if(page.StartsWith("ElementUnlock"))source="ElementUnlock";
        if(page=="ConfirmHome")source="Lose";
        string path=source=="Settings"?"Assets/Vendor/MoneySort/Settings/Prefabs/SettingsUI.prefab":source=="RestorePurchase"?"Assets/SqadSort/Prefabs/UI/RestorePurchaseUI.prefab":Vendor+source+"UI.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null)throw new Exception("Missing "+path);
        var root=UnityEngine.Object.FindFirstObjectByType<RootLifetimeScope>();
        var layers=(IRenderLayerRegistry)root.Container.Resolve(typeof(IRenderLayerRegistry));
        var loc=(ILocalizationService)root.Container.Resolve(typeof(ILocalizationService));
        shown=UnityEngine.Object.Instantiate(prefab,layers.GetHost(RenderLayers.Popup),false);
        layers.Stamp(shown,RenderLayers.Popup);
        var ui=shown.GetComponent<UIBase>();ui.Init(null);
        if(ui is SettingsUI settings){settings.SetData(new SettingsPresentation(Noop,Noop,Noop,page=="SettingsHome",Noop,Noop));settings.SetLabels(loc.Get(LocKeys.SettingsTitle),new[]{loc.Get(LocKeys.SettingsSound),loc.Get(LocKeys.SettingsMusic),loc.Get(LocKeys.SettingsHaptics)},loc.Get(LocKeys.PauseRestart),loc.Get(LocKeys.PauseHome));settings.SetLinkLabels(loc.Get(LocKeys.SettingsTerms),loc.Get(LocKeys.SettingsPrivacy));}
        if(ui is LoseUI lose)lose.SetData(new LosePresentation("Are you sure?","You will lose a life!",page=="ConfirmHome"?"Return Home":"Restart",Noop,Noop,page=="ConfirmHome"?LoseButtonStyle.ReturnHome:LoseButtonStyle.Restart));
        if(ui is OutOfLivesUI lives)lives.SetData(new OutOfLivesPresentation(900,5,1,Noop,Noop,Noop));
        if(ui is OutOfSpaceUI space)space.SetData(new OutOfSpaceOffer(900,2000,Noop,Noop,Noop,false));
        if(ui is RewardedAdPromptUI reward)reward.SetData(new RewardedAdPromptOffer(Noop,Noop,source=="GarageGift"?(Func<int>)(()=>598):null,source=="GarageGift"?new[]{"x1","200"}:new[]{"x1"}));
        if(source=="SkipLevel"||source=="RestartBooster"){
            bool skip=source=="SkipLevel";
            ui.SetLabel("@PromptCard/Title",loc.Get(skip?LocKeys.SkipTitle:LocKeys.RestartBoosterTitle));
            ui.SetLabel("@PromptCard/Description",loc.Get(skip?LocKeys.SkipBody:LocKeys.RestartBoosterBody));
        }
        if(ui is RatingUI rating)rating.SetData(page=="RatingThanks"?(object)new RatingThankYouOffer("Thank you for your support!",Noop):new RatingOffer(_=>{},Noop));
        if(ui is RatingUI)ui.SetLabel("_titleText",loc.Get(page=="RatingThanks"?LocKeys.RatingThanksTitle:LocKeys.RatingTitle));
        if(ui is RestorePurchaseUI restore)restore.Bind("Restore Purchases","No purchases to restore.","OK",Noop);
        if(ui is RentSlotUI rent)rent.SetData(new RentSlotOffer(60,900,2000,1,Noop,Noop,Noop));
        if(ui is ErrorUI error)error.SetData("No ads available. Please try again later.",Noop);
        if(ui is BoosterPurchaseUI purchase)
        {
            bool shuffle=page.EndsWith("Shuffle"),remove=page.EndsWith("Remove");
            var type=shuffle?MoneySortBoosterType.Shuffle:remove?MoneySortBoosterType.Hammer:MoneySortBoosterType.FreeMove;
            var unlockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"BoosterUnlockUI.prefab").GetComponent<BoosterUnlockUI>();
            purchase.SetData(new BoosterPurchaseOffer(type,loc.Get(shuffle?LocKeys.BoosterShuffleName:remove?LocKeys.BoosterRemoveName:LocKeys.BoosterHandName).ToUpperInvariant(),loc.Get(shuffle?LocKeys.BoosterShuffleTip:remove?LocKeys.BoosterRemoveTip:LocKeys.BoosterHandTip),shuffle?1200:remove?1500:900,3,2000,unlockPrefab.Icon(type),Noop,Noop,Noop));
        }
        if(ui is PurchaseResultUI result)result.SetData(new PurchaseResultPresentation(new MoneySortShopReward{coins=2500,shuffle=2,hammer=2,freeMove=2,infiniteLivesMinutes=60},Noop));
        if(ui is BoosterUnlockUI booster)
        {
            var type=page.EndsWith("Shuffle")?MoneySortBoosterType.Shuffle:page.EndsWith("Remove")?MoneySortBoosterType.Hammer:MoneySortBoosterType.FreeMove;
            string name=page.EndsWith("Shuffle")?"Shuffle":page.EndsWith("Remove")?"Remove":"Undo";
            booster.SetData(new BoosterUnlockOffer(name,name=="Shuffle"?"Mix buddies between containers.":name=="Remove"?"Remove buddies of the same color.":"Undo your last move.",booster.Icon(type),type,null,Noop));
        }
        if(ui is ElementUnlockUI element){bool hidden=page.EndsWith("Hidden");element.SetData(new ElementUnlockOffer(loc.Get(hidden?LocKeys.ElementHiddenStackName:LocKeys.ElementHiddenPoleName),loc.Get(hidden?LocKeys.ElementHiddenStackTip:LocKeys.ElementHiddenPoleTip),MoneySortElementType.HiddenMoney,Noop));element.ShowIllustration(hidden?0:1);}
        if(ui is LoadingUI)
        {
            typeof(LoadingUI).GetMethod("SetProgress",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,new object[]{.6f});
            shown.SetActive(true);
        }
        else ui.Show();
        return "Preview "+page+" from "+path;
    }
}
