mg.box('Floor',(1.15,1.15,.04),loc=(0,0,.02),bevel=.018,segments=3,color='tray_floor')
for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35
