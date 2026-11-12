// Names that carry meaning through the pipeline (three.js -> glTF -> Blender -> FBX -> Unity).
// A name is a list of tokens joined by two underscores. The first token says what the object is:
//
//   S__<group>__<n>__<rrggbb>   a flat-colored piece of the static world; Blender joins a group into M__<group>
//   W__<set>__<n>__<rrggbb>     the same, for a set that Unity switches on and off; joined into SW__<set>
//   T__<texture>__<n>           a textured piece; Unity gives it the texture Resources/DRONEVR/Textures/<texture>
//   COL__<id>                   an invisible box the player cannot walk or teleport through
//   FLOOR__<id>                 an invisible box whose top the player can stand on and teleport to
//   HOT__<id>                   an invisible box the player can point at and select
//   ZONE__<id>                  an invisible box things can be dropped into (also selectable)
//   A__<id>                     an anchor: a point; A__<id>__N is one metre from it, in the direction it faces
//   CAL__O, CAL__X, CAL__Y, CAL__Z   the origin and the three axes, so Unity can tell how the file was turned
//
// A last token SET_<set> ties a box to a switchable set, so it goes on and off with it.
// (The same names as TANGHALAN VR's pipeline, so the same Blender step builds both.)

import { BufferGeometryUtils } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

const T = () => window.THREE;
let counter = 0;
export const nextId = () => String(++counter).padStart(4, '0');
export const hex6 = (n) => n.toString(16).padStart(6, '0');
export const slug = (s) => String(s).replace(/[^A-Za-z0-9]+/g, '_').replace(/^_+|_+$/g, '');

// An invisible box, as a mesh with its center at the box's center and no rotation.
export function markerBox(kind, id, x0, x1, y0, y1, z0, z1, set) {
  const THREE = T();
  const w = Math.abs(x1 - x0), h = Math.abs(y1 - y0), d = Math.abs(z1 - z0);
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), new THREE.MeshBasicMaterial({ color: kind === 'COL' ? 0xff3355 : kind === 'FLOOR' ? 0x33cc66 : kind === 'HOT' ? 0xffcc00 : 0x3399ff }));
  m.position.set((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
  m.name = kind + '__' + id + (set ? '__SET_' + set : '');
  m.userData.marker = kind;
  return m;
}

// An anchor: a point and, one metre away, the point it faces.
export function anchor(id, x, y, z, nx, ny, nz) {
  const THREE = T();
  const out = [];
  const a = new THREE.Object3D(); a.name = 'A__' + id; a.position.set(x, y, z); a.userData.marker = 'A'; out.push(a);
  if (nx !== undefined) {
    const len = Math.hypot(nx, ny, nz) || 1;
    const n = new THREE.Object3D(); n.name = 'A__' + id + '__N'; n.position.set(x + nx / len, y + ny / len, z + nz / len); n.userData.marker = 'A'; out.push(n);
  }
  return out;
}

export function calibration() {
  const THREE = T();
  return [['O', 0, 0, 0], ['X', 1, 0, 0], ['Y', 0, 1, 0], ['Z', 0, 0, 1]].map(([n, x, y, z]) => {
    const o = new THREE.Object3D(); o.name = 'CAL__' + n; o.position.set(x, y, z); o.userData.marker = 'CAL'; return o;
  });
}

// Turns a built three.js object into a flat list of named meshes in world space, ready to export:
// every mesh's shape is moved to where it stands, so nothing depends on parents, rotations or scales.
//   group(mesh, center)  -> the group name for a flat-colored mesh (a room, a prop, a switchable set)
//   texName(texture, mesh) -> the texture name for a textured mesh
//   skip(mesh) -> true to leave it out
export function flatten(root, opts) {
  const THREE = T();
  const out = [];
  root.updateMatrixWorld(true);
  const box = new THREE.Box3(), center = new THREE.Vector3();
  root.traverse((o) => {
    if (!o.isMesh || !o.visible) return;
    if (o.userData.marker) return;
    if (opts.skip && opts.skip(o)) return;
    let hidden = false;
    for (let p = o.parent; p; p = p.parent) if (!p.visible) hidden = true;
    if (hidden) return;
    const mat = Array.isArray(o.material) ? o.material[0] : o.material;
    const g = o.geometry.clone();
    g.applyMatrix4(o.matrixWorld);
    if (o.matrixWorld.determinant() < 0) flipWinding(g);
    g.computeBoundingBox(); box.copy(g.boundingBox); box.getCenter(center);
    let name, material;
    if (mat.map) {
      const tn = opts.texName(mat.map, o);
      if (!tn) return;
      const rep = mat.map.repeat;
      if (rep.x !== 1 || rep.y !== 1) { const uv = g.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * rep.x, uv.getY(i) * rep.y); }
      name = 'T__' + tn + '__' + nextId();
      material = new THREE.MeshBasicMaterial({ map: opts.plainTexture ? opts.plainTexture(mat.map) : mat.map });
    } else {
      const grp = opts.group(o, center);
      if (!grp) return;
      const sw = grp.startsWith('SET:');
      const col = mixedColor(mat);
      name = (sw ? 'W__' + grp.slice(4) : 'S__' + grp) + '__' + nextId() + '__' + hex6(col);
      material = opts.material(col);
    }
    const m = new THREE.Mesh(g, material);
    m.name = name;
    out.push(m);
  });
  return out;
}

// The color to bake for a material: see-through and glowing ones become a flat color that reads the same.
function mixedColor(mat) {
  const THREE = T();
  const c = mat.color ? mat.color.clone() : new THREE.Color(0xffffff);
  if (mat.emissive && mat.emissiveIntensity) { const e = mat.emissive.clone().multiplyScalar(Math.min(1, mat.emissiveIntensity) * 0.6); c.r = Math.min(1, c.r + e.r); c.g = Math.min(1, c.g + e.g); c.b = Math.min(1, c.b + e.b); }
  if (mat.transparent && mat.opacity < 1) c.lerp(new THREE.Color(0xffffff), (1 - mat.opacity) * 0.5);
  return c.getHex();
}

function flipWinding(g) {
  const idx = g.index;
  if (idx) { for (let i = 0; i < idx.count; i += 3) { const a = idx.getX(i), b = idx.getX(i + 1); idx.setX(i, b); idx.setX(i + 1, a); } }
}

// Joins the pieces that have the same group and the same color into one, so that a town of thousands of boxes
// leaves three.js as a few dozen meshes. Every piece keeps flat faces: the low-poly look, and the same in Unity.
export function merge(meshes) {
  const THREE = T();
  const out = [], buckets = new Map();
  for (const m of meshes) {
    const p = m.name.split('__');
    if (p[0] !== 'S' && p[0] !== 'W') { out.push(m); continue; }
    const key = p[0] + '__' + p[1] + '__' + p[3];
    if (!buckets.has(key)) buckets.set(key, { kind: p[0], group: p[1], color: p[3], material: m.material, geoms: [] });
    let g = m.geometry.index ? m.geometry.toNonIndexed() : m.geometry;
    const flat = new THREE.BufferGeometry();
    flat.setAttribute('position', g.attributes.position.clone());
    flat.computeVertexNormals();
    buckets.get(key).geoms.push(flat);
  }
  for (const b of buckets.values()) {
    const g = b.geoms.length > 1 ? BufferGeometryUtils.mergeBufferGeometries(b.geoms, false) : b.geoms[0];
    const m = new THREE.Mesh(g, b.material);
    m.name = b.kind + '__' + b.group + '__' + nextId() + '__' + b.color;
    out.push(m);
  }
  return out;
}

// The group of a mesh: the nearest 'group' written on it or on what it belongs to ('SET:<name>' for a set).
export function groupOf(o) {
  for (let p = o; p; p = p.parent) if (p.userData && p.userData.group) return p.userData.group;
  return null;
}
