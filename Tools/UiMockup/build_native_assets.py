"""Author fresh vector UI assets at each actual target size; export native PNG without resizing.

This does not read or modify the rejected atlas. SVG geometry and PNG geometry are
authored from dimensions, fixed bevel widths and glyph paths, not scaled bitmap art.
"""
from pathlib import Path
import json
import math
import hashlib
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/art/ui-native-20261125'
ASSETS=OUT/'assets'
ASSETS.mkdir(exist_ok=True)

def rgb(code):
    return np.array([int(code[i:i+2],16) for i in (0,2,4)],np.float32)/255

class Painter:
    def __init__(self,w,h):
        self.w,self.h=w,h
        self.pw,self.ph=math.ceil(w),math.ceil(h)
        self.y,self.x=np.mgrid[:self.ph,:self.pw].astype(np.float32)
        self.x+=.5;self.y+=.5
        self.rgba=np.zeros((self.ph,self.pw,4),np.float32)
        self.svg=[];self.defs=[]

    def layer(self,mask,color):
        alpha=np.clip(mask,0,1)
        if np.ndim(color)==1:color=np.broadcast_to(color,(self.ph,self.pw,3))
        old=self.rgba[:,:,3]
        out=alpha+old*(1-alpha)
        self.rgba[:,:,:3]=(color*alpha[:,:,None]+self.rgba[:,:,:3]*old[:,:,None]*(1-alpha[:,:,None]))/np.maximum(out[:,:,None],1e-6)
        self.rgba[:,:,3]=out

    def distance(self,box,r):
        x,y,w,h=box;r=max(0,min(r,w/2,h/2))
        qx=np.abs(self.x-(x+w/2))-(w/2-r)
        qy=np.abs(self.y-(y+h/2))-(h/2-r)
        return np.hypot(np.maximum(qx,0),np.maximum(qy,0))+np.minimum(np.maximum(qx,qy),0)-r

    def rect(self,box,r,top,bottom):
        x,y,w,h=box
        if w<=0 or h<=0:return
        distance=self.distance(box,r)
        mask=np.clip(.5-distance,0,1)
        mix=np.clip((self.y-y)/h,0,1)
        color=rgb(top)[None,None,:]*(1-mix[:,:,None])+rgb(bottom)[None,None,:]*mix[:,:,None]
        self.layer(mask,color)
        name='g'+str(len(self.defs))
        self.defs.append(f'<linearGradient id="{name}" x2="0" y2="1"><stop stop-color="#{top}"/><stop offset="1" stop-color="#{bottom}"/></linearGradient>')
        self.svg.append(f'<rect x="{x:.3f}" y="{y:.3f}" width="{w:.3f}" height="{h:.3f}" rx="{r:.3f}" fill="url(#{name})"/>')

    def ellipse(self,cx,cy,rx,ry,code):
        if min(rx,ry)<=0:return
        dist=(np.hypot((self.x-cx)/rx,(self.y-cy)/ry)-1)*min(rx,ry)
        self.layer(np.clip(.5-dist,0,1),rgb(code))
        self.svg.append(f'<ellipse cx="{cx:.3f}" cy="{cy:.3f}" rx="{rx:.3f}" ry="{ry:.3f}" fill="#{code}"/>')

    def puffy(self,box,r,top,bottom,subtle=False):
        x,y,w,h=box
        distance=self.distance(box,r)
        mask=np.clip(.5-distance,0,1)
        dy,dx=np.gradient(distance)
        length=np.maximum(np.hypot(dx,dy),.001)
        light=(-dx*.45-dy*.89)/length
        bevel=min(8,max(3,h*.08))
        edge=np.exp(np.minimum(distance,0)/bevel)
        mix=np.clip((self.y-y)/h,0,1)
        base=rgb(top)[None,None,:]*(1-mix[:,:,None])+rgb(bottom)[None,None,:]*mix[:,:,None]
        shade=edge*light*(.08 if subtle else .24)
        spec=np.exp(-(((self.x-(x+min(r+9,w*.24)))/min(19,w*.16))**2+((self.y-(y+min(12,h*.2)))/min(5.5,h*.1))**2))
        spec*=.07 if subtle else .48
        color=np.clip(base*(1+shade[:,:,None])+spec[:,:,None]*(1-base),0,1)
        self.layer(mask,color)
        clip='clip'+str(len(self.defs));shine='shine'+str(len(self.defs))
        self.defs.append(f'<clipPath id="{clip}"><rect x="{x:.3f}" y="{y:.3f}" width="{w:.3f}" height="{h:.3f}" rx="{r:.3f}"/></clipPath><radialGradient id="{shine}"><stop stop-color="#fff" stop-opacity="{.12 if subtle else .65}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>')
        self.svg.append(f'<ellipse clip-path="url(#{clip})" cx="{x+min(r+9,w*.24):.3f}" cy="{y+min(12,h*.2):.3f}" rx="{min(19,w*.16):.3f}" ry="{min(5.5,h*.1):.3f}" fill="url(#{shine})"/>')

    def poly(self,points,code):
        inside=np.zeros((self.ph,self.pw),bool)
        nearest=np.full((self.ph,self.pw),np.inf,np.float32)
        for i,(ax,ay) in enumerate(points):
            bx,by=points[(i+1)%len(points)];vx,vy=bx-ax,by-ay
            t=np.clip(((self.x-ax)*vx+(self.y-ay)*vy)/max(vx*vx+vy*vy,1e-8),0,1)
            nearest=np.minimum(nearest,np.hypot(self.x-ax-t*vx,self.y-ay-t*vy))
            inside^=((ay>self.y)!=(by>self.y))&(self.x<(bx-ax)*(self.y-ay)/(by-ay+1e-12)+ax)
        self.layer(np.clip(.5-np.where(inside,-nearest,nearest),0,1),rgb(code))
        self.svg.append('<polygon points="'+' '.join(f'{x:.3f},{y:.3f}' for x,y in points)+f'" fill="#{code}"/>')

    def save(self,key):
        array=np.round(np.clip(self.rgba,0,1)*255).astype(np.uint8)
        Image.fromarray(array).save(ASSETS/(key+'.png'))
        svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w:.3f}" height="{self.h:.3f}" viewBox="0 0 {self.w:.3f} {self.h:.3f}"><defs>'+''.join(self.defs)+'</defs>'+''.join(self.svg)+'</svg>'
        (ASSETS/(key+'.svg')).write_text(svg)

def surface(p,role):
    w,h=p.w,p.h
    if role=='footer':
        p.rect((0,0,w,h),0,'652AA1','391A68')
        p.rect((0,0,w,3),0,'D5BEF0','B08BD8')
        return
    panel=role=='panel'
    selected=role=='selected'
    radius=min(36 if panel else 24,min(w,h)/2-1)
    rim=5 if panel else 3
    if role=='lime':top,bottom='AEF744','65BC1B'
    elif role in ('panel','selected'):top,bottom=('FCF9FF','E7DCF5') if panel else ('F1FFF8','C8EDD9')
    elif role=='pill':top,bottom='494579','282847'
    else:top,bottom='AC66E5','6F2FA6'
    p.rect((0,2,w,h-2),radius,'6E448B','4D2869')
    p.rect((0,0,w,h-3),radius,'F6EDFF','B291CE')
    p.rect((rim,rim,w-2*rim,h-2*rim-3),max(0,radius-rim),top,bottom)
    p.puffy((rim,rim,w-2*rim,h-2*rim-3),max(0,radius-rim),top,bottom,subtle=panel or selected)

def icon(p,role):
    w,h=p.w,p.h;s=min(w,h);cx=w/2;cy=h/2
    if role=='heart':
        points=[]
        for t in np.linspace(0,2*np.pi,80,endpoint=False):
            points.append((cx+s*.027*16*np.sin(t)**3,cy-s*.027*(13*np.cos(t)-5*np.cos(2*t)-2*np.cos(3*t)-np.cos(4*t))))
        p.poly(points,'C52651')
        points=[(cx+(x-cx)*.9,cy+(y-cy)*.9-.01*s) for x,y in points]
        p.poly(points,'F65F7D');p.ellipse(cx-.15*s,cy-.15*s,.09*s,.04*s,'FFA7B7');return
    base='lime' if role in ('plus','check') else 'pill' if role=='coin' else 'purple'
    if role=='coin':
        p.ellipse(cx,cy,s*.48,s*.48,'C99424');p.ellipse(cx,cy-s*.015,s*.44,s*.44,'FFE184');p.ellipse(cx,cy-s*.015,s*.37,s*.37,'EABD42')
    else:
        r=s*.48
        p.ellipse(cx,cy,r,r,'BDA1D9');p.ellipse(cx,cy-1,r-2,r-2,'F4EBFF')
        p.ellipse(cx,cy-1,r-5,r-5,'8BCB2F' if base=='lime' else '8148B8')
        p.puffy((cx-r+5,cy-r+4,2*(r-5),2*(r-5)),r-5,'A8E65C' if base=='lime' else 'A967DA','6DA829' if base=='lime' else '6E38A1')
    white='FCFAFF';ink='63388E';gold='F6D575';r=.23*s
    if role in ('plus','close'):
        if role=='plus':
            p.rect((cx-.06*s,cy-.25*s,.12*s,.5*s),.04*s,white,'E3D8EF');p.rect((cx-.25*s,cy-.06*s,.5*s,.12*s),.04*s,white,'E3D8EF')
        else:
            for sign in (-1,1):
                v=np.array([1,sign])/math.sqrt(2);n=np.array([-v[1],v[0]])
                points=[tuple(np.array([cx,cy])+v*a*s+n*b*s) for a,b in [(-.27,-.06),(.27,-.06),(.27,.06),(-.27,.06)]]
                p.poly(points,white)
    elif role=='coin':
        points=[(cx+math.sin(i*math.pi/5)*s*(.26 if i%2==0 else .12),cy-math.cos(i*math.pi/5)*s*(.26 if i%2==0 else .12)) for i in range(10)]
        p.poly(points,'FFF1B0')
    elif role=='home':
        p.poly([(cx-.28*s,cy-.03*s),(cx,cy-.28*s),(cx+.28*s,cy-.03*s),(cx+.20*s,cy-.03*s),(cx+.20*s,cy+.25*s),(cx-.20*s,cy+.25*s),(cx-.20*s,cy-.03*s)],white)
        p.rect((cx-.055*s,cy+.05*s,.11*s,.20*s),.01*s,ink,ink)
    elif role in ('shop','gift'):
        p.rect((cx-.22*s,cy-.10*s,.44*s,.36*s),.055*s,white,'D9C9EB')
        p.ellipse(cx,cy-.10*s,.12*s,.17*s,white);p.ellipse(cx,cy-.10*s,.075*s,.12*s,ink)
        p.rect((cx-.20*s,cy,.40*s,.23*s),.03*s,white,'E7DDF4')
    elif role=='lock':
        p.ellipse(cx,cy-.09*s,.14*s,.20*s,gold);p.ellipse(cx,cy-.09*s,.085*s,.14*s,ink)
        p.rect((cx-.22*s,cy-.02*s,.44*s,.32*s),.045*s,'FFE69C','E4B541')
        p.ellipse(cx,cy+.08*s,.035*s,.045*s,ink);p.rect((cx-.018*s,cy+.10*s,.036*s,.07*s),.01*s,ink,ink)
    elif role=='check':
        p.poly([(cx-.26*s,cy),(cx-.18*s,cy-.08*s),(cx-.04*s,cy+.06*s),(cx+.22*s,cy-.20*s),(cx+.30*s,cy-.12*s),(cx-.04*s,cy+.23*s)],white)
    elif role=='restart':
        p.ellipse(cx,cy,.25*s,.25*s,white);p.ellipse(cx,cy,.15*s,.15*s,ink)
        p.rect((cx+.12*s,cy-.27*s,.19*s,.20*s),0,ink,ink)
        p.poly([(cx+.04*s,cy-.28*s),(cx+.30*s,cy-.28*s),(cx+.30*s,cy-.03*s)],white)
    elif role=='badge':
        p.ellipse(cx,cy,.46*s,.46*s,'CC395D');p.ellipse(cx,cy-1,.41*s,.41*s,'F96C86')
    elif role=='settings':
        points=[]
        for i in range(48):
            a=i*2*math.pi/48;rad=s*(.28 if i%4 in (0,1) else .22)
            points.append((cx+rad*math.cos(a),cy+rad*math.sin(a)))
        p.poly(points,white);p.ellipse(cx,cy,.105*s,.105*s,ink)

def render(role,w,h):
    p=Painter(w,h)
    if role in ('toggle','toggle-off'):
        p.rect((0,0,w,h),h/2,'CDB5E4','9E7CC0')
        p.rect((2,2,w-4,h-4),(h-4)/2,'A7E35D' if role=='toggle' else '8E6BB8','70AD35' if role=='toggle' else '654A8F')
        rad=(h-8)/2;cx=w-h/2 if role=='toggle' else h/2
        p.ellipse(cx,h/2+1,rad,rad,'BBA8CB');p.ellipse(cx,h/2-1,rad-1,rad-1,'FCF9FF')
    elif role in ('lime','purple','panel','header','pill','footer','selected'):surface(p,role)
    else:icon(p,role)
    return p

layout=json.loads((OUT/'before-layout.json').read_text())
assets={};instances=[]
for page in layout['pages']:
    for node in page['nodes']:
        role=node.get('role');size=node.get('native')
        if not node.get('sprite') or role=='keep' or not size or min(size)<=0:continue
        w,h=map(float,size)
        key=f'{role}_{w:.3f}x{h:.3f}'
        instances.append({'page':page['name'],'node':node['path'],'asset':key+'.png','role':role,'local_size':[w,h]})
        if key not in assets:
            render(role,w,h).save(key)
            png=ASSETS/(key+'.png')
            assert Image.open(png).size==(math.ceil(w),math.ceil(h))
            assets[key]={'png':key+'.png','svg':key+'.svg','native_local_size':[w,h],'png_pixels':[math.ceil(w),math.ceil(h)],'ppu':1,'border':[0,0,0,0],'sha256':hashlib.sha256(png.read_bytes()).hexdigest()}
manifest={'schema':'native-ui-assets/v1','method':'Fresh size-specific vector geometry and native pixel rendering; no source bitmap sampling, no resize, no nine-slice, no aspect distortion. Fractional local dimensions are preserved by the SVG viewBox and exact Unity Sprite.rect; PNG canvas is ceil(local size).','assets':assets,'instances':instances}
(OUT/'asset-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({'native_assets':len(assets),'mapped_instances':len(instances),'png':'native dimensions','svg':'native viewBox','resize_operations':0}))
