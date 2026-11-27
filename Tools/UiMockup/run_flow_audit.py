"""Controlled, sequential UI-flow probes through the existing Unity-MCP bridge."""
import importlib.util,json,sys
from pathlib import Path

spec=importlib.util.spec_from_file_location('unity_call',Path('Tools/ArtIntegration/unity_call.py'))
bridge=importlib.util.module_from_spec(spec);spec.loader.exec_module(bridge)
root=Path('docs/art/ui-flow-review-20261127');root.mkdir(parents=True,exist_ok=True)
mode,*args=sys.argv[1:]
def quoted(s):return json.dumps(s,ensure_ascii=True)
if mode=='boot':
 code='var root=UnityEngine.Object.FindFirstObjectByType<ClassicSpins.PrototypeFramework.Composition.RootLifetimeScope>();var config=(ClassicSpins.PrototypeFramework.Presentation.FirstSceneConfig)root.Container.Resolve(typeof(ClassicSpins.PrototypeFramework.Presentation.FirstSceneConfig));var scenes=(ClassicSpins.PrototypeFramework.Presentation.ISceneService)root.Container.Resolve(typeof(ClassicSpins.PrototypeFramework.Presentation.ISceneService));Cysharp.Threading.Tasks.UniTaskExtensions.Forget(config.Load(scenes,System.Threading.CancellationToken.None));return "Actual FirstSceneConfig.Load invoked";'
 tool='script-execute';payload={'csharpCode':code,'timeout':45}
elif mode=='level':
 level=int(args[0]);assert level in (5,11,15)
 code='var assembly=System.AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.FullName==UnityEditor.SessionState.GetString("SquadSort.FlowAuditAssembly",""));var t=assembly.GetType("UiFlowAuditHarness");t.GetMethod("SetScenario").Invoke(null,new object[]{"new-player"});var memory=(Game.Application.InMemoryProgressStore)t.GetField("Memory").GetValue(null);memory.Progress.CurrentLevel='+str(level)+';memory.Progress.HighestCleared='+str(level-1)+';return t.GetMethod("ColdHome").Invoke(null,null);'
 tool='script-execute';payload={'csharpCode':code,'timeout':45}
elif mode=='call':
 method,*values=args
 code='var assembly=System.AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.FullName==UnityEditor.SessionState.GetString("SquadSort.FlowAuditAssembly",""));var t=assembly.GetType("UiFlowAuditHarness");return t.GetMethod('+quoted(method)+').Invoke(null,new object[]{'+','.join(quoted(v) for v in values)+'});'
 tool='script-execute';payload={'csharpCode':code,'timeout':45}
elif mode=='button':
 view,name=args
 code='var views=UnityEngine.Object.FindObjectsByType<Game.Views.UIBase>(UnityEngine.FindObjectsSortMode.None).Where(v=>'+('true' if view=='*' else 'v.GetType().Name=='+quoted(view))+');var matches=views.SelectMany(v=>v.GetComponentsInChildren<UnityEngine.UI.Button>()).Where(b=>b.gameObject.activeInHierarchy&&b.name=='+quoted(name)+').Distinct().ToArray();if(matches.Length!=1)throw new System.Exception("Expected one active '+name+' but found "+matches.Length);if(!matches[0].interactable)throw new System.Exception("Button disabled");matches[0].onClick.Invoke();return "Clicked '+view+'/'+name+'";'
 tool='script-execute';payload={'csharpCode':code,'timeout':45}
elif mode=='inspect':
 tool='script-execute';payload={'csharpCode':'return Newtonsoft.Json.JsonConvert.SerializeObject(UnityEngine.Object.FindObjectsByType<Game.Views.UIBase>(UnityEngine.FindObjectsSortMode.None).Select(v=>new{type=v.GetType().Name,buttons=v.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.gameObject.activeInHierarchy).Select(b=>new{name=b.name,enabled=b.interactable,text=string.Join(" ",b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text))})}));'}
elif mode=='shot':tool='screenshot-game-view';payload={'savePath':str(root/(args[0]+'.png')).replace('\\','/')}
else:raise SystemExit('Unknown audit mode')
result=bridge.call(tool,payload)
with (root/'tool-trace.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps({'mode':mode,'args':args,'result':result},ensure_ascii=False)+'\n')
contents=result.get('result',{}).get('content',[])
for c in contents:
 if c.get('type')=='text':print(c['text'][:5500])
if result.get('error') or result.get('result',{}).get('isError') or any('Tool execution failed' in c.get('text','') for c in contents):raise SystemExit(1)


