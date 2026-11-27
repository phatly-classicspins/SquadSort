from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import html

ROOT=Path('docs/art/wireframe-review-20261127')
FONT='C:/Windows/Fonts/arial.ttf'
def font(n): return ImageFont.truetype(FONT,n)
frames={
 'home':('figma-home-gameplay.jpg',(142,127,502,767)),
 'gameplay':('figma-home-gameplay.jpg',(630,127,990,767)),
 'win':('figma-win.jpg',(519,127,879,767)),
 'settings':('figma-settings.jpg',(326,129,686,769)),
 'settings-home':('figma-settings.jpg',(727,129,1087,769)),
 'shop':('figma-shop-no-offers.jpg',(383,126,743,766)),
 'shop-offers':('figma-shop-offers.jpg',(383,126,743,766)),
}
for name,(source,box) in frames.items():
 Image.open(ROOT/source).convert('RGB').crop(box).resize((720,1280),Image.Resampling.LANCZOS).save(ROOT/f'ref-{name}.png')

pages=[
 ('shop','Shop — khoảng trống và kích thước thẻ','game-shop-settled.png',
  [((0,150,720,224),(0,106,720,197)),((20,224,700,260),(20,197,700,358)),((34,366,686,700),(15,458,705,824))],
  ['1. Mái che trong game nằm cao hơn khoảng 44 px so với Figma.',
   '2. Khoảng trống sau mái che ~161 px; Figma ~36 px. Đã kiểm tra hết animation.',
   '3. Thẻ 690×366 lớn hơn ~650×334; gói Medium bị đẩy xuống dưới footer.',
   'So cùng trạng thái NoSpecialOffer; màu/asset mới đã được duyệt riêng.']),
 ('settings','Settings — tỷ lệ popup và nút','game-settings.png',
  [((86,376,636,988),(50,328,670,1008)),((168,706,548,810),(138,704,592,832))],
  ['1. Thân popup game rộng 620 px; Figma khoảng 550 px, lớn hơn ~13%.',
   '2. Nút Restart game rộng 454 px; Figma khoảng 380 px, lớn hơn ~19%.',
   'Đủ 3 toggle + Restart/Home. Cụm nhân vật là phần theme mới đã duyệt.']),
 ('home','Home — nút Play và phần minh họa','game-home.png',
  [((156,920,556,1048),(144,934,580,1087))],
  ['1. Nút Play game 436×153; Figma khoảng 400×128, lớn hơn và thấp hơn.',
   'Logo/khay là minh họa theme mới; Figma đặt placeholder V, không đủ để so art.',
   'Icon ưu đãi phụ thuộc trạng thái mua/hết hạn; không kết luận thiếu chỉ từ ảnh.']),
 ('gameplay','Gameplay — booster và vùng banner','game-gameplay.png',
  [((100,1055,634,1155),(105,1125,612,1235)),((0,1190,720,1280),(0,1190,720,1280))],
  ['1. Cụm booster trong phiên game nằm thấp hơn vị trí vẽ trong wireframe.',
   '2. Figma có Banner Ads; ảnh game chưa có. Banner có điều kiện ads gate/level.',
   'Cần kiểm tra trạng thái quảng cáo; khác level (Figma 10 / game 20), không so board.']),
 ('win','Win — đủ cụm chức năng, khác tỷ lệ CTA','game-win-fixture.png',
  [((160,1115,560,1245),(144,1091,580,1244))],
  ['1. Nút Continue lớn hơn và cao hơn CTA Level 10 vẽ trong wireframe.',
   'Đã có reward → tiến trình element → bonus meter/RV → nút tiếp tục.',
   'Ảnh game là fixture, không cộng thưởng. Copy/40 xu theo GDD, không phải lỗi.']),
]

def wrap(draw,text,maxwidth,size):
 words=text.split();lines=[];line=''
 for word in words:
  test=(line+' '+word).strip()
  if draw.textlength(test,font=font(size))>maxwidth and line:lines.append(line);line=word
  else:line=test
 if line:lines.append(line)
 return lines

for name,title,actual,regions,notes in pages:
 board=Image.new('RGB',(1080,1300),'#201d2c');d=ImageDraw.Draw(board)
 d.text((28,15),title,font=font(29),fill='white')
 for col,(path,label,color) in enumerate([(ROOT/f'ref-{name}.png','FIGMA / WIREFRAME','#ffc14d'),(ROOT/actual,'GAME / ẢNH CHỤP MỚI','#ff5875')]):
  im=Image.open(path).convert('RGB').resize((480,854),Image.Resampling.LANCZOS)
  x=30+col*540;y=96
  board.paste(im,(x,y));d.text((x,62),label,font=font(22),fill=color)
  for number,pair in enumerate(regions,1):
   a,b,c,e=pair[col];box=(x+a*2/3,y+b*2/3,x+c*2/3,y+e*2/3)
   d.rectangle(box,outline=color,width=4)
   tx=min(box[0]+4,x+445);ty=max(y,box[1]-26)
   d.rectangle((tx,ty,tx+27,ty+27),fill=color);d.text((tx+6,ty+1),str(number),font=font(22),fill='#201d2c')
 y=969
 for note in notes:
  for line in wrap(d,note,1020,21):d.text((30,y),line,font=font(21),fill='#eee9fa');y+=28
  y+=8
 board.crop((0,0,1080,max(1140,y+15))).save(ROOT/f'compare-{name}.jpg',quality=94)

doc='<!doctype html><html lang="vi"><meta charset="utf-8"><title>Squad Sort — đối chiếu Figma</title><style>body{background:#201d2c;color:#eee9fa;font:17px Arial;margin:24px auto;max-width:1120px}img{width:100%;height:auto}a{color:#ffd27b}section{margin:36px 0}p{line-height:1.6}</style><h1>Chưa khớp hoàn toàn wireframe</h1><p>Trái: ảnh chụp Figma. Phải: ảnh chụp Unity mới. Khoanh số là điểm cần đối chiếu; không tự sửa game trong lượt kiểm tra này. Các số đo Figma là ước lượng từ ảnh ở zoom 50%, chuẩn hóa về 720×1280.</p><p><a href="https://www.figma.com/design/yVNFbPJSNsAtIImpdhjKgH/Squad-Sort?node-id=0-1">File Figma gốc</a> · <a href="REVIEW.md">Phạm vi và ghi chú kiểm tra</a></p>'
for name,title,*_ in pages:doc+=f'<section><h2>{html.escape(title)}</h2><a href="compare-{name}.jpg"><img src="compare-{name}.jpg"></a></section>'
doc+='<section><h2>Settings ở Home</h2><p>Đã có Terms of Service và Privacy Policy. Xem ảnh gốc: <a href="ref-settings-home.png">Figma</a> / <a href="game-settings-home.png">Game</a>.</p></section></html>'
(ROOT/'review.html').write_text(doc,encoding='utf-8')

overview=Image.new('RGB',(1080,600),'#201d2c')
for i,name in enumerate(('shop','settings')):
 im=Image.open(ROOT/f'compare-{name}.jpg');im.thumbnail((540,600));overview.paste(im,(i*540,0))
overview.save(ROOT/'overview.jpg',quality=95)
