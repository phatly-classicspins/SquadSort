from pathlib import Path
from PIL import Image, ImageDraw

root = Path('docs/art/ui-squad-theme-20261126/alignment')
for name in ('Home', 'Shop', 'Settings'):
    ref = Image.open(root / f'reference-{name}.png').convert('RGB').resize((720,1280))
    actual = Image.open(root / 'runtime' / ('Shop-Final.png' if name == 'Shop' else f'{name}.png')).convert('RGB').resize((720,1280))
    overlay = Image.blend(ref, actual, .5)
    overlay.save(root / f'overlay-{name}.png')
    board = Image.new('RGB', (2160,1320), '#211b2c')
    draw = ImageDraw.Draw(board)
    for i, (label, im) in enumerate((('APPROVED MOCKUP',ref),('UNITY GAME VIEW',actual),('50% OVERLAY',overlay))):
        draw.text((i*720+20,12), label, fill='white')
        board.paste(im,(i*720,40))
    board.save(root / f'compare-{name}.jpg', quality=92)
html = '<!doctype html><meta charset="utf-8"><title>UI mockup alignment</title><style>body{background:#211b2c;color:white;font:18px sans-serif;margin:24px}img{width:100%;height:auto}section{margin-bottom:40px}</style><h1>Mockup / Unity / overlay 50%</h1>'
for name in ('Home','Shop','Settings'):
    html += f'<section><h2>{name}</h2><img src="compare-{name}.jpg"></section>'
(root/'comparison.html').write_text(html,encoding='utf-8')
