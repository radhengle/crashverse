# CVM1 exporter: Blender mesh objects -> compact binary mesh bundle for Unity (Crashverse.Visuals.CvMesh).
# Coordinates: Unity (x, y, z) = Blender (x, z, y)  (y/z swap = handedness flip -> triangle winding reversed).
import bpy, struct, bmesh
def export(path, objs):
    out = bytearray(b'CVM1'); out += struct.pack('<i', len(objs))
    for o in objs:
        dg = bpy.context.evaluated_depsgraph_get()
        oe = o.evaluated_get(dg); me = oe.to_mesh()
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
        mw = o.matrix_world; nm = mw.to_3x3().inverted().transposed()
        uvl = me.uv_layers.active.data if me.uv_layers.active else None
        col = me.color_attributes.active_color if me.color_attributes else None
        cn = me.corner_normals
        verts = {}; vlist = []; idx = []
        for poly in me.polygons:
            tri = []
            for li in poly.loop_indices:
                l = me.loops[li]; p = mw @ me.vertices[l.vertex_index].co
                n = (nm @ cn[li].vector).normalized()
                uv = tuple(uvl[li].uv) if uvl else (0.0, 0.0)
                c = (1,1,1,1)
                if col is not None:
                    c = tuple(col.data[li].color) if col.domain == 'CORNER' else tuple(col.data[l.vertex_index].color)
                key = (round(p.x,5),round(p.y,5),round(p.z,5),round(n.x,3),round(n.y,3),round(n.z,3),round(uv[0],4),round(uv[1],4),round(c[0],3),round(c[1],3),round(c[2],3))
                if key not in verts:
                    verts[key] = len(vlist); vlist.append((p, n, uv, c))
                tri.append(verts[key])
            idx += [tri[0], tri[2], tri[1]]  # winding flip
        oe.to_mesh_clear()
        name = o.name.encode('utf8'); out += struct.pack('<i', len(name)) + name
        out += struct.pack('<ii', len(vlist), len(idx))
        for p, n, uv, c in vlist: out += struct.pack('<3f', p.x, p.z, p.y)
        for p, n, uv, c in vlist: out += struct.pack('<3f', n.x, n.z, n.y)
        for p, n, uv, c in vlist: out += struct.pack('<2f', uv[0], uv[1])
        for p, n, uv, c in vlist: out += bytes([max(0,min(255,int(round(x*255)))) for x in c])
        out += struct.pack('<%di' % len(idx), *idx)
        print('CVM', o.name, len(vlist), len(idx)//3)
    open(path, 'wb').write(out)
