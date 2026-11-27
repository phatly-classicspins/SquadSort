"""Render size-specific 3D button assets directly at native pixel resolution.

Existing source buttons are visual references only. No bitmap is stretched or sampled.
Run inside Blender: -b -P ... -- <native-art-folder>.
"""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Vector

OUT=Path(sys.argv[sys.argv.index('--')+1]).resolve()
ASSETS=OUT/'assets'
SOURCES=OUT/'button-sources'
SOURCES.mkdir(exist_ok=True)
manifest=json.loads((OUT/'asset-manifest.json').read_text())

def rgba(code):
    c=[int(code[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in c)+(1,)

def material(name,code,roughness=.2,metal=0,checker=False,width=0,height=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=rgba(code)
    bsdf.inputs['Roughness'].default_value=roughness
    bsdf.inputs['Metallic'].default_value=metal
    bsdf.inputs['Coat Weight'].default_value=.45 if not metal else .25
    bsdf.inputs['Coat Roughness'].default_value=.14
    if checker:
        nodes=m.node_tree.nodes;links=m.node_tree.links
        coord=nodes.new('ShaderNodeTexCoord')
        scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(width/44,height/44,0)
        rotation=nodes.new('ShaderNodeVectorRotate');rotation.rotation_type='AXIS_ANGLE';rotation.inputs['Axis'].default_value=(0,0,1);rotation.inputs['Angle'].default_value=math.pi/4
        pattern=nodes.new('ShaderNodeTexChecker');pattern.inputs['Color1'].default_value=rgba('65DB08');pattern.inputs['Color2'].default_value=rgba('57CD05');pattern.inputs['Scale'].default_value=1
        links.new(coord.outputs['Generated'],scale.inputs[0]);links.new(scale.outputs[0],rotation.inputs['Vector']);links.new(rotation.outputs['Vector'],pattern.inputs['Vector']);links.new(pattern.outputs['Color'],bsdf.inputs['Base Color'])
    return m

def rounded_ring(w,h,r,z,cx,cy):
    r=max(.05,min(r,w/2-.01,h/2-.01));points=[]
    for x,y,start in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for j in range(12):
            angle=math.radians(start+j*90/12)
            points.append((cx+x+r*math.cos(angle),cy+y+r*math.sin(angle),z))
    return points

def solid(name,w,h,r,z,thickness,mat,cx,cy,bevel=3):
    bevel=min(bevel,w*.12,h*.12,thickness*.45)
    profile=[(bevel*.5,z),(0,z+bevel),(0,z+thickness-bevel),(bevel*.25,z+thickness-.25*bevel),(bevel,z+thickness)]
    if name in ('EnamelFace','DiamondEnamelFace'):
        profile=[(0,z),(0,z+thickness*.2)]
        profile += [((h/2-.5)*j/24,z+thickness*(.2+.8*math.sin(math.pi*j/48))) for j in range(1,25)]
    verts=[]
    for inset,level in profile:verts+=rounded_ring(w-2*inset,h-2*inset,max(.1,r-inset),level,cx,cy)
    n=48;faces=[tuple(reversed(range(n)))]
    for ring in range(len(profile)-1):
        for i in range(n):faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
    faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj);mesh.materials.append(mat)
    for f in mesh.polygons:f.use_smooth=len(f.vertices)==4
    return obj

def build(key,asset):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
    scene.render.film_transparent=True
    w,h=asset['native_local_size'];pw,ph=asset['png_pixels'];cx,cy=w/2,h/2
    role=key.split('_')[0];metal_frame=role=='lime' and h>=140 and w>350
    radius=min(h*.42,38)
    gold=material('PolishedGold','FFC83C',.17,.72)
    gold_light=material('GoldLip','FFE77D',.13,.62)
    bronze=material('DeepBronze','9C490B',.24,.65)
    rim_dark=material('DeepGreen' if role=='lime' else 'DeepViolet','205800' if role=='lime' else '32115F',.21)
    rim=material('LimeRim' if role=='lime' else 'LavenderRim','C4FA67' if role=='lime' else 'D5AAFF',.18)
    face=material('FaceEnamel','65D30B' if role=='lime' else '9023D6',.20,checker=metal_frame,width=w,height=h)
    if metal_frame:
        bw,bh=w-30,h-30;radius=bh*.34
        solid('BronzeBody',bw,bh,radius,0,6,bronze,cx,cy,2)
        solid('OuterGoldFrame',bw-3,bh-3,radius-1,5,5,gold,cx,cy,1.5)
        solid('GoldHighlightLip',bw-7,bh-7,radius-3,9,4,gold_light,cx,cy,1)
        solid('InnerBronzeGroove',bw-13,bh-13,radius-6,12,4,bronze,cx,cy,1)
        solid('InnerGoldFrame',bw-18,bh-18,radius-8,15,4,gold,cx,cy,1)
        solid('FaceRecess',bw-25,bh-25,radius-12,18,4,rim_dark,cx,cy,1)
        solid('InnerLimeLip',bw-29,bh-29,radius-14,21,3,rim,cx,cy,1)
        solid('DiamondEnamelFace',bw-34,bh-34,radius-16,23,10,face,cx,cy,4)
        for side in (-1,1):
            solid('EndCapSocket',18,bh*.48,8,7,5,bronze,cx+side*(bw/2-1),cy,2)
            solid('EndCap',14,bh*.43,6,11,10,gold,cx+side*(bw/2-1),cy,4)
            solid('EndCapGlint',3,bh*.31,1.4,21,1,gold_light,cx+side*(bw/2-3),cy,1)
    else:
        bw,bh=w-3,h-5
        solid('DarkLowerEdge',bw,bh,radius,0,5,rim_dark,cx,cy-1.5,2)
        solid('OuterRaisedRim',bw-2,bh-2,radius-1,4,5,rim,cx,cy,2)
        solid('RecessedSeam',bw-7,bh-7,radius-4,8,3,rim_dark,cx,cy,1)
        solid('EnamelFace',bw-11,bh-11,max(3,radius-6),10,min(10,bh*.14),face,cx,cy,min(4,bh*.06))

    # The reference uses a deliberately painted broad reflection, recreated as native
    # geometry on the button face rather than a stretched highlight bitmap.
    white=material('BroadWhiteReflection','FFFFFF',.16)
    white.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value=(1,1,1,1)
    white.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=1.4
    fw,fh=(bw-34,bh-34) if metal_frame else (bw-11,bh-11)
    face_top=33 if metal_frame else 10+min(10,bh*.14)
    glint_width=min(31,max(12,fw*.085));glint_height=min(9,max(4,fh*.10))
    gx=cx-fw/2+min(radius*.72,fw*.2)+4;gy=cy+fh/2-min(13,fh*.19)
    glint=solid('BroadSpecularHighlight',glint_width,glint_height,glint_height*.49,face_top+.4,.45,white,0,0,.1)
    glint.location=(gx,gy,0);glint.rotation_euler.z=math.radians(30);glint.visible_shadow=False
    world=bpy.data.worlds.new('Studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.7,.75,.85,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35;scene.world=world
    # Fixed-size lamps preserve the scale of bevel highlights across asset variants.
    for name,position,size,power in [('Key',(cx-130,cy+170,370),180,1500000),('Fill',(cx+180,cy+70,300),160,700000),('Strip',(cx,cy+200,220),240,700000)]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power*.55;data.shape='RECTANGLE';data.size=size;data.size_y=18 if name=='Strip' else size*.55
        obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector((cx,cy,15))-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('NativePixelCamera');data.type='ORTHO';data.sensor_fit='HORIZONTAL';data.ortho_scale=pw
    camera=bpy.data.objects.new('NativePixelCamera',data);scene.collection.objects.link(camera);camera.location=(pw/2,ph/2,1000);camera.rotation_euler=(0,0,0);scene.camera=camera
    scene.render.resolution_x=pw;scene.render.resolution_y=ph;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.view_settings.view_transform='Standard';scene.view_settings.exposure=-.7
    scene.render.filepath=str(ASSETS/asset['png'])
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCES/(key+'.blend')))
    bpy.ops.render.render(write_still=True)
    print('NATIVE_RENDER '+key+' '+str(pw)+'x'+str(ph),flush=True)

for key,asset in manifest['assets'].items():
    if key.startswith(('lime_','purple_')):build(key,asset)
