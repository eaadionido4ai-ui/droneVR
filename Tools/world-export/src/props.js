// The things that move: the drone, what it carries, the people it looks for, the rescue boat, and the rings of
// the flight school. Each is exported on its own, standing on y = 0 at the origin, facing north (-z), so that
// in Unity it faces forward.

import * as P from './plan.js';
import { makeKit } from './kit.js';
import { anchor, calibration, flatten, merge, groupOf } from './tag.js';

const DARK = 0x2c2f33, STEEL = 0x8f979e, WHITE = 0xf4f4f0, ORANGE = 0xff7a1a, RED = 0xc62f22, GREEN = 0x3fbf5a, NAVY = 0x16303f, SKIN = [0xc68a55, 0xd69a63, 0xb87a48, 0xe0aa78];

// The drone: a quadcopter 1.1 m across, on two skids, with a camera under its nose and three hooks under its
// belly. Its four propellers are groups of their own (prop1 to prop4), so that Unity can spin each around its
// anchor (A__prop1 to A__prop4). A__hook is where a package hangs; A__cam is the camera's eye.
function makeDrone(K, root, extra) {
  const body = K.group('drone', root);
  K.box(body, 0.34, 0.12, 0.46, 0xf2760c, 0, 0.36, 0); K.box(body, 0.26, 0.07, 0.3, DARK, 0, 0.45, 0.02); K.box(body, 0.3, 0.1, 0.1, DARK, 0, 0.36, -0.26);
  // red lights at the nose, green at the tail, as on most drones: they tell the pilot which way it faces
  K.box(body, 0.05, 0.03, 0.03, RED, -0.1, 0.37, -0.315); K.box(body, 0.05, 0.03, 0.03, RED, 0.1, 0.37, -0.315); K.box(body, 0.05, 0.03, 0.03, GREEN, -0.1, 0.37, 0.235); K.box(body, 0.05, 0.03, 0.03, GREEN, 0.1, 0.37, 0.235);
  const arms = [[-1, -1], [1, -1], [1, 1], [-1, 1]];
  arms.forEach(([sx, sz], i) => {
    const ax = sx * 0.39, az = sz * 0.39;
    K.box(body, 0.6, 0.045, 0.06, DARK, ax / 2, 0.38, az / 2, 0, -Math.atan2(az, ax), 0);
    K.cyl(body, 0.045, 0.05, 0.1, STEEL, ax, 0.4, az, 8);
    const prop = K.group('prop' + (i + 1), root);
    K.box(prop, 0.5, 0.012, 0.045, WHITE, ax, 0.46, az, 0, i * 0.7, 0); K.cyl(prop, 0.025, 0.025, 0.03, DARK, ax, 0.465, az, 6);
    extra.push(...anchor('prop' + (i + 1), ax, 0.46, az));
  });
  for (const sx of [-1, 1]) {
    K.box(body, 0.03, 0.03, 0.6, DARK, sx * 0.2, 0.015, 0);
    for (const dz of [-0.18, 0.18]) K.box(body, 0.025, 0.31, 0.025, DARK, sx * 0.185, 0.165, dz, 0, 0, sx * 0.1);
  }
  // the camera on its mount, and the hooks
  K.box(body, 0.08, 0.08, 0.05, STEEL, 0, 0.27, -0.2); K.ball(body, 0.055, DARK, 0, 0.2, -0.22, 1); K.cyl(body, 0.025, 0.025, 0.03, 0x6fa8dc, 0, 0.2, -0.275, 8, Math.PI / 2);
  for (const dx of [-0.1, 0, 0.1]) K.box(body, 0.02, 0.07, 0.02, STEEL, dx, 0.27, 0.06);
  extra.push(...anchor('hook', 0, 0.22, 0.06), ...anchor('cam', 0, 0.2, -0.3, 0, 0, -1));
}

// A package, about 35 cm across, with a loop on top for the hook. Each kind has its own shape and color, so
// they can be told apart on the shelf and from the air.
function makeSupply(K, root, id) {
  const g = K.group('supply_' + id, root);
  const kind = P.SUPPLIES.find((s) => s.id === id), c = kind.color;
  const loop = (y) => K.ring(g, 0.05, 0.012, STEEL, 0, y, 0, 4, 10, 0, 0, 0);
  switch (id) {
    case 'water': // a jerrycan
      K.box(g, 0.24, 0.32, 0.16, c, 0, 0.16, 0); K.box(g, 0.1, 0.04, 0.03, c, -0.02, 0.35, 0); K.cyl(g, 0.03, 0.03, 0.05, WHITE, 0.08, 0.34, 0, 8); K.box(g, 0.2, 0.1, 0.005, WHITE, 0, 0.18, 0.083); loop(0.41); break;
    case 'food': // a box of food packs
      K.box(g, 0.34, 0.24, 0.26, c, 0, 0.12, 0); K.box(g, 0.35, 0.03, 0.06, WHITE, 0, 0.24, 0); K.box(g, 0.2, 0.12, 0.005, WHITE, 0, 0.12, 0.133); loop(0.3); break;
    case 'medicine': // a small cool box with a cross
      K.box(g, 0.24, 0.18, 0.18, WHITE, 0, 0.09, 0); K.box(g, 0.25, 0.04, 0.19, c, 0, 0.2, 0); K.box(g, 0.1, 0.03, 0.005, c, 0, 0.1, 0.093); K.box(g, 0.03, 0.1, 0.005, c, 0, 0.1, 0.093); loop(0.27); break;
    case 'firstaid': // a soft bag
      K.box(g, 0.32, 0.2, 0.2, c, 0, 0.1, 0); K.box(g, 0.33, 0.03, 0.21, 0x0e6a43, 0, 0.19, 0); K.box(g, 0.1, 0.03, 0.005, WHITE, 0, 0.1, 0.103); K.box(g, 0.03, 0.1, 0.005, WHITE, 0, 0.1, 0.103); loop(0.26); break;
    case 'radio': // a hand radio in its case
      K.box(g, 0.2, 0.14, 0.16, c, 0, 0.07, 0); K.box(g, 0.07, 0.16, 0.04, DARK, -0.03, 0.2, 0); K.cyl(g, 0.006, 0.006, 0.16, DARK, -0.05, 0.36, 0, 4); K.box(g, 0.04, 0.03, 0.005, 0x9fe89f, -0.03, 0.23, 0.023); loop(0.2).position.x = 0.06; break;
    case 'ring': // a life ring with its rope
      K.ring(g, 0.24, 0.06, c, 0, 0.06, 0, 6, 16); for (const a of [0, 1.57, 3.14, 4.71]) K.box(g, 0.13, 0.13, 0.07, WHITE, Math.cos(a) * 0.24, 0.06, Math.sin(a) * 0.24, 0, -a, 0); loop(0.16); break;
  }
}

// A person standing with one arm raised, waving for help.
function makePerson(K, root, name, shirt, skin, pants, sitting) {
  const g = K.group(name, root), hip = sitting ? 0.45 : 0.82;
  if (sitting) { K.box(g, 0.3, 0.14, 0.42, pants, 0, 0.43, -0.2); K.box(g, 0.3, 0.42, 0.14, pants, 0, 0.21, -0.38); }
  else for (const sx of [-1, 1]) K.box(g, 0.13, 0.8, 0.15, pants, sx * 0.085, 0.4, 0);
  K.box(g, 0.34, 0.5, 0.2, shirt, 0, hip + 0.27, 0);
  K.ball(g, 0.13, skin, 0, hip + 0.68, 0, 1); K.box(g, 0.24, 0.08, 0.24, DARK, 0, hip + 0.79, 0.01);
  K.box(g, 0.09, 0.48, 0.1, skin, -0.23, hip + 0.3, 0, 0, 0, -0.12);            // one arm down
  K.box(g, 0.09, 0.5, 0.1, skin, 0.27, hip + 0.72, 0, 0, 0, -0.45);             // one arm up
  K.box(g, 0.12, 0.2, 0.12, shirt, 0.21, hip + 0.46, 0, 0, 0, -0.45);
}

// The rescue boat: an orange rubber boat with two rescuers aboard and a motor behind.
function makeBoat(K, root) {
  const g = K.group('boat', root);
  K.box(g, 1.5, 0.22, 3.6, 0x4a4f55, 0, 0.16, 0);
  for (const sx of [-1, 1]) K.cyl(g, 0.24, 0.24, 3.8, ORANGE, sx * 0.78, 0.36, 0.05, 8, Math.PI / 2);
  K.cyl(g, 0.24, 0.24, 1.6, ORANGE, 0, 0.36, -1.85, 8, 0, 0, Math.PI / 2); K.ball(g, 0.3, ORANGE, -0.72, 0.36, -1.82, 1); K.ball(g, 0.3, ORANGE, 0.72, 0.36, -1.82, 1);
  K.box(g, 1.3, 0.06, 0.3, WHITE, 0, 0.5, 0.4); K.box(g, 0.24, 0.5, 0.22, DARK, 0, 0.6, 1.95); K.box(g, 0.1, 0.5, 0.1, DARK, 0, 0.1, 2.05);
  for (const [x, z] of [[-0.25, -0.5], [0.25, 0.9]]) {
    K.box(g, 0.3, 0.42, 0.2, ORANGE, x, 0.78, z); K.ball(g, 0.12, SKIN[1], x, 1.13, z, 1); K.box(g, 0.24, 0.08, 0.24, WHITE, x, 1.22, z); K.box(g, 0.28, 0.14, 0.34, NAVY, x, 0.5, z + 0.05);
  }
  K.box(g, 0.5, 0.3, 0.02, WHITE, 0, 0.75, -1.3); K.box(g, 0.3, 0.07, 0.025, RED, 0, 0.75, -1.3); K.box(g, 0.07, 0.24, 0.025, RED, 0, 0.75, -1.3);
}

// A ring of the flight school: 5 m in radius, standing upright, to fly through from north or south.
function makeGate(K, root) {
  const g = K.group('gate', root), r = P.GATES[0].r;
  K.ring(g, r, 0.22, WHITE, 0, 0, 0, 5, 20, 0, 0, 0);
}

export function buildProps(T) {
  const K = makeKit(T);
  const mats = new Map();
  const material = (col) => { let m = mats.get(col); if (!m) { m = new T.MeshStandardMaterial({ color: col, roughness: 1, metalness: 0 }); mats.set(col, m); } return m; };
  const out = {};
  const one = (name, build) => {
    const root = new T.Group(), extra = [];
    build(root, extra);
    out[name] = { meshes: merge(flatten(root, { material, group: groupOf, texName: () => null })), extra: extra.concat(calibration()) };
  };
  one('drone', (root, extra) => makeDrone(K, root, extra));
  for (const s of P.SUPPLIES) one('supply_' + s.id, (root) => makeSupply(K, root, s.id));
  PEOPLE.forEach(([name, shirt, skin, pants, sitting]) => one(name, (root) => makePerson(K, root, name, shirt, skin, pants, sitting)));
  one('boat', (root) => makeBoat(K, root));
  one('gate', (root) => makeGate(K, root));
  return out;
}

// The people: the model's name, the shirt (bright, to be seen from the air), the skin, the trousers, and
// whether they sit. Unity gives each group of survivors a few of them.
export const PEOPLE = [
  ['person_red', 0xe2382b, SKIN[0], 0x35506b, false],
  ['person_yellow', 0xf2b300, SKIN[1], 0x3d3127, false],
  ['person_blue', 0x2f74d0, SKIN[2], 0x4a3b6b, false],
  ['person_pink', 0xe2588a, SKIN[3], 0x35506b, false],
  ['person_sit', 0x4caf50, SKIN[1], 0x2f3f4f, true],
];
