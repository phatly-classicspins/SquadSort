import importlib.util,json,sys,time,os
from pathlib import Path
spec=importlib.util.spec_from_file_location('bridge',Path('Tools/ArtIntegration/unity_call.py'));bridge=importlib.util.module_from_spec(spec);spec.loader.exec_module(bridge)
out=Path(os.environ.get('UI_REVIEW_OUT','docs/art/ui-review-20261127'));out.mkdir(parents=True,exist_ok=True)
def call(tool,args):
 r=bridge.call(tool,args)
 texts='\n'.join(c.get('text','') for c in r.get('result',{}).get('content',[]) if c.get('type')=='text')
 if r.get('error') or r.get('result',{}).get('isError') or 'Tool execution failed' in texts:raise RuntimeError(texts or r)
 return texts
if sys.argv[1]=='init':print(call('script-execute',{'csharpCode':Path('Tools/UiMockup/UiReviewCapture.cs').read_text(),'className':'UiReviewCapture','methodName':'Main','timeout':60}))
elif sys.argv[1]=='current':print(call('screenshot-game-view',{'savePath':str(out/(sys.argv[2]+'.png')).replace('\\','/')}))
elif sys.argv[1]=='shots':
 for name in sys.argv[2:]:
  code='var a=System.AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.FullName==UnityEditor.SessionState.GetString("SquadSort.UiReviewAssembly",""));return a.GetType("UiReviewCapture").GetMethod("Show").Invoke(null,new object[]{'+json.dumps(name)+'});'
  print(call('script-execute',{'csharpCode':code,'timeout':45}),flush=True);time.sleep(1.3)
  if os.environ.get('UI_REVIEW_AUDIT')=='1':
   audit=code.replace('GetMethod("Show")','GetMethod("Audit")')
   print(call('script-execute',{'csharpCode':audit,'timeout':45}),flush=True)
  target=out/(name+'.png')
  if target.exists():target.unlink()
  print(call('screenshot-game-view',{'savePath':str(target).replace('\\','/')}),flush=True)
elif sys.argv[1]=='clear':
 print(call('script-execute',{'csharpCode':'var a=System.AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.FullName==UnityEditor.SessionState.GetString("SquadSort.UiReviewAssembly",""));return a.GetType("UiReviewCapture").GetMethod("Clear").Invoke(null,null);'}))
