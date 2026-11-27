from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import html
root=Path('docs/art/popup-header-alignment-20261127')
names=['SettingsHome','SettingsGameplay','BoosterPurchase','BoosterUnlockUndo','ElementUnlockHidden','Lose','OutOfLives','OutOfSpace','Rating','Error','NoInternet','NotificationPermission','RentSlot','SkipLevel','RestartBooster','StarterBundle','PurchaseFailed']
for name in names:
 assert Image.open(root/'after'/f'{name}.png').size==(1080,1920)
cards=''.join(f'<a href="after/{n}.png" target="_blank"><img src="after/{n}.png" loading="lazy"><b>{i:02}. {html.escape(n)}</b></a>' for i,n in enumerate(names,1))
page='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Popup title alignment</title><style>body{background:#211a30;color:#fff;font:16px Arial;margin:25px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:22px}a{color:#fff;text-decoration:none;background:#352945;border-radius:12px;padding:10px}img{width:100%;display:block}b{display:block;margin:12px 0}p{line-height:1.5;color:#d9ccdf}</style><h1>Popup · dải title tím đã căn lại</h1><p>Tâm phần tím nằm trên tâm viền trên của khung; căn ngang theo khung. Chữ tiêu đề và nút đóng đi cùng cụm. Bấm ảnh để xem bản 1080 × 1920.</p><p>Ảnh chụp trực tiếp trong Unity với dữ liệu xem trước. Không thực hiện giao dịch hoặc thay đổi tiến trình thật.</p><main>CARDS</main></html>'''.replace('CARDS',cards)
(root/'review.html').write_text(page,encoding='utf-8')
sample=['SettingsHome','BoosterUnlockUndo','Lose','SkipLevel']
im=Image.new('RGB',(1000,980),'#211a30');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24)
d.text((20,14),'POPUP TITLE / ALIGNED TO TOP RIM',fill='white',font=font)
for i,n in enumerate(sample):
 # Focus on the title and the upper rim; full screenshots remain linked in the gallery.
 src=Image.open(root/'after'/f'{n}.png').convert('RGB')
 # Each popup has a different authored height, so keep its own title crop.
 box={'SettingsHome':(45,220,1035,750),'BoosterUnlockUndo':(90,180,990,660),'Lose':(70,220,1010,750),'SkipLevel':(80,190,1000,710)}[n]
 src=src.crop(box);src.thumbnail((470,375),Image.Resampling.LANCZOS)
 x=20+i%2*495;y=70+i//2*445
 im.paste(src,(x+(470-src.width)//2,y));d.text((x,y+385),n,fill='white',font=font)
im.save(root/'overview.jpg',quality=95)
(root/'validation.txt').write_text('16 prefab assets edited via Unity-MCP: header, title and close positions aligned.\nStarterBundle/PurchaseFailed reuse shared body/topper sprites and a single close glyph.\n17 preview states captured at 1080x1920.\nEngine-free gate: 482 passed, 0 failed.\nEditMode suite: skipped (no Editor-side production code change).\nPlayMode suite: CI-owned, unverified.\nKeyed Slow leg: skipped (not configured).\nDevice build: not run.\n',encoding='utf-8')
print('17 images verified; review.html and overview.jpg ready')
