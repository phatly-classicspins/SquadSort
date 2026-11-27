"""Package corrected native 3D buttons and a same-size original/new comparison."""
from pathlib import Path
import hashlib
import json
import runpy
import zipfile
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/art/ui-native-gloss-20261125'
manifest=json.loads((OUT/'asset-manifest.json').read_text())
rendered=[]
for key,asset in manifest['assets'].items():
    path=OUT/'assets'/asset['png']
    assert Image.open(path).size==tuple(asset['png_pixels'])
    asset['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    if key.startswith(('lime_','purple_')):
        source=OUT/'button-sources'/(key+'.blend')
        assert source.exists()
        if asset.get('svg'):
            stale=OUT/'assets'/asset.pop('svg')
            if stale.exists():stale.unlink()
        asset['blend']=source.relative_to(OUT).as_posix()
        asset['render_method']='3D curved enamel face, raised rim and native orthographic camera; rendered directly to the final PNG pixel dimensions at 100% resolution'
        rendered.append(key)
assert len(rendered)==28
manifest['method']='109 size-specific sprites. 28 action-button variants use actual Blender 3D surface geometry rendered directly at native resolution; other controls retain their native vector geometry. No bitmap stretching or nine-slice. All original layout rectangles remain unchanged.'
manifest['style_reference']=['Assets/SqadSort/Content/UI/Home/level_normal_button2 1.png','Assets/SqadSort/Content/UI/Settings/BlueButton.png']
(OUT/'asset-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')

source=(ROOT/'Tools/UiMockup/package_native_review.py').read_text().replace('ui-native-20261125','ui-native-gloss-20261125')
source=source.replace('UI native — asset đúng size','UI native — nút 3D bóng theo asset gốc')
source=source.replace('Asset được vẽ riêng theo đúng kích thước local của từng Image: bo góc và viền có số đo cố định, không kéo ảnh mẫu. Đặt trực tiếp vào bản sao prefab ở PPU 1 để kiểm tra.','28 cỡ nút hành động đã được dựng lại bằng 3D: mặt phồng, viền nổi và highlight trắng; nút chính có khung vàng, nẹp hai bên và họa tiết caro theo asset gốc. PNG được render trực tiếp ở đúng kích thước từng nút, không kéo giãn.')
source=source.replace('109 asset native · 720 × 1280 · Không kéo giãn','28 nút 3D · Asset đúng size · Giữ nguyên layout')
source=source.replace('Size-specific native vector/PNG assets assigned to temporary Unity prefab clones at PPU 1 and exact local Sprite.rect. No bitmap resampling, no nine-slice, no transform edits. Layout and text compare equal.','Size-specific assets, including 28 native 3D-rendered buttons, assigned to temporary prefab clones at PPU 1. No resampling, nine-slice or transform edits. Layout and text compare equal.')
source=source.replace('SourceArt','SourceArt')
exec(compile(source,str(ROOT/'Tools/UiMockup/package_native_review.py'),'exec'),{'__file__':str(ROOT/'Tools/UiMockup/package_gloss_review.py'),'__name__':'__main__'})

# Both columns retain the exact source PNG pixels. This proof sheet is not used as an asset.
pairs=[('Nút chính',ROOT/'Assets/SqadSort/Content/UI/Home/level_normal_button2 1.png',OUT/'assets/lime_432.000x180.000.png'),('Nút phụ',ROOT/'Assets/SqadSort/Content/UI/Settings/BlueButton.png',OUT/'assets/purple_378.000x110.000.png')]
sheet=Image.new('RGB',(936,480),(39,32,48));draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',22)
draw.text((20,15),'ASSET GỐC',font=font,fill='white');draw.text((488,15),'BẢN MỚI — ĐÚNG SIZE',font=font,fill='white')
for index,(label,old,new) in enumerate(pairs):
    y=70 if index==0 else 315
    a,b=Image.open(old).convert('RGBA'),Image.open(new).convert('RGBA')
    assert a.size==b.size
    sheet.paste(a,(20,y),a);sheet.paste(b,(488,y),b)
sheet.save(OUT/'button-comparison.png')
readme=(OUT/'README.md').read_text()
readme=readme.replace('Each replacement is freshly authored vector geometry at the exact Image local width and height. PNG is rendered directly at ceil(local size), never resized.','Each replacement is authored at the exact Image local width and height. Twenty-eight button variants use real 3D geometry; other controls use native vector geometry. PNGs are rendered directly at ceil(local size), never resized.')
readme+='''

## Gloss correction after review

The previous buttons were too flat. Twenty-eight native button variants are now rendered from actual rounded 3D enamel geometry, using the original green/gold level button and glossy blue secondary button as finish references. Large level buttons have gold frames, side caps and a diamond pattern. Each .blend source has an orthographic camera at one world unit per pixel and renders at its exact final PNG resolution. No PNG is resized. The remaining native-size controls are unchanged.

Use the delivered PNG, not the .blend, in the game. The .blend is editable authoring source only. SVG files for the replaced buttons have been removed to avoid shipping stale flat sources. No game prefab is modified. Shader/lighting appearance is reviewed in the captured mockups, not claimed as an exact replica of the original hand-painted bitmap.
'''
(OUT/'README.md').write_text(readme,encoding='utf-8')
import_notes=(OUT/'IMPORT.md').read_text().replace('109 standalone size-specific PNG sprites and their SVG vector source.', '109 standalone size-specific PNG sprites. Twenty-eight button variants have editable .blend sources; other controls have SVG vector sources.')
(OUT/'IMPORT.md').write_text(import_notes,encoding='utf-8')
archive=OUT.parent/'ui-native-gloss-assets-20261125.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for folder in ['assets','button-sources']:
        for path in sorted((OUT/folder).iterdir()):
            if path.is_file() and path.suffix!='.blend1':z.write(path,path.relative_to(OUT))
    for name in ['asset-manifest.json','asset-map.csv','IMPORT.md','validation.json','button-comparison.png','README.md']:z.write(OUT/name,name)
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print(json.dumps({'buttons_rendered':len(rendered),'assets_total':len(manifest['assets']),'zip_mb':round(archive.stat().st_size/1048576,2)}))
