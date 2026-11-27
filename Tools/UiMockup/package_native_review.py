"""Validate the unchanged UI layout and package the preview-only reskin review."""
from pathlib import Path
import json
import html
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/art/ui-native-20261125'
before = json.loads((OUT/'before-layout.json').read_text())
after = json.loads((OUT/'after-layout.json').read_text())
assert len(before['pages']) == len(after['pages']) == 25
diffs=[]; count=0; replacements=0; unique=set()
for old,new in zip(before['pages'],after['pages']):
    assert old['name']==new['name'] and len(old['nodes'])==len(new['nodes'])
    for a,b in zip(old['nodes'],new['nodes']):
        count+=1
        for key in ['path','rect','anchor','size','pivot','scale','fontSize','text','active']:
            if a[key]!=b[key]:diffs.append({'page':old['name'],'node':a['path'],'property':key,'before':a[key],'after':b[key]})
        if b.get('sprite') and b.get('role')!='keep':
            replacements+=1;unique.add(b['sprite'])
    for mode in ['before','after']:
        assert Image.open(OUT/mode/(old['name']+'.png')).size==(720,1280)
assert not diffs, diffs[:8]
report={'reference_pixels':[720,1280],'pages':25,'nodes_compared':count,'layout_differences':diffs,'sprite_instances_mapped':replacements,'unique_source_sprites_mapped':len(unique),'method':'Size-specific native vector/PNG assets assigned to temporary Unity prefab clones at PPU 1 and exact local Sprite.rect. No bitmap resampling, no nine-slice, no transform edits. Layout and text compare equal.','scope':'Mockup only; original logo, illustrations, backgrounds and runtime-only video/3D content are retained or unavailable in the authored prefab preview.'}
(OUT/'validation.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')

names=[p['name'] for p in before['pages']]
labels={'Home':'Home','GameplayHud':'HUD gameplay','Settings':'Cài đặt','Shop':'Cửa hàng','LevelUp':'Thắng màn','Lose':'Thua màn','OutOfLives':'Hết mạng','OutOfSpace':'Hết chỗ','BoosterPurchase':'Mua booster','BoosterUnlock':'Mở booster','ElementUnlock':'Mở element','StarterBundle':'Starter Pack','RemoveAds':'Xóa quảng cáo','GarageGift':'Quà tặng','SkipLevel':'Bỏ qua màn','RestartBooster':'Chơi lại','RentSlot':'Thuê ô','PurchaseReward':'Nhận phần thưởng','PurchaseFailed':'Mua thất bại','NoInternet':'Mất mạng','NotificationPermission':'Thông báo','Rating':'Đánh giá','Loading':'Đang tải','Error':'Lỗi','RestorePurchase':'Khôi phục mua hàng'}
options=''.join(f'<option value="{n}"'+(' selected' if n=='Settings' else '')+'>'+html.escape(labels.get(n,n))+'</option>' for n in names)
cards=''.join(f'<a class="card" href="after/{n}.png"><img loading="lazy" src="after/{n}.png" alt="{html.escape(labels.get(n,n))}"><span>{html.escape(labels.get(n,n))}</span></a>' for n in names)
page='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Mockup UI — giữ nguyên layout</title>
<style>*{box-sizing:border-box}body{margin:0;background:#f1edf7;color:#211b36;font:16px/1.5 Segoe UI,Arial,sans-serif}header,main{max-width:1220px;margin:auto;padding:28px}h1{font-size:29px;margin:0 0 12px}p{max-width:900px}.tag{display:inline-block;padding:7px 12px;background:#e2f5da;border-radius:30px;color:#2a5723;font-weight:600}.toolbar{display:flex;flex-wrap:wrap;gap:12px;align-items:center;margin:20px 0}select,button{font:inherit;padding:9px 15px;border:1px solid #c7bcda;border-radius:10px;background:white;color:inherit}button{cursor:pointer}button.active{background:#6d35ad;color:white}.stage{width:min(100%,720px);position:relative;aspect-ratio:720/1280;margin:auto;background:#111;overflow:hidden;box-shadow:0 12px 38px #30214228}.stage>img{position:absolute;inset:0;width:100%;height:100%;object-fit:contain}.stage .front{clip-path:inset(0 0 0 var(--split,50%))}.line{position:absolute;inset:0 auto 0 var(--split,50%);width:2px;background:#b3ff42;pointer-events:none}.slider{display:block;width:min(100%,720px);margin:18px auto}.captions{display:flex;justify-content:space-between;max-width:720px;margin:10px auto}.grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:18px}.card{display:block;text-decoration:none;color:inherit;background:white;border-radius:12px;overflow:hidden;box-shadow:0 3px 12px #30214212}.card img{display:block;width:100%;aspect-ratio:720/1280}.card span{display:block;padding:10px;font-size:14px}.note{font-size:14px;color:#675979}@media(max-width:800px){header,main{padding:18px}.grid{grid-template-columns:repeat(2,minmax(0,1fr))}} </style>
<header><h1>UI native — asset đúng size</h1><p>Asset được vẽ riêng theo đúng kích thước local của từng Image: bo góc và viền có số đo cố định, không kéo ảnh mẫu. Đặt trực tiếp vào bản sao prefab ở PPU 1 để kiểm tra.</p><span class="tag">109 asset native · 720 × 1280 · Không kéo giãn</span><p class="note">Giữ vị trí, kích thước, anchor, pivot, scale, cỡ chữ và nội dung. Logo, nền và minh họa gốc được giữ. Giá trị chữ là dữ liệu mẫu có sẵn trên prefab; video và nội dung runtime không được mô phỏng.</p></header>
<main><h2>Đối chiếu cùng tọa độ</h2><div class="toolbar"><select id="screen">__OPTIONS__</select><button data-mode="before">UI cũ</button><button data-mode="compare" class="active">Kéo so sánh</button><button data-mode="after">Mockup mới</button></div><div class="captions"><strong>Cũ</strong><strong>Mới</strong></div><div class="stage" id="stage"><img id="old" src="before/Settings.png" alt="UI cũ"><img id="new" class="front" src="after/Settings.png" alt="Mockup mới"><div class="line" id="line"></div></div><input class="slider" id="split" type="range" min="0" max="100" value="50" aria-label="Vị trí ranh giới so sánh"><p class="note">Ảnh PNG gốc của mỗi màn là 720 × 1280. Trang này chỉ thu phóng ảnh để vừa cửa sổ; có thể bấm từng ảnh bên dưới để xem kích thước gốc.</p><h2>Toàn bộ bộ UI</h2><div class="grid">__CARDS__</div></main>
<script>const stage=document.getElementById('stage'),slider=document.getElementById('split'),sel=document.getElementById('screen'),old=document.getElementById('old'),next=document.getElementById('new'),line=document.getElementById('line');let mode='compare';function paint(){stage.style.setProperty('--split',(mode==='before'?100:mode==='after'?0:slider.value)+'%');line.style.display=mode==='compare'?'block':'none';slider.style.display=mode==='compare'?'block':'none'}slider.oninput=paint;sel.onchange=()=>{old.src='before/'+sel.value+'.png';next.src='after/'+sel.value+'.png'};document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>{mode=b.dataset.mode;document.querySelectorAll('[data-mode]').forEach(x=>x.classList.toggle('active',x===b));paint()});paint();</script></html>'''.replace('__OPTIONS__',options).replace('__CARDS__',cards)
(OUT/'review.html').write_text(page,encoding='utf-8')

# A contact sheet is a presentation of the untouched Unity captures, not a regenerated mockup.
chosen=['Home','Settings','Shop','LevelUp','OutOfLives','RemoveAds']
width,height=360,640;pad=22;title=40
sheet=Image.new('RGB',(3*width+4*pad,2*(height+title)+3*pad),(239,234,248))
draw=ImageDraw.Draw(sheet)
try:font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',22)
except OSError:font=ImageFont.load_default()
for i,n in enumerate(chosen):
    x=pad+(i%3)*(width+pad);y=pad+(i//3)*(height+title+pad)
    draw.text((x,y+3),labels[n],font=font,fill=(42,29,66))
    image=Image.open(OUT/'after'/(n+'.png')).convert('RGB').resize((width,height),Image.Resampling.LANCZOS)
    sheet.paste(image,(x,y+title))
sheet.save(OUT/'overview.png')
(OUT/'README.md').write_text(f'''# UI asset-only mockup — 2026-11-25

Open `review.html` to compare all 25 screens/popups. Each source capture is 720×1280, matching the currently loaded UiHost and PopupHost, scale 1 / PPU 1.

{count} RectTransforms compared equal across both sets: world screen rectangles, anchors, sizeDelta, pivot, localScale, fontSize, text and active state. {replacements} sprite instances ({len(unique)} unique source sprites) are mapped to the proposed skin. `validation.json` and both layout snapshots carry the evidence.

Each replacement is freshly authored vector geometry at the exact Image local width and height. PNG is rendered directly at ceil(local size), never resized. The exact fractional local extent is retained in Sprite.rect and SVG viewBox. PPU 1, border 0, no nine-slice. The original RectTransform, copy and font sizes remain unchanged. Nothing under Assets is saved.

Proposed direction: soft enamel, pearl/lavender panels, violet secondary controls and lime primary controls. Existing branding, background illustrations, reward/gameplay artwork, fonts and labels remain. Each PNG is a standalone fixed-size sprite, not an atlas crop or a stretched universal button. Runtime-only video/3D illustrations can appear empty in authored-prefab previews. The controls are static illustrative prefab states rather than a play-through.

Asset source: fresh code-native SVG geometry / analytic native PNG rendering in `Tools/UiMockup/build_native_assets.py`; no AI atlas is sampled. Capture source: `Tools/UiMockup/CaptureNativePreview.cs`. Asset dimensions and instance mappings are in `asset-manifest.json`.

Skipped: Play Mode, no runtime or gameplay changes requested.
Skipped: Unity tests/build; this is a preview-only art/layout inspection with no project code or asset integration.
''',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
