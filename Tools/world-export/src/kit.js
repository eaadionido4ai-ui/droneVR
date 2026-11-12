// Small helpers to build low-poly things in three.js: boxes, cylinders, roofs and painted signs. Every piece
// has one flat color. A piece belongs to the 'group' written on the THREE.Group it is added to (see tag.js):
// Blender joins the pieces of a group into one object, and Unity can switch a 'SET:<name>' group on and off.

export function makeKit(T) {
  const mats = new Map();
  const lam = (color) => { let m = mats.get(color); if (!m) { m = new T.MeshLambertMaterial({ color }); mats.set(color, m); } return m; };
  const add = (mesh, parent, x, y, z, rx, ry, rz) => {
    mesh.position.set(x || 0, y || 0, z || 0);
    if (rx || ry || rz) mesh.rotation.set(rx || 0, ry || 0, rz || 0);
    parent.add(mesh);
    return mesh;
  };

  const kit = {
    T, lam,

    /** A group of pieces: name is its group ('SET:<name>' for a set Unity switches). */
    group(name, parent) { const g = new T.Group(); if (name) g.userData.group = name; if (parent) parent.add(g); return g; },

    /** A box by its size and its center. */
    box(parent, w, h, d, color, x, y, z, rx, ry, rz) { return add(new T.Mesh(new T.BoxGeometry(w, h, d), lam(color)), parent, x, y, z, rx, ry, rz); },

    /** A box from corner to corner. */
    slab(parent, x0, x1, y0, y1, z0, z1, color) {
      return kit.box(parent, Math.abs(x1 - x0), Math.abs(y1 - y0), Math.abs(z1 - z0), color, (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
    },

    /** An upright cylinder (or a cone, with a top radius of 0) by its center. */
    cyl(parent, rTop, rBottom, h, color, x, y, z, sides, rx, ry, rz) {
      return add(new T.Mesh(new T.CylinderGeometry(rTop, rBottom, h, sides || 8), lam(color)), parent, x, y, z, rx, ry, rz);
    },

    /** A rough ball: 20 flat faces, or 80 with detail 1. */
    ball(parent, r, color, x, y, z, detail) { return add(new T.Mesh(new T.IcosahedronGeometry(r, detail || 0), lam(color)), parent, x, y, z); },

    /** A ring lying flat (turn it with rx to stand it up). */
    ring(parent, r, tube, color, x, y, z, sides, around, rx, ry, rz) {
      return add(new T.Mesh(new T.TorusGeometry(r, tube, sides || 6, around || 16), lam(color)), parent, x, y, z, rx === undefined ? Math.PI / 2 : rx, ry, rz);
    },

    /**
     * A roof with two slopes over a rectangle: the ridge runs along the longer side. w along x, d along z,
     * from y (the top of the walls) up to y + rise, with the eaves over metres past the walls.
     */
    gable(parent, x, y, z, w, d, rise, color, over) {
      const o = over === undefined ? 0.5 : over, alongX = w >= d;
      const span = (alongX ? d : w) / 2 + o, len = (alongX ? w : d) + 2 * o;
      const shape = new T.Shape();
      shape.moveTo(-span, 0); shape.lineTo(span, 0); shape.lineTo(0, rise); shape.lineTo(-span, 0);
      const g = new T.ExtrudeGeometry(shape, { depth: len, bevelEnabled: false });
      g.translate(0, 0, -len / 2);
      const m = new T.Mesh(g, lam(color));
      // the shape is drawn across x and pushed out along z: turn it when the ridge runs along x
      if (alongX) m.rotation.y = Math.PI / 2;
      return add(m, parent, x, y, z, 0, m.rotation.y, 0);
    },

    /** A flat piece with its own outline (x, z pairs), at a height, so thick: a puddle, a patch of ground. */
    patch(parent, points, y, thick, color) {
      const shape = new T.Shape();
      points.forEach(([px, pz], i) => (i ? shape.lineTo(px, -pz) : shape.moveTo(px, -pz)));
      const g = new T.ExtrudeGeometry(shape, { depth: thick, bevelEnabled: false });
      g.rotateX(-Math.PI / 2);
      return add(new T.Mesh(g, lam(color)), parent, 0, y, 0);
    },

    /** A picture painted on a canvas. */
    canvas(w, h, draw) {
      const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
      draw(cv.getContext('2d'), w, h);
      return cv;
    },

    /**
     * A flat sign showing a painted canvas, w by h metres, facing the way yaw says (0 faces south, +z;
     * Math.PI / 2 faces east). textures collects the canvases by name: each becomes Textures/<name>.png.
     */
    sign(parent, textures, name, cv, w, h, x, y, z, yaw, tilt) {
      const tex = new T.CanvasTexture(cv);
      const m = new T.Mesh(new T.PlaneGeometry(w, h), new T.MeshBasicMaterial({ map: tex, transparent: true }));
      m.userData.texName = name;
      textures.set(name, cv);
      m.rotation.order = 'YXZ';
      return add(m, parent, x, y, z, tilt || 0, yaw || 0, 0);
    },

    /** A board with words on it: a title line and smaller lines under it. */
    board(w, h, background, color, title, lines, opts) {
      const o = opts || {};
      return kit.canvas(w, h, (c) => {
        c.fillStyle = background; c.fillRect(0, 0, w, h);
        if (o.border) { c.strokeStyle = o.border; c.lineWidth = 14; c.strokeRect(7, 7, w - 14, h - 14); }
        c.fillStyle = color; c.textAlign = 'center'; c.textBaseline = 'middle';
        const ts = o.titleSize || Math.round(h * 0.34);
        c.font = '900 ' + ts + 'px Nunito, sans-serif';
        const n = (lines || []).length, top = n ? h * 0.36 : h * 0.52;
        c.fillText(title, w / 2, top, w - 60);
        c.font = '800 ' + Math.round(ts * 0.48) + 'px Nunito, sans-serif';
        if (o.sub) c.fillStyle = o.sub;
        (lines || []).forEach((line, i) => c.fillText(line, w / 2, top + ts * 0.78 + i * ts * 0.56, w - 60));
      });
    },
  };
  return kit;
}

/** A repeatable source of numbers from 0 to 1, so the same town is built every time. */
export function seeded(a) {
  return function () {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
