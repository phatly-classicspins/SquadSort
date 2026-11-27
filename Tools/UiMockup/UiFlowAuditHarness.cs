using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Presentation;
using Game.Views;
using Game.Gen;
using UnityEngine;

// Transient Unity-MCP diagnostic only. Never imported into Assets or registered in the game.
public static class UiFlowAuditHarness
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Out="docs/art/ui-flow-review-20261127";
    public static InMemoryProgressStore Memory;
    public static AuditWallet Wallet;
    static IProgressStore Original;
    static IWalletService OriginalWallet;
    static string OriginalJson;
    static long OriginalCoins;
    static AuditFacts Facts=new AuditFacts();
    static List<object> Trace=new List<object>();
    public static string Main()
    {
        if(!UnityEditor.EditorApplication.isPlaying)throw new Exception("Play mode required");
        UnityEditor.SessionState.SetString("SquadSort.FlowAuditAssembly",typeof(UiFlowAuditHarness).Assembly.FullName);
        Attach();
        return Snapshot("memory sandbox installed");
    }
    public static object Current()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
        if(hud!=null)return Owner(typeof(GameplayHudView).GetField("RestartPressed",Flags).GetValue(hud) as Delegate);
        var home=UnityEngine.Object.FindFirstObjectByType<HomeView>();
        if(home!=null)return Owner(typeof(HomeView).GetField("PlayPressed",Flags).GetValue(home) as Delegate);
        throw new Exception("No active screen view");
    }
    static object Owner(Delegate d)
    {
        if(d==null)throw new Exception("View event not bound");
        foreach(var call in d.GetInvocationList())
        {
            if(call.Target is GameplayScreen || call.Target is MainScreen)return call.Target;
            if(call.Target!=null)foreach(var f in call.Target.GetType().GetFields(Flags))
            {var v=f.GetValue(call.Target);if(v is GameplayScreen||v is MainScreen)return v;}
        }
        throw new Exception("Screen callback owner missing");
    }
    public static string Attach()
    {
        var queue=new Queue<object>();var seen=new HashSet<object>();queue.Enqueue(Current());int changed=0;
        while(queue.Count>0)
        {
            var item=queue.Dequeue();if(item==null||!seen.Add(item)||item is UnityEngine.Object)continue;
            var type=item.GetType();if(type.Namespace==null||!type.Namespace.StartsWith("Game."))continue;
            if(seen.Count>1000)throw new Exception("Unexpected service graph size");
            foreach(var field in type.GetFields(Flags))
            {
                var value=field.GetValue(item);if(value==null)continue;
                if(field.FieldType==typeof(IProgressStore))
                {
                    if(Memory==null){Original=(IProgressStore)value;OriginalJson=Newtonsoft.Json.JsonConvert.SerializeObject(Original.Progress);Memory=new InMemoryProgressStore();Memory.Progress.CopyFrom(Original.Progress.Clone());Memory.Settings.Sound=Original.Settings.Sound;Memory.Settings.Music=Original.Settings.Music;Memory.Settings.Haptics=Original.Settings.Haptics;}
                    if(!ReferenceEquals(value,Memory)){field.SetValue(item,Memory);changed++;}
                }
                else if(field.FieldType==typeof(IWalletService))
                {
                    if(Wallet==null){OriginalWallet=(IWalletService)value;OriginalCoins=OriginalWallet.Balance(SqadSortResources.Coin);Wallet=new AuditWallet();Wallet.Grant(SqadSortResources.Coin,OriginalCoins,GrantSource.Reward);}
                    if(!ReferenceEquals(value,Wallet)){field.SetValue(item,Wallet);changed++;}
                }
                else if(field.FieldType==typeof(IProgressFacts)){field.SetValue(item,Facts);changed++;}
                else if(!field.FieldType.IsValueType && value is not string && value is not Delegate)queue.Enqueue(value);
            }
        }
        if(Memory==null||Wallet==null)throw new Exception("Sandbox ports not found");
        return "Attached memory store/wallet/facts: "+changed+" service fields";
    }
    public static string Snapshot(string label)
    {
        var active=UnityEngine.Object.FindObjectsByType<UIBase>(FindObjectsSortMode.None).Select(v=>v.GetType().Name).Distinct().ToArray();
        var row=new{label,screen=Current().GetType().Name,active,memoryLevel=Memory.Progress.CurrentLevel,memoryLives=Memory.Progress.Lives,memoryCoins=Wallet.Balance(SqadSortResources.Coin),realProgressUnchanged=OriginalJson==Newtonsoft.Json.JsonConvert.SerializeObject(Original.Progress),realCoinsUnchanged=OriginalWallet.Balance(SqadSortResources.Coin)==OriginalCoins};
        Trace.Add(row);System.IO.Directory.CreateDirectory(Out);System.IO.File.WriteAllText(Out+"/runtime-trace.json",Newtonsoft.Json.JsonConvert.SerializeObject(Trace,Newtonsoft.Json.Formatting.Indented));return Newtonsoft.Json.JsonConvert.SerializeObject(row);
    }
    public static string SetScenario(string name)
    {
        Attach();
        if(name=="empty-undo")Memory.Progress.BoosterUndo=0;
        if(name=="poor")Wallet.Set(0);
        if(name=="rich")Wallet.Set(5000);
        if(name=="new-player")
        {
            Memory.Progress.CopyFrom(new ProgressModel());Memory.Progress.Lives=5;Memory.Progress.StartCoinsGranted=true;Wallet.Set(1000);
        }
        if(name=="empty-lives")Memory.Progress.Lives=0;
        if(name=="stock"){Memory.Progress.BoosterUndo=2;Memory.Progress.BoosterShuffle=2;Memory.Progress.BoosterAddContainer=2;}
        if(Current() is GameplayScreen game)game.CheatRefresh();
        return Snapshot(name);
    }
    public static string ColdHome()
    {
        var current=Current();var scenes=(ISceneService)current.GetType().GetField("_scenes",Flags).GetValue(current);
        scenes.LoadAsync(SceneKeys.Main,new MainParam(ColdBoot:true),SceneTransition.Replace).Forget();return "Cold Home route requested with memory-only progress";
    }
    public static string Invoke(string method)
    {
        Attach();var game=Current() as GameplayScreen;if(game==null)throw new Exception("Gameplay required");
        if(method=="lose")return "CheatLose="+game.CheatLose();
        if(method=="win")return "CheatWin="+game.CheatWin();
        if(method=="buy-undo") {var task=(UniTask)typeof(GameplayScreen).GetMethod("Buy",Flags).Invoke(game,new object[]{Booster.Undo});task.Forget();return "Actual Buy flow entered with memory-only wallet";}
        throw new Exception("Unknown audit method");
    }
    public sealed class AuditWallet:IWalletService
    {
        readonly Dictionary<ResourceKey,long> balances=new Dictionary<ResourceKey,long>();
        public void Set(long value)=>balances[SqadSortResources.Coin]=value;
        public long Balance(ResourceKey key)=>balances.TryGetValue(key,out var v)?v:0;
        public bool CanAfford(ResourceKey key,long amount)=>Balance(key)>=amount;
        public bool TrySpend(ResourceKey key,long amount,GrantSource source){if(!CanAfford(key,amount))return false;balances[key]=Balance(key)-amount;return true;}
        public void Grant(ResourceKey key,long amount,GrantSource source)=>balances[key]=Balance(key)+amount;
        public bool TryExchange(ResourceKey key,long cost,Action<IWalletGrant> grant,GrantSource source)=>throw new NotSupportedException("No real exchanges in flow audit");
    }
    public sealed class AuditFacts:IProgressFacts
    {
        public void FirstClear(int level){}
        public void Earned(string currency,long value,string placement){}
        public void Spent(string currency,long value,string placement){}
        public void FeatureUnlocked(string feature){}
    }
}
