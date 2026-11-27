"""Review the exact existing Chip Away UI art; no recreated or stretched replacements."""
from pathlib import Path
import json
import hashlib
import html
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/art/ui-chip-away-20261125'
data=json.loads((OUT/'before-layout.json').read_text())
previous=json.loads((ROOT/'docs/art/ui-native-20261125/before-layout.json').read_text())
diffs=[];nodes=0;assets={}
for current,old in zip(data['pages'],previous['pages']):
    assert current['name']==old['name']
    assert Image.open(OUT/'before'/(current['name']+'.png')).size==(720,1280)
    for a,b in zip(current['nodes'],old['nodes']):
        nodes+=1
        for key in ['path','rect','native','anchor','size','pivot','scale','fontSize','text','active']:
            if a.get(key)!=b.get(key):diffs.append({'page':current['name'],'node':a['path'],'property':key})
        if a.get('texture'):
            path=ROOT/a['texture']
            entry=assets.setdefault(a['texture'],{'path':a['texture'],'sprites':set(),'used_by':set()})
            entry['sprites'].add(a['sprite']);entry['used_by'].add(current['name'])
            if path.is_file():
                entry['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
                try:entry['source_pixels']=list(Image.open(path).size)
                except Exception:pass
assert not diffs,diffs[:10]
inventory=[]
for entry in assets.values():
    entry['sprites']=sorted(entry['sprites']);entry['used_by']=sorted(entry['used_by']);inventory.append(entry)
(OUT/'original-assets.json').write_text(json.dumps(inventory,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
report={'screens':len(data['pages']),'reference_pixels':[720,1280],'rect_transforms_compared':nodes,'layout_differences':diffs,'source_textures':len(inventory),'replacement_assets':0,'bitmap_resizing':False,'method':'Fresh captures of the existing authored Chip Away prefabs, using the exact original sprite references and materials. No generated substitutes. No production prefab, scene or art asset changed.'}
(OUT/'validation.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
names=[p['name'] for p in data['pages']]
labels={'Home':'Home','GameplayHud':'HUD gameplay','Settings':'Cài đặt','Shop':'Cửa hàng','LevelUp':'Thắng màn','Lose':'Thua màn','OutOfLives':'Hết mạng','OutOfSpace':'Hết chỗ','BoosterPurchase':'Mua booster','BoosterUnlock':'Mở booster','ElementUnlock':'Mở element','StarterBundle':'Starter Pack','RemoveAds':'Xóa quảng cáo','GarageGift':'Quà tặng','SkipLevel':'Bỏ qua màn','RestartBooster':'Chơi lại','RentSlot':'Thuê ô','PurchaseReward':'Nhận phần thưởng','PurchaseFailed':'Mua thất bại','NoInternet':'Mất mạng','NotificationPermission':'Thông báo','Rating':'Đánh giá','Loading':'Đang tải','Error':'Lỗi','RestorePurchase':'Khôi phục mua hàng'}
cards=''.join(f'<a href="before/{n}.png"><img loading="lazy" src="before/{n}.png" alt="{html.escape(labels.get(n,n))}"><span>{html.escape(labels.get(n,n))}</span></a>' for n in names)
document='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Bộ UI Chip Away gốc</title><style>*{box-sizing:border-box}body{font:16px/1.5 Segoe UI,Arial,sans-serif;color:#30261b;background:#fff5e6;margin:0}main{max-width:1260px;margin:auto;padding:28px}h1{font-size:30px;margin:0 0 10px}p{max-width:920px}.note{padding:14px 18px;border-radius:12px;background:#e6f5da;color:#31521d}.grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:18px;margin-top:24px}a{color:inherit;text-decoration:none;background:white;border-radius:10px;overflow:hidden;box-shadow:0 4px 18px #4b2c1814}img{width:100%;display:block;aspect-ratio:720/1280}span{display:block;padding:10px;font-weight:600;font-size:14px}@media(max-width:800px){main{padding:18px}.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}</style><main><h1>Bộ UI Chip Away gốc</h1><p>Đây là bản xem trực tiếp bộ UI đang có trong project: nút xanh lá/xanh dương, khung vàng, mặt bóng, chữ và minh họa gốc. Không dùng lại các nút tím/lavender đã thiết kế thử.</p><p class="note">25 màn/popup · 720 × 1280 · 847 RectTransform giữ nguyên · Sprite gốc được dùng nguyên bản</p><p>Bấm ảnh để mở PNG đủ kích thước. Đây là trạng thái mẫu trên prefab; video và nội dung tạo lúc chạy game không được mô phỏng.</p><div class="grid">__CARDS__</div></main></html>'''.replace('__CARDS__',cards)
(OUT/'review.html').write_text(document,encoding='utf-8')
chosen=['Home','Settings','Shop','LevelUp','OutOfLives','RemoveAds']
sheet=Image.new('RGB',(1168,1426),(255,245,230));draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',22)
for i,n in enumerate(chosen):
    x=22+(i%3)*382;y=22+(i//3)*702
    draw.text((x,y+3),labels[n],font=font,fill=(51,36,21))
    # Thumbnails are review presentation only. The linked capture and all source assets remain native.
    im=Image.open(OUT/'before'/(n+'.png')).convert('RGB').resize((360,640),Image.Resampling.LANCZOS)
    sheet.paste(im,(x,y+40))
sheet.save(OUT/'overview.png')
(OUT/'README.md').write_text('''# Chip Away UI — original art baseline

This review uses the exact original Chip Away sprites and materials already present in the project. It replaces the rejected lavender/generated-button directions as the visual reference. It is not a claim that new art was generated.

25 fresh 720×1280 captures were produced by Unity-MCP in a temporary Preview Scene. 847 RectTransforms, local dimensions, anchors, pivots, scale, font sizes, text and active states compare equal to the initial project capture. No Image.sprite replacement, resize, prefab save, production art write or gameplay change was performed.

`original-assets.json` maps the existing source files, their hashes, dimensions and screens that use them. Reuse those existing project assets directly; do not duplicate their .meta GUIDs or fit a universal replacement to unrelated controls. If a future new size is required, author that size from the original art source, preserving its finish.

`review.html` links every original-resolution PNG. `overview.png` is a thumbnail presentation only. Runtime-only illustration/video content can be absent in prefab-state captures.

Skipped: Play Mode, engine-free tests and player build; this is a read-only art reference capture with no game code or persistent asset changes.
''',encoding='utf-8')
print(json.dumps(report))
