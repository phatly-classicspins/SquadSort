from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import html,json,shutil,zipfile
out=Path('docs/art/ui-review-20261127')
items=[('Home','Home'),('Shop','Shop'),('Gameplay','Gameplay'),('Loading','Loading'),('SettingsHome','Settings / Home'),('SettingsGameplay','Settings / Gameplay'),('Win','Win'),('Lose','Confirm Restart'),('ConfirmHome','Confirm Home'),('OutOfSpace','Play On'),('OutOfLives','Out of Lives'),('BoosterPurchaseActual','Buy Booster'),('BoosterUnlockUndo','Unlock Undo'),('BoosterUnlockShuffle','Unlock Shuffle'),('BoosterUnlockRemove','Unlock Remove'),('ElementUnlockHidden','Hidden Buddies'),('ElementUnlockHouse','House'),('StarterBundle','Starter Pack'),('RemoveAds','Remove Ads'),('GarageGift','Limited Time Reward'),('SkipLevel','Skip Level'),('RestartBooster','Restart Booster'),('RentSlot','Extra Slots'),('PurchaseReward','Purchase Success'),('PurchaseFailed','Purchase Failed'),('RestorePurchase','Restore Purchases'),('Rating','Rating'),('RatingThanks','Rating / Thank You'),('NoInternet','No Internet'),('NotificationPermission','Notifications'),('Error','Ad Error')]
for source,name,title in [('first-player-direct-gameplay','Tutorial','Tutorial / Level 1'),('shuffle-guide-after-tap','ShuffleGuide','Tutorial / Shuffle'),('remove-guide','RemoveGuide','Tutorial / Remove')]:
 p=Path('docs/art/ui-flow-review-20261127')/(source+'.png')
 if p.exists():shutil.copy2(p,out/(name+'.png'));items.append((name,title))
live={'OutOfSpace','Home','Shop','Gameplay','Win','BoosterPurchaseActual','Tutorial','ShuffleGuide','RemoveGuide'}
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',n)
for group in range((len(items)+9)//10):
 batch=items[group*10:(group+1)*10];canvas=Image.new('RGB',(1280,1080),'#181524');d=ImageDraw.Draw(canvas)
 d.text((24,15),f'SQUAD SORT  /  UI REVIEW  /  {group+1}',fill='white',font=font(27))
 d.text((24,53),'1080 x 1920 originals in review.html | PREVIEW = prefab with sample data',fill='#c7bfd7',font=font(18))
 for i,(name,title) in enumerate(batch):
  x=24+(i%5)*250;y=98+(i//5)*488
  im=Image.open(out/(name+'.png')).convert('RGB');assert im.size==(1080,1920),(name,im.size)
  canvas.paste(im.resize((230,409),Image.Resampling.LANCZOS),(x,y))
  d.text((x,y+417),f'{group*10+i+1:02}  {title}',fill='white',font=font(18))
  d.text((x,y+442),'FLOW CAPTURE' if name in live else 'PREFAB PREVIEW',fill='#b0e2bf' if name in live else '#e9bf84',font=font(14))
 canvas.save(out/f'overview-{group+1}.jpg',quality=94)
cards=[]
for i,(name,title) in enumerate(items,1):
 mode='Flow capture' if name in live else 'Prefab preview · sample data'
 cards.append(f'<button class="card" onclick="show({i-1})"><img loading="lazy" src="{name}.png"><b>{i:02} · {html.escape(title)}</b><small>{mode}</small></button>')
data=json.dumps([{'src':n+'.png','title':f'{i:02} · {t}'} for i,(n,t) in enumerate(items,1)])
page='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Squad Sort — UI review</title><style>body{margin:0;background:#181524;color:#fff;font:16px Arial}header{padding:28px;position:sticky;top:0;background:#181524ed;z-index:1}h1{margin:0 0 10px;font-size:26px}p{color:#c9c0d8;line-height:1.5;margin:5px 0}main{padding:0 24px 30px;display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:22px}.card{background:#292337;color:white;border:1px solid #4b3f60;border-radius:12px;padding:9px;cursor:zoom-in;text-align:left}.card img{width:100%;display:block;border-radius:6px}.card b,.card small{display:block;margin:10px 4px}.card small{color:#c9bdd4}dialog{background:#181524;border:1px solid #71617d;color:white;padding:12px;max-width:95vw;max-height:95vh}dialog::backdrop{background:#000d}.bar{display:flex;justify-content:space-between;gap:16px;align-items:center;padding:6px}dialog img{display:block;max-width:90vw;max-height:82vh;margin:auto}a{color:#d1baff}.bar button{padding:8px 14px;cursor:pointer}</style><header><h1>Squad Sort · UI review</h1><p>NUM ảnh · ảnh gốc 1080 × 1920 · bấm ảnh để phóng lớn; dùng ← → để đổi ảnh.</p><p>Flow capture: chụp từ flow chạy trong Editor. Prefab preview: prefab hiện tại được mở riêng với dữ liệu mẫu để đánh giá hình thức; không phải bằng chứng đã chạy flow.</p><p>Ảnh có thể có nhãn CHEAT màu đỏ của bản phát triển. Ảnh Play On và nhóm Tutorial lấy từ phiên kiểm tra ngay trước đó. Không thực hiện giao dịch thật.</p></header><main>CARDS</main><dialog id="viewer"><div class="bar"><button onclick="step(-1)">←</button><b id="caption"></b><a id="original" target="_blank">Ảnh gốc</a><button onclick="step(1)">→</button><button onclick="viewer.close()">Đóng ×</button></div><img id="large"></dialog><script>const items=DATA;let index=0;function show(i){index=(i+items.length)%items.length;large.src=items[index].src;caption.textContent=items[index].title;original.href=large.src;if(!viewer.open)viewer.showModal()}function step(d){show(index+d)}document.addEventListener('keydown',e=>{if(!viewer.open)return;if(e.key==='ArrowRight')step(1);if(e.key==='ArrowLeft')step(-1)});</script></html>'''.replace('NUM',str(len(items))).replace('CARDS',''.join(cards)).replace('DATA',data)
(out/'review.html').write_text(page,encoding='utf-8')
(out/'index.json').write_text(json.dumps([{'number':i,'file':n+'.png','title':t,'mode':'flow' if n in live else 'prefab-preview'} for i,(n,t) in enumerate(items,1)],indent=2),encoding='utf-8')
with zipfile.ZipFile(out.parent/'ui-review-20261127.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in out.iterdir():
  if p.is_file():z.write(p,p.name)
print(f'{len(items)} states; {(len(items)+9)//10} overview sheets; gallery and ZIP ready')

