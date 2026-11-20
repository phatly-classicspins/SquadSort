obj=mg.box('Rim',(1.15,.12,.26),loc=(0,0,.13),bevel=.045,segments=4,color='waiting_rim')
mg.smooth(obj,angle=40)
for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35
