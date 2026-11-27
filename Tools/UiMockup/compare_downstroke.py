from pathlib import Path
from PIL import Image, ImageDraw

root=Path('docs/art/ui-squad-theme-20261126')
paths=[root/'alignment/reference-Settings.png',root/'typography/runtime/Settings-Final.png',root/'downstroke/Settings.png']
images=[Image.open(p).convert('RGB').resize((720,1280)) for p in paths]
board=Image.new('RGB',(1350,525),'#211b2c');draw=ImageDraw.Draw(board)
for i,title in enumerate(('MOCKUP','STROKE CU','STROKE DO XUONG')):draw.text((i*450+12,12),title,fill='white')
for y,box in [(45,(235,317,510,394)),(195,(235,725,495,795)),(345,(265,847,465,919))]:
    for i,im in enumerate(images):
        crop=im.crop(box);crop.thumbnail((420,135))
        crop=crop.resize((420,int(crop.height*420/crop.width)))
        board.paste(crop,(i*450+12,y))
board.save(root/'downstroke/compare-stroke.jpg',quality=95)
