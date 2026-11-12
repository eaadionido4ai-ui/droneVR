// Builds the emergency operations center: the room under the barangay hall where the drone operators work.
// In its middle stands the map table, on which Unity lays a model of the whole barangay, one metre for
// every 200. Around it: the wall screen (north), the mission board (west), the loading dock and the shelf of
// supplies (east), and the pilot's seat (south-east).
//
// The room is built where it stands in DRONE VR's coordinates (plan.js: EOC), so Unity loads it as it is.
// Unity puts its panels and moving things at the anchors (A__...); the boxes (COL__, FLOOR__) are what the
// operator walks on and cannot walk through. See tag.js for the names.

import * as P from './plan.js';
import { makeKit } from './kit.js';
import { markerBox, anchor, calibration, flatten, merge, groupOf } from './tag.js';

const WALL = 0xe9e4d8, BAND = 0x16303f, FLOOR_A = 0x6f7f8c, FLOOR_B = 0x7b8b97, CEIL = 0xf4f1ea, TABLE = 0x2a3a45, RIM = 0x16303f;
const STEEL = 0x8f979e, WOOD = 0x9a7a52, DARK = 0x2c2f33, GOLD = 0xffc53d, LIGHT = 0xfff8e0;

export function buildEoc(T) {
  const K = makeKit(T);
  const root = new T.Group();
  const room = K.group('eoc', root);
  const textures = new Map();
  const extra = [];
  const E = P.EOC, TB = P.TABLE, f = E.floor;
  const x0 = E.x - E.w / 2, x1 = E.x + E.w / 2, z0 = E.z - E.d / 2, z1 = E.z + E.d / 2, top = f + E.h;
  const COL = (id, ax, bx, ay, by, az, bz) => extra.push(markerBox('COL', 'eoc_' + id, ax, bx, ay, by, az, bz));
  const A = (...a) => extra.push(...anchor(...a));

  // ---- the room ----
  for (let i = 0; i < E.w; i++) for (let j = 0; j < E.d; j++) K.slab(room, x0 + i, x0 + i + 1, f - 0.2, f, z0 + j, z0 + j + 1, (i + j) % 2 ? FLOOR_A : FLOOR_B);
  extra.push(markerBox('FLOOR', 'eoc', x0, x1, f - 0.2, f, z0, z1));
  K.slab(room, x0, x1, top, top + 0.2, z0, z1, CEIL);
  const wall = (id, ax, bx, az, bz) => {
    K.slab(room, ax, bx, f, f + 1, az, bz, BAND); K.slab(room, ax, bx, f + 1, f + 1.06, az, bz, GOLD); K.slab(room, ax, bx, f + 1.06, top, az, bz, WALL);
    COL('wall_' + id, ax, bx, f, top, az, bz);
  };
  wall('n', x0 - 0.3, x1 + 0.3, z0 - 0.3, z0); wall('s', x0 - 0.3, x1 + 0.3, z1, z1 + 0.3); wall('w', x0 - 0.3, x0, z0, z1); wall('e', x1, x1 + 0.3, z0, z1);
  for (const dx of [-5, 0, 5]) for (const dz of [-2.5, 2.5]) K.box(room, 2.4, 0.06, 0.6, LIGHT, E.x + dx, top - 0.03, E.z + dz);
  // the door, in the south wall
  K.slab(room, x0 + 1.2, x0 + 2.4, f, f + 2.2, z1 - 0.05, z1, 0x7a5a3a); K.box(room, 0.08, 0.16, 0.06, GOLD, x0 + 2.25, f + 1.05, z1 - 0.07);
  K.sign(room, textures, 'sign_eoc', K.board(1024, 200, '#16303f', '#fff5df', 'BDRRMC · EMERGENCY OPERATIONS CENTER', [], { titleSize: 46, border: '#ffc53d' }), 6, 1.17, E.x, top - 0.75, z1 - 0.02, Math.PI);

  // ---- the map table ----
  const ty = f + TB.top;
  K.slab(room, TB.x - TB.w / 2, TB.x + TB.w / 2, ty - 0.1, ty - 0.012, TB.z - TB.d / 2, TB.z + TB.d / 2, TABLE);
  for (const [ax, bx, az, bz] of [[-TB.w / 2, TB.w / 2, -TB.d / 2, -TB.d / 2 + 0.06], [-TB.w / 2, TB.w / 2, TB.d / 2 - 0.06, TB.d / 2], [-TB.w / 2, -TB.w / 2 + 0.06, -TB.d / 2, TB.d / 2], [TB.w / 2 - 0.06, TB.w / 2, -TB.d / 2, TB.d / 2]])
    K.slab(room, TB.x + ax, TB.x + bx, ty - 0.012, ty + 0.02, TB.z + az, TB.z + bz, RIM);
  K.slab(room, TB.x - TB.w / 2 + 0.3, TB.x + TB.w / 2 - 0.3, f, ty - 0.1, TB.z - TB.d / 2 + 0.3, TB.z + TB.d / 2 - 0.3, 0x1f2a32);
  K.slab(room, TB.x - TB.w / 2 + 0.2, TB.x + TB.w / 2 - 0.2, f, f + 0.08, TB.z - TB.d / 2 + 0.2, TB.z + TB.d / 2 - 0.2, STEEL);
  COL('table', TB.x - TB.w / 2, TB.x + TB.w / 2, f, ty, TB.z - TB.d / 2, TB.z + TB.d / 2);
  // where the middle of the barangay lies on the table, and which way is north
  A('table', TB.x, ty, TB.z, 0, 0, -1);
  // a compass rose at the table's north-east corner
  K.box(room, 0.02, 0.004, 0.16, GOLD, TB.x + TB.w / 2 - 0.16, ty - 0.008, TB.z - TB.d / 2 + 0.2); K.box(room, 0.16, 0.004, 0.02, 0xc9c2b2, TB.x + TB.w / 2 - 0.16, ty - 0.008, TB.z - TB.d / 2 + 0.2);

  // the console at the table's south-west corner, turned to the operator
  A('console', TB.x - TB.w / 2 - 0.55, f + 1.18, TB.z + TB.d / 2 + 0.25, 0.45, 0, 1);
  K.cyl(room, 0.05, 0.05, 0.95, STEEL, TB.x - TB.w / 2 - 0.6, f + 0.48, TB.z + TB.d / 2 + 0.2, 6); K.cyl(room, 0.28, 0.3, 0.04, STEEL, TB.x - TB.w / 2 - 0.6, f + 0.02, TB.z + TB.d / 2 + 0.2, 10);
  // what the mission needs, at the table's south-east corner
  A('needs', TB.x + TB.w / 2 + 0.55, f + 1.18, TB.z + TB.d / 2 + 0.25, -0.45, 0, 1);
  K.cyl(room, 0.05, 0.05, 0.95, STEEL, TB.x + TB.w / 2 + 0.6, f + 0.48, TB.z + TB.d / 2 + 0.2, 6); K.cyl(room, 0.28, 0.3, 0.04, STEEL, TB.x + TB.w / 2 + 0.6, f + 0.02, TB.z + TB.d / 2 + 0.2, 10);

  // ---- the north wall: the big screen ----
  K.slab(room, E.x - 2.5, E.x + 2.5, f + 1.25, f + 3.05, z0, z0 + 0.06, DARK);
  A('screen', E.x, f + 2.15, z0 + 0.09, 0, 0, 1);
  A('status', E.x + 3.9, f + 2.0, z0 + 0.05, 0, 0, 1);
  // a radio rack in the north-east corner
  K.slab(room, x1 - 1.1, x1 - 0.1, f, f + 1.7, z0 + 0.1, z0 + 0.7, 0x3a4650);
  for (let k = 0; k < 4; k++) { K.slab(room, x1 - 1.02, x1 - 0.18, f + 0.25 + k * 0.38, f + 0.52 + k * 0.38, z0 + 0.7, z0 + 0.73, k % 2 ? 0x1f2a32 : 0x2c3a44); K.box(room, 0.05, 0.05, 0.03, k % 2 ? 0x5fd35f : GOLD, x1 - 0.3, f + 0.4 + k * 0.38, z0 + 0.74); }
  K.cyl(room, 0.012, 0.012, 1.2, STEEL, x1 - 0.3, f + 2.3, z0 + 0.4, 5);
  COL('rack', x1 - 1.1, x1, f, f + 1.8, z0, z0 + 0.75);

  // ---- the west wall: the mission board and the briefing ----
  A('board', x0 + 0.05, f + 1.75, E.z + 1.9, 1, 0, 0);
  A('brief', x0 + 0.05, f + 1.75, E.z - 2.2, 1, 0, 0);
  K.slab(room, x0, x0 + 0.04, f + 0.72, f + 2.78, E.z + 0.2, E.z + 3.6, 0x3a2f24); K.slab(room, x0, x0 + 0.04, f + 0.72, f + 2.78, E.z - 4.0, E.z - 0.4, 0x3a2f24);

  // ---- the east wall: the loading dock and the supplies ----
  const bx0 = x1 - 1.0, bz0 = E.z - 4.2, bz1 = E.z - 1.4, by = f + 0.82;
  K.slab(room, bx0, x1 - 0.05, by - 0.06, by, bz0, bz1, WOOD);
  for (const z of [bz0 + 0.1, bz1 - 0.1]) for (const x of [bx0 + 0.08, x1 - 0.15]) K.box(room, 0.07, 0.78, 0.07, STEEL, x, f + 0.39, z);
  K.slab(room, bx0 + 0.05, x1 - 0.1, f + 0.2, f + 0.24, bz0 + 0.05, bz1 - 0.05, STEEL);
  // the charging cradle the drone sits on
  K.slab(room, bx0 + 0.18, bx0 + 0.82, by, by + 0.03, E.z - 3.15, E.z - 2.45, DARK); K.box(room, 0.06, 0.02, 0.06, 0x5fd35f, bx0 + 0.25, by + 0.04, E.z - 2.52);
  COL('dock', bx0, x1, f, by, bz0, bz1);
  A('dock', bx0 + 0.5, by + 0.03, E.z - 2.8, -1, 0, 0);
  A('dock_label', x1 - 0.04, f + 1.75, E.z - 2.8, -1, 0, 0);
  // the shelf: two boards, three kinds of supplies on each
  const sz0 = E.z - 0.6, sz1 = E.z + 2.4, sx0 = x1 - 0.6;
  for (const y of [0.55, 1.05, 1.55]) K.slab(room, sx0, x1 - 0.05, f + y - 0.04, f + y, sz0, sz1, WOOD);
  for (const z of [sz0, (sz0 + sz1) / 2, sz1]) K.slab(room, sx0, x1 - 0.05, f, f + 1.6, z - 0.03, z + 0.03, STEEL);
  COL('shelf', sx0, x1, f, f + 1.65, sz0, sz1);
  P.SUPPLIES.forEach((s, i) => A('shelf_' + s.id, sx0 + 0.27, f + (i < 3 ? 1.05 : 0.55), sz0 + 0.5 + (i % 3) * 1.0, -1, 0, 0));
  A('shelf_label', x1 - 0.04, f + 2.05, (sz0 + sz1) / 2, -1, 0, 0);

  // ---- the pilot's seat, in the south-east corner, facing the big screen ----
  const px = E.x + 5.2, pz = E.z + 3.3;
  K.box(room, 0.5, 0.08, 0.5, 0x2f4f6f, px, f + 0.46, pz); K.box(room, 0.5, 0.6, 0.08, 0x2f4f6f, px, f + 0.8, pz + 0.24); K.cyl(room, 0.04, 0.04, 0.42, STEEL, px, f + 0.22, pz, 6); K.cyl(room, 0.28, 0.28, 0.04, STEEL, px, f + 0.02, pz, 5);
  // the radio control on its stand, at the pilot's right hand
  K.cyl(room, 0.03, 0.03, 0.95, STEEL, px + 0.6, f + 0.48, pz - 0.35, 6); K.cyl(room, 0.18, 0.2, 0.03, STEEL, px + 0.6, f + 0.015, pz - 0.35, 8);
  K.box(room, 0.3, 0.05, 0.2, DARK, px + 0.6, f + 0.98, pz - 0.35, -0.5, 0, 0);
  for (const dx of [-0.08, 0.08]) { K.cyl(room, 0.012, 0.012, 0.06, STEEL, px + 0.6 + dx, f + 1.02, pz - 0.36, 5, -0.5, 0, 0); K.ball(room, 0.018, GOLD, px + 0.6 + dx, f + 1.05, pz - 0.375); }
  K.cyl(room, 0.004, 0.004, 0.25, STEEL, px + 0.72, f + 1.1, pz - 0.44, 4);
  // a mat on the floor marks the spot
  K.slab(room, px - 0.9, px + 0.9, f, f + 0.012, pz - 0.9, pz + 0.9, 0x2a6b8f); K.ring(room, 0.6, 0.03, GOLD, px, f + 0.02, pz, 4, 20);
  A('pilot', px, f, pz - 0.75, 0, 0, -1);
  A('pilot_label', x1 - 0.04, f + 1.75, pz, -1, 0, 0);

  // ---- where the operator starts, and what Unity shows there ----
  A('spawn', E.x, f, E.z + 3.6, 0, 0, -1);
  A('start', E.x, f + 1.5, E.z + 1.9, 0, 0, 1);
  A('results', E.x, f + 1.6, TB.z + TB.d / 2 + 0.9, 0, 0, 1);

  const mats = new Map();
  const material = (col) => { let m = mats.get(col); if (!m) { m = new T.MeshStandardMaterial({ color: col, roughness: 1, metalness: 0 }); mats.set(col, m); } return m; };
  const meshes = merge(flatten(root, { texName: (tex, mesh) => mesh.userData.texName, material, group: groupOf }));
  return { meshes, extra: extra.concat(calibration()), textures };
}
