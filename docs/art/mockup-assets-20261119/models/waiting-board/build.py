import math
from mathutils.geometry import delaunay_2d_cdt


PITCH = 1.15
HALF_WIDTH = 2 * PITCH
HALF_LENGTH = PITCH
HEIGHT = 0.26
RIM_THICKNESS = 0.12
RIM_INSIDE = RIM_THICKNESS
FLOOR_CLEARANCE = 0.0005
FLOOR_BOTTOM = 0.004
FLOOR_TOP = 0.049
CORNER_SEGMENTS = 6

OUTLINE = [
    (-HALF_WIDTH, -HALF_LENGTH),
    (HALF_WIDTH, -HALF_LENGTH),
    (HALF_WIDTH, 0.0),
    (PITCH, 0.0),
    (PITCH, HALF_LENGTH),
    (-HALF_WIDTH, HALF_LENGTH),
]
OUTLINE_RADII = [0.23, 0.23, 0.23, 0.13, 0.23, 0.23]


def unit_edge(start, end):
    dx, dy = end[0] - start[0], end[1] - start[1]
    length = math.hypot(dx, dy)
    return dx / length, dy / length


def rounded_outline(corners, radii, inset):
    points = []
    for i, (x, y) in enumerate(corners):
        incoming = unit_edge(corners[i - 1], corners[i])
        outgoing = unit_edge(corners[i], corners[(i + 1) % len(corners)])
        left_in = (-incoming[1], incoming[0])
        left_out = (-outgoing[1], outgoing[0])
        cross = incoming[0] * outgoing[1] - incoming[1] * outgoing[0]
        turn = 1 if cross > 0 else -1

        radius = radii[i] - turn * inset
        corner_x = x + inset * (left_in[0] + left_out[0])
        corner_y = y + inset * (left_in[1] + left_out[1])
        center_x = corner_x - radius * incoming[0] + turn * radius * left_in[0]
        center_y = corner_y - radius * incoming[1] + turn * radius * left_in[1]
        start_angle = math.atan2(-turn * left_in[1], -turn * left_in[0])

        for step in range(CORNER_SEGMENTS + 1):
            angle = start_angle + turn * math.pi * step / (2 * CORNER_SEGMENTS)
            points.append((
                center_x + radius * math.cos(angle),
                center_y + radius * math.sin(angle),
            ))
    return points


def append_loop(vertices, points, z):
    start = len(vertices)
    vertices.extend((x, y, z) for x, y in points)
    return list(range(start, len(vertices)))


def bridge(faces, first, second):
    for i in range(len(first)):
        j = (i + 1) % len(first)
        faces.append((first[i], first[j], second[j], second[i]))


def circular_bevel(center_inset, center_z, radius, start_angle, end_angle):
    profile = []
    segments = 4
    for step in range(segments + 1):
        fraction = step / segments
        angle = start_angle + (end_angle - start_angle) * fraction
        profile.append((
            center_inset + radius * math.cos(angle),
            center_z + radius * math.sin(angle),
        ))
    return profile


def build_rim():
    vertices, faces = [], []
    outer_foot = 0.026
    outer_top = 0.038
    inner_top = 0.032
    inner_foot = 0.014
    quarter = math.pi / 2
    profile = (
        circular_bevel(outer_foot, outer_foot, outer_foot, -quarter, -math.pi)
        + circular_bevel(outer_top, HEIGHT - outer_top, outer_top, math.pi, quarter)
        + circular_bevel(
            RIM_INSIDE - inner_top, HEIGHT - inner_top, inner_top, quarter, 0.0
        )
        + circular_bevel(RIM_INSIDE - inner_foot, inner_foot, inner_foot, 0.0, -quarter)
    )
    loops = [
        append_loop(vertices, rounded_outline(OUTLINE, OUTLINE_RADII, inset), z)
        for inset, z in profile
    ]
    for i, loop in enumerate(loops):
        bridge(faces, loop, loops[(i + 1) % len(loops)])

    rim = mg.mesh("Rim", vertices, faces, color="waiting_rim")
    mg.smooth(rim, angle=40)
    return rim


def inside_outline(x, y, points):
    inside = False
    previous_x, previous_y = points[-1]
    for current_x, current_y in points:
        if (current_y > y) != (previous_y > y):
            crossing = current_x + (previous_x - current_x) * (
                (y - current_y) / (previous_y - current_y)
            )
            if x < crossing:
                inside = not inside
        previous_x, previous_y = current_x, current_y
    return inside


def add_cap(vertices, faces, boundary, holes=(), upward=True):
    loops = [boundary, *holes]
    polygons = [[vertices[index][:2] for index in loop] for loop in loops]
    points = []
    edges = []
    vertex_indices = []
    for loop, polygon in zip(loops, polygons):
        start = len(points)
        points.extend(polygon)
        vertex_indices.extend(loop)
        edges.extend(
            (start + i, start + (i + 1) % len(loop))
            for i in range(len(loop))
        )

    coordinates, _, triangles, sources, _, _ = delaunay_2d_cdt(
        points, edges, [], 0, 1e-6, True
    )
    for triangle in triangles:
        a, b, c = (coordinates[index] for index in triangle)
        x = (a.x + b.x + c.x) / 3.0
        y = (a.y + b.y + c.y) / 3.0
        if not inside_outline(x, y, polygons[0]):
            continue
        if any(inside_outline(x, y, polygon) for polygon in polygons[1:]):
            continue
        face = tuple(vertex_indices[sources[index][0]] for index in triangle)
        ccw = (b.x - a.x) * (c.y - a.y) > (b.y - a.y) * (c.x - a.x)
        faces.append(face if ccw == upward else face[::-1])


def cell_corners(column, row, columns):
    half_seam = 0.023
    edge_margin = 0.137
    left = -HALF_WIDTH + column * PITCH
    bottom = -HALF_LENGTH + row * PITCH
    right = left + PITCH
    top = bottom + PITCH

    left += edge_margin if column == 0 else half_seam
    right -= edge_margin if column == columns - 1 else half_seam
    bottom += edge_margin if row == 0 else half_seam
    top -= edge_margin if row == 1 or column == 3 else half_seam
    return [(left, bottom), (right, bottom), (right, top), (left, top)]


def add_cell(vertices, faces, seam_faces, corners):
    radii = [0.135] * 4
    profile = [
        (0.000, FLOOR_TOP),
        (0.008, FLOOR_TOP - 0.001),
        (0.017, FLOOR_TOP - 0.006),
        (0.026, FLOOR_TOP - 0.009),
    ]
    loops = [
        append_loop(vertices, rounded_outline(corners, radii, inset), z)
        for inset, z in profile
    ]
    for i in range(len(loops) - 1):
        start = len(faces)
        bridge(faces, loops[i], loops[i + 1])
        if i == 1:
            seam_faces.extend(range(start, len(faces)))

    center_x = (corners[0][0] + corners[2][0]) / 2
    center_y = (corners[0][1] + corners[2][1]) / 2
    center = len(vertices)
    vertices.append((center_x, center_y, profile[-1][1]))
    inner_loop = loops[-1]
    for i, index in enumerate(inner_loop):
        faces.append((center, index, inner_loop[(i + 1) % len(inner_loop)]))
    return loops[0]


def build_floor():
    vertices, faces, seam_faces = [], [], []
    edge = rounded_outline(OUTLINE, OUTLINE_RADII, RIM_INSIDE + FLOOR_CLEARANCE)
    base = append_loop(vertices, edge, FLOOR_BOTTOM)
    surface = append_loop(vertices, edge, FLOOR_TOP)
    bridge(faces, base, surface)
    add_cap(vertices, faces, base, upward=False)

    cells = []
    for row, columns in enumerate((4, 3)):
        for column in range(columns):
            cells.append(add_cell(
                vertices, faces, seam_faces, cell_corners(column, row, columns)
            ))
    add_cap(vertices, faces, surface, holes=cells)

    floor = mg.mesh("Floor", vertices, faces, color="tray_floor")
    floor.data.materials.append(mg.color("hidden"))
    for index in seam_faces:
        floor.data.polygons[index].material_index = 1

    side_face_count = len(base)
    for polygon in floor.data.polygons:
        polygon.use_smooth = polygon.index < side_face_count
    return floor


build_floor()
build_rim()

# Soft toy material finish
for material in mg.materials.values():
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35
