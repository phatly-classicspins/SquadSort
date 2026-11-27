import json,html
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path('docs/art/popup-content-review-20261127')
checks=[];issues=[];names=[];icon_checks=[]
for path in sorted((root/'after').glob('*.png')):
 name=path.stem;names.append(name);assert Image.open(path).size==(1080,1920)
 d=json.loads((root/'metrics'/f'{name}.json').read_text())
 frames=[g for g in d['graphics'] if g['sprite'].startswith('popup-') and g['sprite']!='popup-topper']
 frame=max(frames,key=lambda g:(g['rect'][2]-g['rect'][0])*(g['rect'][3]-g['rect'][1]),default=None)
 headers=[g for g in d['graphics'] if g['sprite']=='popup-topper' or g['sprite'].startswith('header-') and g['path'].split('/')[-1] in ['BoosterHeader','TitleRibbon','Ribbon']]
 for t in d['texts']:
  if t['overflow']:issues.append({'page':name,'kind':'text-overflow','node':t['path']})
  leaf=t['path'].split('/')[-1];ink=t['ink']
  if leaf in ['Title','BoosterName'] and headers:
   h=headers[0];r=h['rect'];w=r[2]-r[0];hh=r[3]-r[1]
   face=[r[0]+w*50/515,r[1]+hh*26/236,r[0]+w*465/515,r[1]+hh*110/236] if h['sprite']=='popup-topper' else [r[0]+10,r[1]+8,r[2]-10,r[3]-8]
   dx=(ink[0]+ink[2]-face[0]-face[2])/2;dy=(ink[1]+ink[3]-face[1]-face[3])/2
   inside=all([ink[0]>=face[0]-1,ink[1]>=face[1]-1,ink[2]<=face[2]+1,ink[3]<=face[3]+1])
   checks.append({'page':name,'title':t['text'],'insidePurple':inside,'centerOffset':[round(dx,2),round(dy,2)]})
   if not inside or max(abs(dx),abs(dy))>1:issues.append({'page':name,'kind':'title-bounds-or-center','offset':[dx,dy]})
  if leaf in ['Description','Message'] and frame:
   r=frame['rect'];inside=ink[0]>=r[0]+8 and ink[2]<=r[2]-8 and ink[1]>=r[1]+8 and ink[3]<=r[3]-8
   if not inside:issues.append({'page':name,'kind':'body-outside-panel','node':t['path']})
 if frame:
  primary=[g for g in d['graphics'] if (g['path'].endswith('/BoosterIcon') and 'Button' not in g['path']) or (name in ['PurchaseFailed','RestartBooster','SkipLevel'] and g['path'].endswith('/Icon')) or (name=='OutOfLives' and g['path'].endswith('/HeartImage'))]
  for icon in primary:
   r=frame['rect'];b=icon['rect'];dx=(b[0]+b[2]-r[0]-r[2])/2
   icon_checks.append({'page':name,'node':icon['path'],'centerOffsetX':round(dx,2),'widthFraction':round((b[2]-b[0])/(r[2]-r[0]),3),'preserveAspect':icon['aspect']})
   if abs(dx)>1:issues.append({'page':name,'kind':'primary-icon-center','offset':dx})
 if name=='GarageGift':
  icon=next(g for g in d['graphics'] if g['path'].endswith('/FreeMoveIcon'));label=next(t for t in d['texts'] if t['path'].endswith('/FreeMoveAmount'));gap=icon['rect'][1]-label['ink'][3]
  icon_checks.append({'page':name,'rewardIconToCountGap':round(gap,2)})
  if gap<4:issues.append({'page':name,'kind':'reward-icon-count-clearance','gap':gap})
(root/'verification.json').write_text(json.dumps({'states':len(names),'titlesMeasured':len(checks),'checks':checks,'iconChecks':icon_checks,'issues':issues,'scope':'English preview data, 1080x1920 Game View. Geometry plus visual review; no device build.','skipped':['EditMode suite: no Editor-side production code changes','PlayMode suite: CI-owned, unverified','Keyed Slow gate: not configured','Device build and other aspect ratios/locales: not run']},indent=2),encoding='utf-8')
cards=[]
for i,name in enumerate(names,1):
 cards.append(f'<section><h2>{i:02}. {html.escape(name)}</h2><div class="pair"><a href="before/{name}.png" target="_blank"><small>TRƯỚC</small><img loading="lazy" src="before/{name}.png"></a><a href="after/{name}.png" target="_blank"><small>SAU</small><img loading="lazy" src="after/{name}.png"></a></div></section>')
page='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Popup text and icon review</title><style>body{background:#211a30;color:white;margin:24px;font:16px Arial}p{color:#d7cce2;line-height:1.5}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:22px}section{background:#30253e;padding:16px;border-radius:12px}h2{font-size:18px;margin:0 0 12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:10px}img{width:100%;display:block}a{color:#e4caff;text-decoration:none}small{display:block;padding:0 0 8px}</style><h1>Popup · chữ và icon</h1><p>28 trạng thái · trước / sau · bấm ảnh để xem bản 1080 × 1920.</p><p>Đã kiểm tra vùng chữ hiển thị, căn tâm title vào vùng tím, mô tả trong khung và tỷ lệ icon chính. Ảnh dùng dữ liệu xem trước tiếng Anh trong Unity; không thực hiện mua hàng.</p><p>Thông số kiểm tra: <a href="verification.json">verification.json</a>. Chưa kiểm tra các ngôn ngữ khác, tỷ lệ màn hình khác hoặc build thiết bị.</p><main>CARDS</main></html>'''.replace('CARDS',''.join(cards))
(root/'review.html').write_text(page,encoding='utf-8')
board=Image.new('RGB',(1200,1050),'#211a30');draw=ImageDraw.Draw(board);font=lambda n:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',n)
draw.text((22,16),'POPUP / TEXT & ICON REVIEW',fill='white',font=font(29))
examples=[('ElementUnlockHidden','Title: Hidden Buddy',(180,260,940,650)),('SettingsHome','Title: Settings',(100,260,990,650)),('PurchaseFailed','Icon: Purchase Failed',(120,680,960,1100)),('GarageGift','Icons: Limited Time Reward',(160,750,920,1200))]
for i,(name,label,box) in enumerate(examples):
 x=20+(i%2)*595;y=72+(i//2)*485
 draw.text((x,y),label,fill='white',font=font(23))
 for j,stage in enumerate(['before','after']):
  draw.text((x+j*285,y+38),'BEFORE' if j==0 else 'AFTER',fill='#cdb5e5',font=font(17))
  im=Image.open(root/stage/f'{name}.png').convert('RGB').crop(box);im.thumbnail((275,365),Image.Resampling.LANCZOS);board.paste(im,(x+j*285,y+68))
board.save(root/'overview.jpg',quality=95)
print(json.dumps({'states':len(names),'titles':len(checks),'issues':issues},ensure_ascii=False))
