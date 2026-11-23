VARIANT='hidden'
import math
from mathutils import Vector

TAU = 2.0 * math.pi


def clamp(value, low, high):
    return max(low, min(high, value))


def signed_power(value, exponent):
    if abs(value) < 1.0e-9:
        return 0.0
    return math.copysign(abs(value) ** exponent, value)


def connect_rings(faces, inner, outer):
    for index in range(len(inner)):
        following = (index + 1) % len(inner)
        faces.append((inner[index], inner[following], outer[following], outer[index]))


def append_ring(vertices, points):
    first = len(vertices)
    vertices.extend(points)
    return list(range(first, len(vertices)))


def cap_ring(faces, ring, pole):
    for index in range(len(ring)):
        faces.append((ring[index], ring[(index + 1) % len(ring)], pole))


def mesh_part(name, vertices, faces, pivot, material, parent=None):
    anchor = Vector(pivot)
    local_vertices = [tuple(Vector(vertex) - anchor) for vertex in vertices]
    return mg.mesh(name, local_vertices, faces, loc=pivot, color=material, parent=parent)


def catmull_rom(a, b, c, d, t):
    return 0.5 * (
        2.0 * b
        + (c - a) * t
        + (2.0 * a - 5.0 * b + 4.0 * c - d) * t * t
        + (-a + 3.0 * b - 3.0 * c + d) * t * t * t
    )


def tube_geometry(vertices, faces, controls, sides, steps, depth_scale=1.0, cap_steps=2):
    knots = [Vector(control) for control in controls]
    samples = []
    for index in range(len(knots) - 1):
        a = knots[max(0, index - 1)]
        b = knots[index]
        c = knots[index + 1]
        d = knots[min(index + 2, len(knots) - 1)]
        for step in range(steps):
            samples.append(catmull_rom(a, b, c, d, step / steps))
    samples.append(knots[-1])

    positions = [sample.to_3d() for sample in samples]
    frames = []
    depth_axis = Vector((0.0, 1.0, 0.0))
    for index, center in enumerate(positions):
        before = positions[max(0, index - 1)]
        after = positions[min(len(positions) - 1, index + 1)]
        tangent = (after - before).normalized()
        depth = (depth_axis - tangent * depth_axis.dot(tangent)).normalized()
        across = tangent.cross(depth).normalized()
        frames.append((center, samples[index][3], tangent, across, depth))

    def make_ring(center, radius, across, depth):
        points = []
        for side in range(sides):
            angle = TAU * side / sides
            point = center + radius * (
                math.cos(angle) * across + math.sin(angle) * depth * depth_scale
            )
            points.append(tuple(point))
        return append_ring(vertices, points)

    center, radius, tangent, across, depth = frames[0]
    start_pole = len(vertices)
    vertices.append(tuple(center - tangent * radius))
    previous = None
    for index in range(cap_steps, 0, -1):
        angle = 0.5 * math.pi * index / (cap_steps + 1)
        ring = make_ring(
            center - tangent * radius * math.sin(angle),
            radius * math.cos(angle), across, depth,
        )
        if previous is None:
            cap_ring(faces, ring, start_pole)
        else:
            connect_rings(faces, previous, ring)
        previous = ring

    for center, radius, tangent, across, depth in frames:
        ring = make_ring(center, radius, across, depth)
        connect_rings(faces, previous, ring)
        previous = ring

    for index in range(1, cap_steps + 1):
        angle = 0.5 * math.pi * index / (cap_steps + 1)
        ring = make_ring(
            center + tangent * radius * math.sin(angle),
            radius * math.cos(angle), across, depth,
        )
        connect_rings(faces, previous, ring)
        previous = ring
    end_pole = len(vertices)
    vertices.append(tuple(center + tangent * radius))
    cap_ring(faces, previous, end_pole)


def tube_part(name, controls, sides, steps, pivot, material, parent,
              depth_scale=1.0, cap_steps=2):
    vertices, faces = [], []
    tube_geometry(vertices, faces, controls, sides, steps, depth_scale, cap_steps)
    obj = mesh_part(name, vertices, faces, pivot, material, parent)
    mg.smooth(obj, angle=75)
    return obj


BODY_CENTER_Y = 0.055
BODY_CENTER_Z = 0.468
BODY_HALF_X = 0.390
BODY_HALF_Y = 0.340
BODY_HALF_Z = 0.378
BODY_BEVEL = 0.149

body = mg.box(
    "Body", (2.0 * BODY_HALF_X, 2.0 * BODY_HALF_Y, 2.0 * BODY_HALF_Z),
    bevel=BODY_BEVEL, segments=4, color='hidden',
)
core = (
    BODY_HALF_X - BODY_BEVEL,
    BODY_HALF_Y - BODY_BEVEL,
    BODY_HALF_Z - BODY_BEVEL,
)
body_normals = []
for vertex in body.data.vertices:
    point = vertex.co.copy()
    normal = Vector(tuple(point[i] - clamp(point[i], -core[i], core[i]) for i in range(3)))
    body_normals.append(tuple(normal.normalized()))
    vertex.co.y += BODY_CENTER_Y
    vertex.co.z += BODY_CENTER_Z
for polygon in body.data.polygons:
    polygon.use_smooth = True
body.data.normals_split_custom_set_from_vertices(body_normals)
body.data.update()


def front_surface(x, z):
    dx = max(abs(x) - core[0], 0.0)
    dz = max(abs(z - BODY_CENTER_Z) - core[2], 0.0)
    rounded_depth = math.sqrt(max(BODY_BEVEL ** 2 - dx ** 2 - dz ** 2, 0.0))
    return BODY_CENTER_Y - core[1] - rounded_depth


def rounded_limb(name, pivot, shape):
    sides = 18
    divisions = 10
    vertices = [shape(-1.0, 0.0, 0.0)]
    faces = []
    previous = None
    for index in range(1, divisions):
        latitude = -0.5 * math.pi + math.pi * index / divisions
        vertical = math.sin(latitude)
        profile = math.cos(latitude)
        ring = append_ring(vertices, [
            shape(vertical, profile, TAU * side / sides) for side in range(sides)
        ])
        if previous is None:
            cap_ring(faces, ring, 0)
        else:
            connect_rings(faces, previous, ring)
        previous = ring
    top = len(vertices)
    vertices.append(shape(1.0, 0.0, 0.0))
    cap_ring(faces, previous, top)
    obj = mesh_part(name, vertices, faces, pivot, 'hidden', body)
    mg.smooth(obj, angle=75)
    return obj


def make_limb_pair(side, suffix):
    def arm_shape(vertical, profile, angle):
        center_x = 0.416 - 0.045 * vertical - 0.013 * vertical * vertical
        width = 0.081 * profile ** 0.84 * (1.0 - 0.17 * vertical)
        depth = 0.089 * profile ** 0.88 * (1.0 - 0.10 * vertical)
        return (
            side * (center_x + width * math.cos(angle)),
            0.026 - 0.015 * vertical + depth * math.sin(angle),
            0.357 + 0.139 * vertical,
        )

    def foot_shape(vertical, profile, angle):
        dx = 0.114 * profile ** 0.69 * signed_power(math.cos(angle), 0.91)
        dy = 0.137 * profile ** 0.69 * signed_power(math.sin(angle), 0.91)
        return (
            side * (0.172 + dx + 0.055 * dy),
            -0.013 + dy + 0.029 * vertical,
            0.067 + 0.067 * signed_power(vertical, 0.76),
        )

    rounded_limb("Arm_" + suffix, (side * 0.370, 0.012, 0.481), arm_shape)
    rounded_limb("Foot_" + suffix, (side * 0.172, 0.032, 0.113), foot_shape)


make_limb_pair(1, "L")
make_limb_pair(-1, "R")

# Accessory supplied by family appendix.


EYE_WIDTH = 0.108
EYE_HEIGHT = 0.102
EYE_BULGE = 0.081
PUPIL_U = 0.044
PUPIL_V = 0.017
PUPIL_WIDTH = 0.046
PUPIL_HEIGHT = 0.058


def sclera_depth(u, v):
    distance_squared = (u / EYE_WIDTH) ** 2 + (v / EYE_HEIGHT) ** 2
    return EYE_BULGE * math.sqrt(max(0.0, 1.0 - distance_squared))


def pupil_depth(u, v):
    distance_squared = ((u - PUPIL_U) / PUPIL_WIDTH) ** 2 + ((v - PUPIL_V) / PUPIL_HEIGHT) ** 2
    return sclera_depth(u, v) + 0.0035 + 0.010 * math.sqrt(max(0.0, 1.0 - distance_squared))


def make_eye_shell(name, pivot, eye_point):
    sides = 24
    vertices = [eye_point(0.0, 0.0, EYE_BULGE)]
    faces = []
    previous = None
    for ring_index in range(9):
        points = []
        for index in range(sides):
            azimuth = TAU * index / sides
            if ring_index == 5:
                latitude = 1.32 - 0.068 * math.sin(azimuth)
            else:
                latitude = (0.23, 0.46, 0.69, 0.92, 1.10, 0.0, 1.57, 1.95, 2.40)[ring_index]
            spread = math.sin(latitude)
            depth = math.cos(latitude) * (EYE_BULGE if latitude < math.pi / 2.0 else 0.041)
            points.append(eye_point(
                EYE_WIDTH * spread * math.cos(azimuth),
                EYE_HEIGHT * spread * math.sin(azimuth),
                depth,
            ))
        ring = append_ring(vertices, points)
        if previous is None:
            cap_ring(faces, ring, 0)
        else:
            connect_rings(faces, previous, ring)
        previous = ring
    pole = len(vertices)
    vertices.append(eye_point(0.0, 0.0, -0.041))
    cap_ring(faces, previous, pole)

    obj = mesh_part(name, vertices, faces, pivot, "white", body)
    obj.data.materials.append(mg.color("ink"))
    for polygon in obj.data.polygons:
        if polygon.index >= 6 * sides:
            polygon.material_index = 1
    return obj


def eye_overlay(name, eye_point, pivot, center, radii, depth_at, thickness, sides, material):
    u, v = center
    vertices = [eye_point(u, v, depth_at(u, v) + thickness)]
    faces = []
    previous = None
    for fraction in (0.38, 0.72, 0.93, 1.0, 0.98):
        points = []
        for index in range(sides):
            angle = TAU * index / sides
            x = u + radii[0] * fraction * math.cos(angle)
            z = v + radii[1] * fraction * math.sin(angle)
            lift = thickness * math.sqrt(max(0.0, 1.0 - fraction ** 2))
            if previous is not None and fraction == 0.98:
                lift = -0.009
            points.append(eye_point(x, z, depth_at(x, z) + lift))
        ring = append_ring(vertices, points)
        if previous is None:
            cap_ring(faces, ring, 0)
        else:
            connect_rings(faces, previous, ring)
        previous = ring

    pole = len(vertices)
    vertices.append(eye_point(u, v, depth_at(u, v) - 0.023))
    cap_ring(faces, previous, pole)
    return mesh_part(name, vertices, faces, pivot, material, body)


def make_eye(side, suffix, height):
    pivot = (side * 0.164, -0.299, height)
    center = Vector(pivot)
    yaw = side * 0.145
    roll = side * -0.078
    horizontal = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    vertical = Vector((0.0, 0.0, 1.0))
    outward = Vector((math.sin(yaw), -math.cos(yaw), 0.0))
    u_axis = horizontal * math.cos(roll) + vertical * math.sin(roll)
    v_axis = vertical * math.cos(roll) - horizontal * math.sin(roll)

    def eye_point(u, v, depth):
        return tuple(center + u_axis * u + v_axis * v + outward * depth)

    shell = make_eye_shell("Eye_Shell_" + suffix, pivot, eye_point)
    pupil = eye_overlay(
        "Pupil_" + suffix, eye_point, pivot,
        (PUPIL_U, PUPIL_V), (PUPIL_WIDTH, PUPIL_HEIGHT),
        pupil_depth, 0.0, 18, "ink",
    )
    glint = eye_overlay(
        "Catchlight_" + suffix, eye_point, pivot,
        (PUPIL_U - 0.018, PUPIL_V + 0.028), (0.0105, 0.0115),
        lambda u, v: pupil_depth(u, v) + 0.002, 0.003, 12, "white",
    )

    lashes = [
        [(0.073, 0.058, 0.042, 0.012), (0.095, 0.064, 0.033, 0.010),
         (0.111, 0.077, 0.024, 0.006), (0.118, 0.096, 0.021, 0.0015)],
        [(0.088, 0.037, 0.039, 0.011), (0.110, 0.044, 0.028, 0.010),
         (0.127, 0.056, 0.018, 0.006), (0.137, 0.070, 0.015, 0.0015)],
        [(0.096, 0.016, 0.034, 0.010), (0.115, 0.018, 0.028, 0.009),
         (0.131, 0.025, 0.020, 0.005), (0.142, 0.038, 0.015, 0.0015)],
    ]
    if VARIANT not in ('purple','pink'):
        lashes=[]
    lash_vertices, lash_faces = [], []
    for lash in lashes:
        controls = [(*eye_point(side * u, v, depth), radius) for u, v, depth, radius in lash]
        tube_geometry(lash_vertices, lash_faces, controls, 6, 2, 0.49, 1)
    lash_mesh = mesh_part("Lashes_" + suffix, lash_vertices, lash_faces, pivot, "ink", body)

    brow_path = [
        (0.101, height + 0.205, 0.004),
        (0.128, height + 0.220, 0.010),
        (0.168, height + 0.220, 0.012),
        (0.205, height + 0.207, 0.009),
        (0.239, height + 0.181, 0.002),
    ]
    if VARIANT in ('red','orange'):
        brow_path=[(.095,height+.125,.012),(.13,height+.15,.018),
                   (.19,height+.20,.020),(.24,height+.225,.010)]
    elif VARIANT == 'yellow':
        brow_path=[(.095,height+.245,.008),(.13,height+.24,.013),
                   (.19,height+.19,.013),(.24,height+.165,.004)]
    brow_controls = [
        (side * x, front_surface(side * x, z) - 0.001, z, radius)
        for x, z, radius in brow_path
    ]
    brow = tube_part(
        "Brow_" + suffix, brow_controls, 7, 3, pivot, "ink", body, 0.61, 1,
    )
    eye = mg.join("Eye_" + suffix, [shell, pupil, glint, lash_mesh, brow])
    mg.smooth(eye, angle=68)


for side,suffix in ((1,'L'),(-1,'R')):
    center=side*.164
    tube_part('Eye_'+suffix,[(center-.065,-.303,.494,.009),
              (center,-.31,.473,.01),(center+.065,-.303,.494,.009)],
              7,3,(center,-.30,.49),'ink',body,cap_steps=1)


smile_profile = [
    (-0.080, 0.329, 0.007),
    (-0.056, 0.311, 0.009),
    (-0.023, 0.302, 0.010),
    (0.014, 0.304, 0.010),
    (0.053, 0.317, 0.0095),
    (0.084, 0.338, 0.0085),
    (0.105, 0.367, 0.007),
]
if VARIANT == 'red':
    smile_profile=[(-.09,.315,.008),(-.05,.35,.011),(0,.36,.012),(.055,.345,.01),(.09,.32,.007)]
elif VARIANT == 'yellow':
    smile_profile=[(-.055,.31,.007),(-.035,.34,.009),(0,.35,.01),(.035,.34,.009),(.055,.31,.007)]
elif VARIANT == 'hidden':
    smile_profile=[(-.025,.32,.006),(0,.315,.007),(.025,.32,.006)]
smile_controls = [
    (x, front_surface(x, z) - 0.003, z, radius) for x, z, radius in smile_profile
]
tube_part("Mouth", smile_controls, 8, 3, (0.011, -0.285, 0.320), "ink", body, 0.68)

# Soft toy material finish
for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35

# Geometry-only appendix to the approved shared character body.
parts = []

def blob(name, radius, loc, scale, color):
    obj = mg.sphere(name, radius, loc=loc, scale=scale, segments=8, rings=4,
                    color=color, parent=body)
    mg.smooth(obj, angle=75)
    parts.append(obj)
    return obj

def strand(name, controls, color):
    obj = tube_part(name, controls, 8, 3, (0, 0, .84), color, body, cap_steps=1)
    parts.append(obj)

if VARIANT == 'blue':
    vertices, faces = [], []
    for side, height in ((1,.501),(-1,.487)):
        first = len(vertices)
        for i in range(24):
            a = TAU*i/24
            for j in range(6):
                b = TAU*j/6
                rr = .127 + .013*math.cos(b)
                vertices.append((side*.164+rr*math.cos(a), -.399-.013*math.sin(b),
                                 height+rr*math.sin(a)))
        for i in range(24):
            for j in range(6):
                faces.append((first+i*6+j, first+((i+1)%24)*6+j,
                              first+((i+1)%24)*6+(j+1)%6, first+i*6+(j+1)%6))
    obj=mg.mesh('GlassesFrames',vertices,faces,color='ink',parent=body)
    mg.smooth(obj,angle=75)
    parts.append(obj)
    strand('GlassesBridge',[(-.043,-.405,.50,.012),(0,-.410,.52,.012),(.043,-.405,.51,.012)],'ink')
    for side in (-1,1):
        strand('GlassesTemple',[(side*.29,-.398,.51,.012),(side*.35,-.30,.53,.011),
                               (side*.38,-.13,.53,.008)],'ink')
elif VARIANT == 'green':
    for index,(cx,cz) in enumerate(((-.23,.86),(0,.925),(.23,.86))):
        for i in range(5):
            a=TAU*i/5
            obj=blob('DaisyPetal',.029,(cx+.042*math.cos(a),-.16,cz+.042*math.sin(a)),
                     (.75,.40,1.35),'white')
            obj.rotation_euler.y=math.pi/2-a
        blob('DaisyCentre',.031,(cx,-.179,cz),(1,.48,1),'gold')
    for side in (-1,1):
        blob('CrownLeaf',.075,(side*.31,-.05,.855),(1,.35,.42),'green')
elif VARIANT == 'orange':
    for i,(x,top) in enumerate(((-.12,1.00),(0,1.075),(.12,1.015))):
        strand('Quiff',[(x*.65,.015,.82,.048),(x,-.005,.92,.037),
                       (x+.055,-.025,top,.003)],'orange')
elif VARIANT == 'yellow':
    strand('WorriedQuiff',[(0,.015,.82,.034),(.025,-.01,.96,.034),
                          (-.015,-.03,1.035,.026),(-.075,-.04,1.015,.015),
                          (-.055,-.04,.98,.002)],'yellow')
elif VARIANT == 'pink':
    blob('BowLeft',.075,(.115,-.015,.88),(1.0,.65,1.1),'pink')
    blob('BowRight',.075,(.265,-.015,.88),(1.0,.65,1.1),'pink')
    blob('BowKnot',.044,(.19,-.063,.88),(.9,.6,1.0),'pink')
elif VARIANT == 'brown':
    strand('CurledQuiff',[(0,.015,.82,.045),(-.045,-.01,.94,.038),
                          (.015,-.035,.995,.031),(.075,-.045,.965,.021),
                          (.045,-.045,.93,.002)],'brown')
elif VARIANT == 'cyan':
    strand('WaveQuiff',[(-.09,.01,.82,.04),(-.04,-.015,.95,.035),
                       (.045,-.035,1.025,.025),(.115,-.04,.99,.002)],'cyan')
elif VARIANT == 'hidden':
    strand('QuestionMark',[(-.075,-.31,.68,.014),(-.055,-.315,.735,.015),
                           (.015,-.315,.748,.015),(.067,-.315,.707,.014),
                           (.042,-.315,.66,.014),(0,-.315,.635,.014),
                           (0,-.315,.60,.014)],'ink')
    blob('QuestionDot',.019,(0,-.315,.557),(1,.65,1),'ink')

if parts:
    mg.join('Accessory',parts)
else:
    mg.empty('Accessory',loc=(0,0,.84),parent=body)

# Re-centre the full silhouette without changing the approved proportions.
bpy.context.view_layer.update()
coords=[obj.matrix_world @ v.co for obj in bpy.context.scene.objects
        if obj.type=='MESH' for v in obj.data.vertices]
lo=Vector((min(v.x for v in coords),min(v.y for v in coords),min(v.z for v in coords)))
hi=Vector((max(v.x for v in coords),max(v.y for v in coords),max(v.z for v in coords)))
offset=Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z))
body.location += offset

for material in mg.materials.values():
    shader = material.node_tree.nodes.get('Principled BSDF')
    if shader:
        shader.inputs['Roughness'].default_value=.35
