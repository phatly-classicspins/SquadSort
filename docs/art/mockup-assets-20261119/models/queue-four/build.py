import math


HALF_WIDTH = 0.64
HALF_LENGTH = 2.30
RIM_HALF_THICKNESS = 0.06
FRONT_RADIUS = 0.235
BACK_RADIUS = 0.37
FRONT_STEPS = 8
BACK_STEPS = 10
TIP_STEPS = 8

side_x = HALF_WIDTH - RIM_HALF_THICKNESS
front_y = -HALF_LENGTH + RIM_HALF_THICKNESS + FRONT_RADIUS
back_y = HALF_LENGTH - RIM_HALF_THICKNESS - BACK_RADIUS

# Follow the rim from the right mouth tip, around the back, to the left
# mouth tip. Each sample contains its centre and outward plan normal.
path = []


def add_arc(cx, cy, radius, start, end, steps):
    for i in range(steps + 1):
        angle = math.radians(start + (end - start) * i / steps)
        nx = math.cos(angle)
        ny = math.sin(angle)
        path.append((cx + radius * nx, cy + radius * ny, nx, ny))


add_arc(side_x - FRONT_RADIUS, front_y, FRONT_RADIUS, -90, 0, FRONT_STEPS)
add_arc(side_x - BACK_RADIUS, back_y, BACK_RADIUS, 0, 90, BACK_STEPS)
add_arc(-side_x + BACK_RADIUS, back_y, BACK_RADIUS, 90, 180, BACK_STEPS)
add_arc(-side_x + FRONT_RADIUS, front_y, FRONT_RADIUS, 180, 270, FRONT_STEPS)


def rim_outline(radius):
    outer = [(x + radius * nx, y + radius * ny) for x, y, nx, ny in path]
    inner = [(x - radius * nx, y - radius * ny) for x, y, nx, ny in path]
    left_x, left_y = path[-1][:2]
    right_x, right_y = path[0][:2]
    left_tip = []
    right_tip = []

    for i in range(1, TIP_STEPS):
        angle = math.pi * i / TIP_STEPS
        dx = radius * math.sin(angle)
        dy = radius * math.cos(angle)
        left_tip.append((left_x + dx, left_y - dy))
        right_tip.append((right_x - dx, right_y + dy))

    return outer + left_tip + inner[::-1] + right_tip


# Round the foot into the straight walls and roll the shoulders into a
# narrow, flat crown. Each height uses the same continuous closed outline.
rim_profile = []
for i in range(4):
    angle = math.pi * i / 6
    rim_profile.append((
        0.029 * (1 - math.cos(angle)),
        0.031 + 0.029 * math.sin(angle),
    ))
for i in range(6):
    angle = math.pi * i / 10
    rim_profile.append((
        0.206 + 0.054 * math.sin(angle),
        RIM_HALF_THICKNESS - 0.054 * (1 - math.cos(angle)),
    ))

rim_vertices = []
for z, radius in rim_profile:
    rim_vertices.extend((x, y, z) for x, y in rim_outline(radius))

path_count = len(path)
outline_count = 2 * path_count + 2 * (TIP_STEPS - 1)
inner_start = path_count + TIP_STEPS - 1
left_tip_start = path_count - 1
right_tip_start = inner_start + path_count - 1
rim_faces = []
accent_faces = []

for layer in range(len(rim_profile) - 1):
    lower = layer * outline_count
    upper = lower + outline_count
    for i in range(outline_count):
        j = (i + 1) % outline_count
        outer_mouth = i < 5 or path_count - 6 <= i < left_tip_start
        outer_tip = (
            left_tip_start <= i < left_tip_start + TIP_STEPS // 2
            or right_tip_start + TIP_STEPS // 2 <= i
        )
        if rim_profile[layer][0] < 0.249 and (outer_mouth or outer_tip):
            accent_faces.append(len(rim_faces))
        rim_faces.append((lower + i, lower + j, upper + j, upper + i))

# Close each end of the vertical stack with a strip following the rim
# and a semicircular face at each mouth tip.
for layer in (0, len(rim_profile) - 1):
    base = layer * outline_count
    cap_faces = []
    for i in range(path_count - 1):
        inner_i = inner_start + path_count - 1 - i
        cap_faces.append((i, i + 1, inner_i - 1, inner_i))
    cap_faces.append(tuple(range(left_tip_start, inner_start + 1)))
    cap_faces.append(tuple(range(right_tip_start, outline_count)) + (0,))
    for face in cap_faces:
        if layer == 0:
            face = face[::-1]
        rim_faces.append(tuple(base + index for index in face))

rim = mg.mesh("Rim", rim_vertices, rim_faces, color="tray_rim")
accent_index = len(rim.data.materials)
rim.data.materials.append(mg.color("waiting_rim"))
for face_index in accent_faces:
    rim.data.polygons[face_index].material_index = accent_index
mg.smooth(rim, angle=40)


def floor_outline(inset):
    # The floor overlaps the foot of the rim and extends through the
    # open mouth as a low, beveled threshold.
    edge_x = path[0][0] - 0.015 - inset
    edge_y = -HALF_LENGTH + 0.007 + inset
    rim_overlap = 0.024 + inset
    outline = [(edge_x, edge_y)]
    outline.extend(
        (x - rim_overlap * nx, y - rim_overlap * ny)
        for x, y, nx, ny in path
    )
    outline.append((-edge_x, edge_y))
    return outline


floor_profile = ((0.0, 0.004), (0.004, 0.0), (0.034, 0.0), (0.04, 0.006))
floor_vertices = []
for z, inset in floor_profile:
    floor_vertices.extend((x, y, z) for x, y in floor_outline(inset))

floor_count = path_count + 2
floor_faces = [tuple(reversed(range(floor_count)))]
for layer in range(len(floor_profile) - 1):
    lower = layer * floor_count
    upper = lower + floor_count
    for i in range(floor_count):
        j = (i + 1) % floor_count
        floor_faces.append((lower + i, lower + j, upper + j, upper + i))

top = (len(floor_profile) - 1) * floor_count
floor_faces.append(tuple(range(top, top + floor_count)))
mg.mesh("Floor", floor_vertices, floor_faces, color="tray_floor")

# Soft toy material finish
for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35
