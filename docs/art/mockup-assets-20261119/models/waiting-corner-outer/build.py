import math
radius=0.23
inner=radius-.12
points=[(radius*math.cos(math.pi*i/24)-radius/2,radius*math.sin(math.pi*i/24)-radius/2) for i in range(13)]
points += [(inner*math.cos(math.pi*i/24)-radius/2,inner*math.sin(math.pi*i/24)-radius/2) for i in range(12,-1,-1)]
obj=mg.profile('Rim',points,.26,axis='Z',loc=(0,0,.13),bevel=.012,segments=2,color='waiting_rim')
mg.smooth(obj,angle=40)

for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35
