// Builds the riverside barangay from its plan (plan.js): the ground, the streets, the houses and the places
// with a name, the bridge, the trees and the poles, and the pieces each mission's scene adds or takes away.
// Everything is low-poly and flat-colored, in DRONE VR's coordinates (metres; x east, y up, z south).
//
// The scene sets (Unity switches them; see SETS in plan.js):
//   normal      the houses that fall in the earthquake, standing; the water tower upright; the wire up
//   quake       their rubble, the fire, the fallen wire, the cracked bridge, the landslide, the fuel spill,
//               the leaning water tower, the debris that dams the creek
//   flood       the stranded jeepney, drowned cars, the platform under the trees
//   wind        trees across the streets, roofing sheets
//   bridge_ok   the middle of the bridge;   bridge_out   the middle of the bridge, fallen

import * as P from './plan.js';
import { makeKit, seeded } from './kit.js';
import { calibration, flatten, merge, groupOf } from './tag.js';

const GRASS = [0x8fb56a, 0x97b96e, 0x9dbb72, 0xa5bf78, 0xabc17c, 0xb3c383];
const MUD = 0x7a6a52, BED = 0x6b5e4b, SAND = 0xcdbb8a, ASPHALT = 0x5b5e63, DIRT = 0x9a8866, CONCRETE = 0xbfc3c7, DARK = 0x2c2f33;
const WOOD = 0x8a6a45, TRUNK = 0x6f5a3c, LEAF = [0x3f8f4a, 0x4a9a52, 0x57a35a], PALM = [0x4f9a4a, 0x5aa552];
const WINDOW = 0x6f8fa8, DOOR = 0x5a4632, STEEL = 0x8f979e, RED = 0xc62f22, WHITE = 0xf4f4f0, YELLOW = 0xffc53d;

export function buildTown(T) {
  const K = makeKit(T);
  const root = new T.Group();
  const textures = new Map();
  const rnd = seeded(5150);
  const G = P.ground;
  const g = {};
  for (const name of ['terrain', 'roads', 'buildings', 'trees', 'props']) g[name] = K.group(name, root);
  for (const name of Object.keys(P.SETS)) g[name] = K.group('SET:' + name, root);

  terrain(T, K, g.terrain);
  far(K, g.terrain);
  roads(T, K, g.roads, g.props);
  for (const b of P.BUILDINGS) building(K, b.set === 'normal' ? g.normal : g.buildings, b, rnd);
  named(K, g, textures);
  bridge(T, K, g);
  pad(K, g.props);
  for (const t of P.TREES) tree(K, g.trees, t, rnd);
  poles(K, g);
  waterTower(K, g.normal, 0);
  waterTower(K, g.quake, 0.17);
  cellTower(K, g.props);
  quake(K, g.quake, rnd);
  flood(K, g.flood);
  wind(K, g.wind, rnd);

  const mats = new Map();
  const material = (col) => { let m = mats.get(col); if (!m) { m = new T.MeshStandardMaterial({ color: col, roughness: 1, metalness: 0 }); mats.set(col, m); } return m; };
  const meshes = merge(flatten(root, { texName: (tex, mesh) => mesh.userData.texName, material, group: groupOf }));
  return { meshes, extra: calibration(), textures };
}

// ---------- the ground ----------

// The ground as a sheet of small flat faces, finer along the creek and the river's banks. Each face takes the
// color of what it is: the river's bed, a bank, the creek, or grass that pales as the land rises.
function terrain(T, K, group) {
  const Tn = P.TERRAIN, W = P.WORLD;
  const xs = new Set(), zs = new Set();
  for (let x = W.x0; x <= W.x1; x += 5) xs.add(x);
  for (let z = W.z0; z <= W.z1; z += 5) zs.add(z);
  [-Tn.creekHalf, -Tn.creekHalf + 1, Tn.creekHalf - 1, Tn.creekHalf].forEach((d) => xs.add(Tn.creekX + d));
  [Tn.riverNorth, Tn.riverNorth + Tn.riverBank, Tn.riverSouth - Tn.riverBank, Tn.riverSouth].forEach((z) => zs.add(z));
  const X = [...xs].sort((a, b) => a - b), Z = [...zs].sort((a, b) => a - b);
  const buckets = new Map();
  const put = (color, pts) => { if (!buckets.has(color)) buckets.set(color, []); buckets.get(color).push(...pts); };
  for (let j = 0; j + 1 < Z.length; j++) {
    for (let i = 0; i + 1 < X.length; i++) {
      const x0 = X[i], x1 = X[i + 1], z0 = Z[j], z1 = Z[j + 1], cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, h = P.ground(cx, cz);
      let color;
      if (cz > Tn.riverNorth && cz < Tn.riverSouth) color = h < -0.4 ? BED : SAND;
      else if (cz < Tn.riverNorth && Math.abs(cx - Tn.creekX) < Tn.creekHalf) color = MUD;
      else {
        // paler as the land rises, with a little unevenness from place to place
        const band = Math.max(0, Math.min(GRASS.length - 1, Math.floor(h / 1.1 + (Math.sin(cx * 0.31) * Math.cos(cz * 0.27) > 0.2 ? 1 : 0))));
        color = GRASS[band];
      }
      const a = [x0, P.ground(x0, z0), z0], b = [x1, P.ground(x1, z0), z0], c = [x1, P.ground(x1, z1), z1], d = [x0, P.ground(x0, z1), z1];
      put(color, [...a, ...d, ...c, ...a, ...c, ...b]);
    }
  }
  for (const [color, pts] of buckets) {
    const geo = new T.BufferGeometry();
    geo.setAttribute('position', new T.Float32BufferAttribute(pts, 3));
    geo.computeVertexNormals();
    group.add(new T.Mesh(geo, K.lam(color)));
  }
}

// What lies beyond the barangay, so the horizon is not empty: flat land a little above the river's normal
// level (it goes under when the flood rises), the river going on to the west and the east, and far hills.
function far(K, group) {
  const W = P.WORLD, Tn = P.TERRAIN, R = 900, y = 0.4;
  K.slab(group, -R, W.x0, y - 0.5, y, -R, R, GRASS[1]); K.slab(group, W.x1, R, y - 0.5, y, -R, R, GRASS[1]);
  K.slab(group, W.x0, W.x1, y - 0.5, y, -R, W.z0, GRASS[2]); K.slab(group, W.x0, W.x1, y - 0.5, y, W.z1, R, GRASS[1]);
  K.slab(group, -R, W.x0, y, y + 0.03, Tn.riverNorth, Tn.riverSouth, 0x3f7fb5); K.slab(group, W.x1, R, y, y + 0.03, Tn.riverNorth, Tn.riverSouth, 0x3f7fb5);
  for (let k = 0; k < 14; k++) {
    const a = k / 14 * Math.PI * 2 + 0.2, r = 760 + (k % 3) * 60, h = 70 + ((k * 37) % 5) * 22;
    K.cyl(group, 0, 150 + (k % 4) * 30, h, k % 2 ? 0x7fa6a0 : 0x8db0a6, Math.cos(a) * r, h / 2, Math.sin(a) * r, 6);
  }
}

// The height of a street at a point: the ground, without the cut of the creek (a street crosses it level),
// and climbing to the deck of the bridge at its two ends.
function roadY(x, z) {
  const Tn = P.TERRAIN, B = P.BRIDGE;
  const cut = Tn.creekDepth * Math.max(0, Math.min(1, Tn.creekHalf - Math.abs(x - Tn.creekX)));
  let y = P.ground(x, z) + (z < Tn.riverNorth ? cut : 0);
  if (Math.abs(x - B.x) <= B.w / 2 + 1) {
    if (z > 82 && z <= B.z0) y = Math.max(y, P.ground(x, 82) + (B.deck - P.ground(x, 82)) * (z - 82) / (B.z0 - 82));
    if (z >= B.z1 && z < 158) y = Math.max(y, B.deck + (P.ground(x, 158) - B.deck) * (z - B.z1) / (158 - B.z1));
  }
  return y;
}

function roads(T, K, group, props) {
  const Tn = P.TERRAIN;
  for (const r of P.ROADS) {
    const len = Math.hypot(r.x1 - r.x0, r.z1 - r.z0), n = Math.ceil(len / 5);
    const ux = (r.x1 - r.x0) / len, uz = (r.z1 - r.z0) / len, px = -uz * r.w / 2, pz = ux * r.w / 2;
    const pts = [], side = [];
    for (let k = 0; k < n; k++) {
      const a = k / n * len, b = (k + 1) / n * len;
      const ax = r.x0 + ux * a, az = r.z0 + uz * a, bx = r.x0 + ux * b, bz = r.z0 + uz * b;
      const ya = roadY(ax, az) + 0.07, yb = roadY(bx, bz) + 0.07;
      const p = [[ax - px, ya, az - pz], [ax + px, ya, az + pz], [bx + px, yb, bz + pz], [bx - px, yb, bz - pz]];
      pts.push(...p[0], ...p[1], ...p[2], ...p[0], ...p[2], ...p[3]);
      // where the street stands above the ground (a ramp to the bridge, a crossing of the creek): its sides
      const ga = Math.min(P.ground(ax - px, az - pz), P.ground(ax + px, az + pz)), gb = Math.min(P.ground(bx - px, bz - pz), P.ground(bx + px, bz + pz));
      if (ya - ga > 0.3 || yb - gb > 0.3) {
        for (const s of [-1, 1]) {
          const q = [[ax + s * px, ya, az + s * pz], [bx + s * px, yb, bz + s * pz], [bx + s * px, gb - 0.2, bz + s * pz], [ax + s * px, ga - 0.2, az + s * pz]];
          if (s > 0) side.push(...q[0], ...q[2], ...q[1], ...q[0], ...q[3], ...q[2]); else side.push(...q[0], ...q[1], ...q[2], ...q[0], ...q[2], ...q[3]);
        }
      }
    }
    const geo = new T.BufferGeometry();
    geo.setAttribute('position', new T.Float32BufferAttribute(pts, 3));
    group.add(new T.Mesh(geo, K.lam(r.id.startsWith('sitio') ? DIRT : ASPHALT)));
    if (side.length) {
      const sg = new T.BufferGeometry();
      sg.setAttribute('position', new T.Float32BufferAttribute(side, 3));
      props.add(new T.Mesh(sg, K.lam(0xa9a79d)));
    }
    // a culvert under each street that crosses the creek
    if (r.z0 === r.z1 && r.x0 < Tn.creekX && r.x1 > Tn.creekX && r.z0 < Tn.riverNorth) {
      const y = roadY(Tn.creekX, r.z0);
      K.slab(props, Tn.creekX - 5.5, Tn.creekX - 4, y - 2, y + 0.05, r.z0 - r.w / 2 - 0.4, r.z0 + r.w / 2 + 0.4, CONCRETE);
      K.slab(props, Tn.creekX + 4, Tn.creekX + 5.5, y - 2, y + 0.05, r.z0 - r.w / 2 - 0.4, r.z0 + r.w / 2 + 0.4, CONCRETE);
      K.slab(props, Tn.creekX - 4, Tn.creekX + 4, y - 0.45, y + 0.04, r.z0 - r.w / 2 - 0.4, r.z0 + r.w / 2 + 0.4, CONCRETE);
      for (const s of [-1, 1]) K.slab(props, Tn.creekX - 5.5, Tn.creekX + 5.5, y + 0.05, y + 0.75, r.z0 + s * (r.w / 2 + 0.3) - 0.1, r.z0 + s * (r.w / 2 + 0.3) + 0.1, WHITE);
    }
  }
  // the dashes down the middle of the main street
  const main = P.ROADS.find((r) => r.id === 'main');
  for (let x = main.x0 + 4; x < main.x1 - 4; x += 8) {
    if (Math.abs(x + 1.5 - Tn.creekX) < 7) continue;
    K.box(group, 3, 0.02, 0.22, 0xe8c84a, x + 1.5, roadY(x + 1.5, main.z0) + 0.09, main.z0);
  }
}

// ---------- buildings ----------

function building(K, group, b, rnd) {
  const x0 = b.x - b.w / 2, x1 = b.x + b.w / 2, z0 = b.z - b.d / 2, z1 = b.z + b.d / 2, top = b.base + b.h;
  if (b.roof === 'open') {
    // a roof on posts over a concrete floor
    K.slab(group, x0, x1, b.base - 1.5, b.base, z0, z1, CONCRETE);
    const nx = b.w > 16 ? 3 : 2, nz = b.d > 16 ? 3 : 2;
    for (let i = 0; i < nx; i++) for (let j = 0; j < nz; j++) {
      if (nx === 3 && nz === 3 && i === 1 && j === 1) continue;
      K.box(group, 0.4, b.h, 0.4, b.wall, x0 + 0.4 + i * (b.w - 0.8) / (nx - 1), b.base + b.h / 2, z0 + 0.4 + j * (b.d - 0.8) / (nz - 1));
    }
    K.slab(group, x0 - 0.6, x1 + 0.6, top, top + b.roofH * 0.35, z0 - 0.6, z1 + 0.6, b.top);
    K.gable(group, b.x, top + b.roofH * 0.35, b.z, b.w, b.d, b.roofH * 0.65, b.top, 0.6);
    return;
  }
  K.slab(group, x0, x1, b.base - 1.5, top, z0, z1, b.wall);
  if (b.roof === 'gable') K.gable(group, b.x, top, b.z, b.w, b.d, b.roofH, b.top, 0.5);
  else {
    K.slab(group, x0 - 0.25, x1 + 0.25, top, top + b.roofH * 0.5, z0 - 0.25, z1 + 0.25, b.top);
    // a low wall around a flat roof, as high as the roof is counted
    for (const [ax, bx, az, bz] of [[x0 - 0.25, x1 + 0.25, z0 - 0.25, z0], [x0 - 0.25, x1 + 0.25, z1, z1 + 0.25], [x0 - 0.25, x0, z0, z1], [x1, x1 + 0.25, z0, z1]])
      K.slab(group, ax, bx, top + b.roofH * 0.5, top + b.roofH, az, bz, b.wall);
  }
  // a door and windows, on the sides that are wide enough
  const storeys = b.h > 5 ? 2 : 1;
  K.box(group, 1, 2, 0.08, DOOR, b.x + (rnd() - 0.5) * (b.w - 3), b.base + 1, z1 + 0.03);
  for (let s = 0; s < storeys; s++) {
    const y = b.base + 1.6 + s * 2.8;
    for (let k = 0; k < Math.floor(b.w / 3.4); k++) {
      const x = x0 + 1.7 + k * 3.4;
      K.box(group, 1.1, 1, 0.06, WINDOW, x, y, z0 - 0.02);
      if (s > 0 || Math.abs(x - b.x) > 1.6) K.box(group, 1.1, 1, 0.06, WINDOW, x, y, z1 + 0.02);
    }
    for (let k = 0; k < Math.floor(b.d / 3.6); k++) {
      const z = z0 + 1.8 + k * 3.6;
      K.box(group, 0.06, 1, 1.1, WINDOW, x0 - 0.02, y, z); K.box(group, 0.06, 1, 1.1, WINDOW, x1 + 0.02, y, z);
    }
  }
}

// What tells the places with a name apart from the air: their signs, and something on or beside each.
function named(K, g, textures) {
  const B = (id) => P.building(id), props = g.props, top = (b) => b.base + b.h;
  const navy = '#16303f', cream = '#fff5df', gold = '#ffc53d';
  const hall = B('hall');
  K.sign(props, textures, 'sign_hall', K.board(1024, 384, navy, cream, 'BARANGAY HALL', ['Disaster Operations Center · Drone Pad'], { border: gold }), 8, 3, hall.x + hall.w / 2 + 0.06, hall.base + 4.6, hall.z, Math.PI / 2);
  K.cyl(props, 0.06, 0.08, 9, STEEL, hall.x + hall.w / 2 + 1.5, hall.base + 4.5, hall.z + 5, 6);
  K.box(props, 1.4, 0.9, 0.04, 0x0038a8, hall.x + hall.w / 2 + 2.25, hall.base + 8.5, hall.z + 5); K.box(props, 1.4, 0.45, 0.05, 0xce1126, hall.x + hall.w / 2 + 2.25, hall.base + 8.28, hall.z + 5);
  const school = B('school');
  K.sign(props, textures, 'sign_evac', K.board(1024, 256, '#128253', '#ffffff', 'EVACUATION CENTER', ['Paaralang Elementarya'], { border: '#ffffff' }), 12, 3, school.x, top(school) - 1.2, school.z + school.d / 2 + 0.06, 0);
  // the mark to drop supplies on, in the school's yard
  const evac = P.SITES.find((s) => s.id === 'evac');
  dropMark(K, props, evac.x, P.ground(evac.x, evac.z), evac.z, evac.r);
  const health = B('health');
  K.sign(props, textures, 'sign_health', K.board(768, 256, '#ffffff', '#c62f22', 'HEALTH CENTER', [], { border: '#c62f22' }), 7.5, 2.5, health.x, top(health) - 1.1, health.z + health.d / 2 + 0.06, 0);
  K.box(props, 3, 0.06, 1, RED, health.x, top(health) + health.roofH * 0.5 + 0.04, health.z); K.box(props, 1, 0.06, 3, RED, health.x, top(health) + health.roofH * 0.5 + 0.045, health.z);
  const hs = P.SITES.find((s) => s.id === 'health');
  dropMark(K, props, hs.x, P.ground(hs.x, hs.z), hs.z, hs.r);
  const market = B('market');
  K.sign(props, textures, 'sign_market', K.board(768, 256, '#d9650a', '#ffffff', 'PALENGKE', [], {}), 9, 3, market.x, top(market) - 1, market.z - market.d / 2 - 0.06, Math.PI);
  for (let k = 0; k < 5; k++) K.box(props, 5, 0.12, 2.4, k % 2 ? 0x2f74d0 : 0xf2b300, market.x - 12 + k * 6, market.base + 3, market.z + market.d / 2 + 1.1, 0.25, 0, 0);
  const store = B('store'), canopy = B('canopy');
  K.sign(props, textures, 'sign_fuel', K.board(256, 512, '#d43b2f', '#ffffff', 'GAS', [], { titleSize: 110 }), 1.6, 3.2, canopy.x + canopy.w / 2 + 2.5, canopy.base + 5.2, canopy.z + 3, 0);
  K.cyl(props, 0.12, 0.12, 3.6, STEEL, canopy.x + canopy.w / 2 + 2.5, canopy.base + 1.8, canopy.z + 3, 6);
  for (const dx of [-3, 3]) { K.box(props, 0.7, 1.5, 0.5, RED, canopy.x + dx, canopy.base + 0.75, canopy.z); K.box(props, 0.72, 0.4, 0.52, WHITE, canopy.x + dx, canopy.base + 1.2, canopy.z); }
  K.box(props, 3, 0.4, 0.06, WHITE, store.x, top(store) + 0.4, store.z + store.d / 2 + 0.04);
  const chapel = B('chapel'), belfry = B('belfry'), kapilya = B('kapilya');
  for (const [b, s] of [[belfry, 1], [kapilya, 0.7]]) {
    const y = b.base + b.h + b.roofH;
    K.box(props, 0.25 * s, 2.4 * s, 0.25 * s, WHITE, b.x, y + 1.2 * s, b.z); K.box(props, 1.3 * s, 0.25 * s, 0.25 * s, WHITE, b.x, y + 1.6 * s, b.z);
  }
  K.box(props, 2.2, 3.2, 0.1, DOOR, chapel.x, chapel.base + 1.6, chapel.z + chapel.d / 2 + 0.04);
  const mill = B('mill');
  K.cyl(props, 3, 3, 9, STEEL, mill.x - mill.w / 2 - 4, mill.base + 4.5, mill.z, 10); K.cyl(props, 0, 3.2, 2, 0x6f777e, mill.x - mill.w / 2 - 4, mill.base + 10, mill.z, 10);
  // the benches of the covered court by the gym, where people shelter in the flood
  const gym = B('gym');
  for (let k = 0; k < 4; k++) K.slab(props, gym.x - 7, gym.x + 7, gym.base, gym.base + 0.6 * (k + 1), gym.z + 5 + k * 1.2, gym.z + 6.2 + k * 1.2, k % 2 ? 0x9aa3ab : 0xb7bfc6);
  // the sitio's mark, and the mark on the roof of the house by the river
  const sitio = P.SITES.find((s) => s.id === 'sitio'), roof = P.SITES.find((s) => s.id === 'roof');
  dropMark(K, props, sitio.x, P.ground(sitio.x, sitio.z), sitio.z, sitio.r);
  dropMark(K, props, roof.x, P.ground(roof.x, roof.z) + roof.y, roof.z, roof.r);
  // the landings where the rescue boats wait
  for (const s of P.STAGING) {
    const y = P.ground(s.x, s.z);
    K.slab(props, s.x - 4, s.x + 4, y - 0.3, y + 0.25, s.z + 3.2, s.z + 5.2, WOOD);
    for (const dx of [-3.6, 3.6]) K.cyl(props, 0.14, 0.14, 2.2, TRUNK, s.x + dx, y + 0.8, s.z + 4.2, 6);
    K.box(props, 0.1, 2.6, 0.1, STEEL, s.x + 3.6, y + 2.1, s.z + 4.2); K.box(props, 1.2, 0.7, 0.05, 0xff7a1a, s.x + 3.0, y + 3.0, s.z + 4.2);
  }
}

// A mark on the ground to drop on: a ring and a cross.
function dropMark(K, group, x, y, z, r) {
  K.ring(group, r, 0.12, 0xff7a1a, x, y + 0.12, z, 4, 24);
  K.box(group, r * 1.2, 0.05, 0.3, WHITE, x, y + 0.1, z); K.box(group, 0.3, 0.05, r * 1.2, WHITE, x, y + 0.1, z);
}

function bridge(T, K, g) {
  const B = P.BRIDGE, x0 = B.x - B.w / 2, x1 = B.x + B.w / 2, y0 = B.deck - B.thick, deck = 0x9a9d9f;
  const span = (group, za, zb) => {
    K.slab(group, x0, x1, y0, B.deck, za, zb, deck);
    for (const x of [x0 + 0.15, x1 - 0.15]) { K.slab(group, x - 0.08, x + 0.08, B.deck + 0.9, B.deck + 1.0, za, zb, WHITE); for (let z = za + 0.5; z < zb; z += 2) K.box(group, 0.12, 1, 0.12, WHITE, x, B.deck + 0.5, z); }
  };
  span(g.props, B.z0, B.spanZ0); span(g.props, B.spanZ1, B.z1); span(g.bridge_ok, B.spanZ0, B.spanZ1);
  for (const z of [B.spanZ0, B.spanZ1]) K.slab(g.props, x0 + 0.6, x1 - 0.6, -3, y0, z - 1, z + 1, 0x85888a);
  for (const z of [B.z0 + 0.6, B.z1 - 0.6]) K.slab(g.props, x0 - 0.4, x1 + 0.4, -1, y0, z - 0.8, z + 0.8, 0x85888a);
  // the middle, fallen: its two halves hang from the piers into the river
  const half = (B.spanZ1 - B.spanZ0) / 2;
  for (const s of [-1, 1]) {
    const hinge = s < 0 ? B.spanZ0 : B.spanZ1;
    const part = new T.Group(); part.position.set(B.x, B.deck - B.thick / 2, hinge); part.rotation.x = s * 0.62; g.bridge_out.add(part);
    K.box(part, B.w, B.thick, half - 0.4, deck, 0, 0, -s * (half - 0.4) / 2);
    K.box(part, 0.16, 0.1, half - 0.4, WHITE, B.w / 2 - 0.15, 1.2, -s * (half - 0.4) / 2);
  }
  K.box(g.bridge_out, B.w * 0.7, 0.5, 3, 0x85888a, B.x + 0.6, -1.4, (B.spanZ0 + B.spanZ1) / 2, 0.2, 0.3, 0.1);
  for (const z of [B.spanZ0 - 2.5, B.spanZ1 + 2.5]) for (const dx of [-2.4, 0, 2.4]) K.box(g.bridge_out, 0.9, 0.9, 0.5, 0xff7a1a, B.x + dx, B.deck + 0.45, z);
}

function pad(K, group) {
  const p = P.PAD, y = P.ground(p.x, p.z);
  K.slab(group, p.x - p.size / 2, p.x + p.size / 2, y - 0.8, y + 0.2, p.z - p.size / 2, p.z + p.size / 2, CONCRETE);
  K.ring(group, 3, 0.1, YELLOW, p.x, y + 0.22, p.z, 4, 24);
  K.box(group, 0.35, 0.03, 2.6, YELLOW, p.x - 1, y + 0.215, p.z); K.box(group, 0.35, 0.03, 2.6, YELLOW, p.x + 1, y + 0.215, p.z); K.box(group, 2, 0.03, 0.35, YELLOW, p.x, y + 0.215, p.z);
  for (const [dx, dz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) K.box(group, 0.25, 0.25, 0.25, RED, p.x + dx * (p.size / 2 - 0.2), y + 0.32, p.z + dz * (p.size / 2 - 0.2));
  // a wind sock on a mast, north of the pad
  K.cyl(group, 0.05, 0.07, 6, STEEL, p.x + 6, y + 3, p.z - 6, 6); K.cyl(group, 0.12, 0.3, 1.6, 0xff7a1a, p.x + 6.8, y + 5.8, p.z - 6, 6, 0, 0, Math.PI / 2);
}

// ---------- trees, poles, towers ----------

function tree(K, group, t, rnd) {
  const y = P.ground(t.x, t.z);
  if (t.kind === 'palm') {
    const lean = (rnd() - 0.5) * 0.16, dir = rnd() * Math.PI * 2;
    const hold = new K.T.Group(); hold.position.set(t.x, y, t.z); hold.rotation.set(Math.cos(dir) * lean, 0, Math.sin(dir) * lean); group.add(hold);
    K.cyl(hold, 0.16, 0.26, t.h, TRUNK, 0, t.h / 2, 0, 6);
    for (let k = 0; k < 7; k++) {
      const a = k / 7 * Math.PI * 2 + dir;
      const leaf = new K.T.Group(); leaf.position.set(0, t.h, 0); leaf.rotation.set(0, -a, 0); hold.add(leaf);
      K.box(leaf, t.r, 0.07, 0.55, PALM[k % 2], t.r * 0.45, -0.25, 0, 0, 0, -0.5);
    }
    K.ball(hold, 0.22, 0x6f5a3c, 0.15, t.h - 0.3, 0.1); K.ball(hold, 0.22, 0x7a6644, -0.12, t.h - 0.35, -0.12);
  } else {
    K.cyl(group, 0.22, 0.3, t.h * 0.6, TRUNK, t.x, y + t.h * 0.3, t.z, 6);
    K.ball(group, t.r, LEAF[Math.floor(rnd() * 3)], t.x, y + t.h - t.r * 0.5, t.z, 0);
    K.ball(group, t.r * 0.65, LEAF[Math.floor(rnd() * 3)], t.x + t.r * 0.5, y + t.h - t.r * 0.2, t.z + t.r * 0.3, 0);
  }
}

function poles(K, g) {
  const down = (a, b) => (a.x === 135 && b.x === 165);
  P.POLES.forEach((p, i) => {
    const y = P.ground(p.x, p.z);
    K.cyl(g.props, 0.13, 0.17, p.h, 0x5a5048, p.x, y + p.h / 2, p.z, 6);
    K.box(g.props, 0.12, 0.12, 1.7, 0x5a5048, p.x, y + p.h - 0.5, p.z);
    const q = P.POLES[i + 1];
    if (!q) return;
    const len = q.x - p.x, ya = y + p.h - 0.45, yb = P.ground(q.x, q.z) + q.h - 0.45;
    for (const dz of [-0.7, 0.7]) {
      const group = down(p, q) ? g.normal : g.props;
      K.box(group, Math.hypot(len, yb - ya), 0.05, 0.05, DARK, (p.x + q.x) / 2, (ya + yb) / 2 - 0.25, p.z + dz, 0, 0, Math.atan2(yb - ya, len));
    }
    if (down(p, q)) {
      // after the earthquake one wire lies on the street, the other hangs from its pole
      const mid = (p.x + q.x) / 2, gy = roadY(mid, p.z + 4) + 0.12;
      K.box(g.quake, Math.hypot(15, ya - gy), 0.07, 0.07, DARK, p.x + 7.5, (ya + gy) / 2, p.z + 2.3, 0.3, 0, -Math.atan2(ya - gy, 15));
      K.box(g.quake, 15, 0.07, 0.07, DARK, mid + 7.5, gy, p.z + 5.2, 0, 0.12, 0);
      K.box(g.quake, Math.hypot(6, 3), 0.07, 0.07, DARK, q.x - 3, yb - 1.6, q.z - 0.7, 0, 0, Math.atan2(3, 6));
      for (const [dx, dz] of [[0, 0], [0.5, 0.3], [-0.4, 0.2]]) K.cyl(g.quake, 0, 0.12, 0.5, YELLOW, mid + dx, gy + 0.3, p.z + 3.9 + dz, 4);
      for (const dx of [-5, 5]) K.cyl(g.quake, 0.05, 0.3, 0.8, 0xff7a1a, mid + dx, gy + 0.4, p.z + 6.5, 6);
    }
  });
}

function waterTower(K, group, lean) {
  const t = P.TOWERS.find((q) => q.id === 'watertower'), y = P.ground(t.x, t.z);
  const hold = new K.T.Group(); hold.position.set(t.x, y, t.z); hold.rotation.z = lean; group.add(hold);
  for (const [dx, dz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) K.box(hold, 0.35, 13, 0.35, STEEL, dx * 2.1, 6.5, dz * 2.1);
  for (const yb of [4, 9]) { K.box(hold, 4.6, 0.2, 0.2, STEEL, 0, yb, -2.1); K.box(hold, 4.6, 0.2, 0.2, STEEL, 0, yb, 2.1); K.box(hold, 0.2, 0.2, 4.6, STEEL, -2.1, yb, 0); K.box(hold, 0.2, 0.2, 4.6, STEEL, 2.1, yb, 0); }
  K.cyl(hold, t.r, t.r, 4.4, 0x9fc3d8, 0, 15.2, 0, 12); K.cyl(hold, 0, t.r + 0.2, 1.2, 0x6f8fa8, 0, 18, 0, 12);
  if (lean) { K.box(hold, 3, 0.1, 0.12, DARK, 0.8, 14.6, 3.25, 0, 0, 0.5); K.box(hold, 0.12, 2.2, 0.1, DARK, 0.2, 13.9, 3.25, 0, 0, -0.3); }
}

function cellTower(K, group) {
  const t = P.TOWERS.find((q) => q.id === 'celltower'), y = P.ground(t.x, t.z), n = 7, seg = t.h / n;
  for (let k = 0; k < n; k++) {
    const w0 = 2.4 - k * 0.28, w1 = 2.4 - (k + 1) * 0.28, color = k % 2 ? WHITE : RED;
    for (const [dx, dz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
      const lean = Math.atan2(w0 - w1, seg);
      K.box(group, 0.2, seg + 0.05, 0.2, color, t.x + dx * (w0 + w1) / 2, y + (k + 0.5) * seg, t.z + dz * (w0 + w1) / 2, dz * lean, 0, -dx * lean);
    }
    K.box(group, 2 * w1 + 0.2, 0.14, 0.14, color, t.x, y + (k + 1) * seg, t.z - w1); K.box(group, 2 * w1 + 0.2, 0.14, 0.14, color, t.x, y + (k + 1) * seg, t.z + w1);
    K.box(group, 0.14, 0.14, 2 * w1 + 0.2, color, t.x - w1, y + (k + 1) * seg, t.z); K.box(group, 0.14, 0.14, 2 * w1 + 0.2, color, t.x + w1, y + (k + 1) * seg, t.z);
  }
  for (const a of [0, 2.1, 4.2]) K.box(group, 0.5, 2, 0.25, WHITE, t.x + Math.cos(a) * 0.9, y + t.h - 3, t.z + Math.sin(a) * 0.9, 0, -a, 0);
  K.box(group, 0.3, 0.3, 0.3, RED, t.x, y + t.h + 0.2, t.z);
}

// ---------- the earthquake ----------

function quake(K, group, rnd) {
  // each fallen house: a heap of its own walls and roof
  for (const r of P.RUBBLE) {
    const b = P.building(r.of);
    for (let k = 0; k < 16; k++) {
      const w = 1 + rnd() * 2.6, h = 0.25 + rnd() * 0.7, d = 0.8 + rnd() * 2.2;
      K.box(group, w, h, d, k % 3 ? b.wall : b.top, r.x + (rnd() - 0.5) * (r.w - 2), r.base + 0.2 + rnd() * 1.3, r.z + (rnd() - 0.5) * (r.d - 2), (rnd() - 0.5) * 0.9, rnd() * 3, (rnd() - 0.5) * 0.9);
    }
    K.slab(group, r.x - r.w / 2 + 0.5, r.x + r.w / 2 - 0.5, r.base - 1, r.base + 0.5, r.z - r.d / 2 + 0.5, r.z + r.d / 2 - 0.5, 0x8d877b);
    for (let k = 0; k < 4; k++) K.box(group, 0.2, 0.2, 3 + rnd() * 2, WOOD, r.x + (rnd() - 0.5) * r.w, r.base + 1.2 + rnd() * 0.5, r.z + (rnd() - 0.5) * r.d, (rnd() - 0.5) * 0.6, rnd() * 3, 0);
  }
  // the fire in the first of them, and its smoke, leaning with the wind
  const fire = P.HAZARDS.find((h) => h.id === 'fire'), fy = P.ground(fire.x, fire.z) + 1.2;
  for (const [dx, dz, h, c] of [[0, 0, 3.2, 0xff7a1a], [0.9, 0.4, 2.2, YELLOW], [-0.8, 0.5, 2.5, 0xff5a1a], [0.2, -0.9, 1.8, YELLOW], [-0.3, 0.2, 1.4, 0xfff1a8]]) K.cyl(group, 0, 0.75, h, c, fire.x + dx, fy + h / 2, fire.z + dz, 5);
  const bantay = P.MISSIONS.find((m) => m.id === 'bantay'), wf = bantay.windFrom * Math.PI / 180, wx = -Math.sin(wf), wz = Math.cos(wf);
  for (let k = 0; k < 7; k++) K.ball(group, 1.2 + k * 0.45, k % 2 ? 0x55585c : 0x6a6d70, fire.x + wx * k * k * 0.35, fy + 3 + k * 2.6, fire.z + wz * k * k * 0.35);
  // the crack across the bridge, and a railing knocked askew
  const B = P.BRIDGE, zc = (B.spanZ0 + B.spanZ1) / 2;
  for (let k = 0; k < 6; k++) K.box(group, 1.5, 0.05, 0.22, DARK, B.x - B.w / 2 + 0.7 + k * 1.33, B.deck + 0.03, zc + (k % 2 ? 0.45 : -0.45), 0, k % 2 ? 0.7 : -0.7, 0);
  K.box(group, 0.16, 0.1, 5, WHITE, B.x + B.w / 2 + 0.3, B.deck + 0.5, zc, 0, 0, 0.9);
  K.box(group, 2.2, 1.3, 0.06, 0xff7a1a, B.x, B.deck + 0.75, B.z0 + 1.2);
  // the landslide on the school's hill: bare earth, boulders, a tree gone over
  const slide = P.HAZARDS.find((h) => h.id === 'slide');
  const pts = [[-9, -7], [-2, -11], [7, -8], [10, 0], [6, 9], [-3, 11], [-10, 5]];
  const on = (x, z, up) => [x, P.ground(x, z) + up, z];
  for (let k = 0; k < pts.length; k++) {
    // a fan of faces lying on the slope
    const [ax, az] = pts[k], [bx, bz] = pts[(k + 1) % pts.length];
    const geo = new K.T.BufferGeometry();
    geo.setAttribute('position', new K.T.Float32BufferAttribute([...on(slide.x, slide.z, 0.3), ...on(slide.x + bx, slide.z + bz, 0.3), ...on(slide.x + ax, slide.z + az, 0.3)], 3));
    group.add(new K.T.Mesh(geo, K.lam(k % 2 ? 0x8a623c : 0x7a5632)));
  }
  for (let k = 0; k < 9; k++) { const x = slide.x - 12 + rnd() * 8, z = slide.z - 8 + rnd() * 16; K.ball(group, 0.6 + rnd() * 0.9, k % 2 ? 0x77706a : 0x8a8279, x, P.ground(x, z) + 0.4, z); }
  K.cyl(group, 0.25, 0.3, 6, TRUNK, slide.x - 6, P.ground(slide.x - 6, slide.z + 3) + 0.6, slide.z + 3, 6, 0, 0.4, 1.45); K.ball(group, 2.2, LEAF[0], slide.x - 10, P.ground(slide.x - 10, slide.z + 4.5) + 1.2, slide.z + 4.5);
  // the fuel spilled on the station's forecourt
  const spill = P.HAZARDS.find((h) => h.id === 'spill'), sy = P.ground(spill.x, spill.z) + 0.1;
  K.patch(group, [[-4.5, -2], [-1, -3.4], [3.5, -2.2], [5, 0.8], [2, 3.2], [-2.5, 2.6], [-5, 0.6]].map(([x, z]) => [spill.x + x, spill.z + z]), sy, 0.04, 0x2b2440);
  K.patch(group, [[-2.5, -1], [0.5, -1.8], [2.6, -0.6], [1.5, 1.4], [-1.5, 1.2]].map(([x, z]) => [spill.x + x, spill.z + z]), sy + 0.04, 0.02, 0x5a4a8a);
  K.cyl(group, 0.3, 0.3, 0.9, 0x2f74d0, spill.x - 3.4, sy + 0.3, spill.z - 2.6, 8, 0, 0.3, Math.PI / 2); K.cyl(group, 0.3, 0.3, 0.9, 0x2f74d0, spill.x - 4.2, sy + 0.45, spill.z - 3.4, 8);
  // the debris that dams the creek under the river road
  const dam = P.HAZARDS.find((h) => h.id === 'dam'), dy = P.ground(dam.x, dam.z - 5);
  for (let k = 0; k < 12; k++) K.cyl(group, 0.18, 0.24, 4 + rnd() * 3.5, k % 3 ? TRUNK : WOOD, dam.x + (rnd() - 0.5) * 5, dy + 0.3 + rnd() * 1.6, dam.z - 5 + (rnd() - 0.5) * 2.4, 6, rnd() * 0.5, rnd() * 3, 1.2 + rnd() * 0.6);
  for (let k = 0; k < 8; k++) K.box(group, 1 + rnd() * 1.5, 0.3 + rnd() * 0.5, 0.8 + rnd(), k % 2 ? 0x8d877b : 0x9aa3ab, dam.x + (rnd() - 0.5) * 6, dy + 0.4 + rnd() * 1.4, dam.z - 5 + (rnd() - 0.5) * 2.2, rnd(), rnd() * 3, rnd());
  K.ball(group, 1.8, LEAF[1], dam.x + 1.5, dy + 1.9, dam.z - 5.6);
}

// ---------- the flood ----------

function car(K, group, x, z, yaw, color) {
  const hold = new K.T.Group(); hold.position.set(x, roadY(x, z) + 0.07, z); hold.rotation.y = yaw; group.add(hold);
  K.box(hold, 4.2, 0.7, 1.7, color, 0, 0.65, 0); K.box(hold, 2.2, 0.6, 1.5, color, -0.2, 1.3, 0); K.box(hold, 2.25, 0.4, 1.52, WINDOW, -0.2, 1.25, 0);
  for (const [dx, dz] of [[-1.3, -0.8], [1.3, -0.8], [1.3, 0.8], [-1.3, 0.8]]) K.cyl(hold, 0.33, 0.33, 0.25, DARK, dx, 0.33, dz, 8, Math.PI / 2);
}

function flood(K, group) {
  // the jeepney stranded on the main street: two people are on its roof
  const j = P.SURVIVORS.find((s) => s.id === 'jeep'), y = roadY(j.x, j.z) + 0.07;
  const hold = new K.T.Group(); hold.position.set(j.x, y, j.z); group.add(hold);
  K.box(hold, 5.6, 1.3, 2, 0xc62f22, 0, 1.15, 0); K.box(hold, 3.9, 0.12, 2.1, 0xf4f4f0, -0.6, 2.36, 0); K.box(hold, 1.3, 0.8, 1.9, 0xb9c0c6, 2.3, 0.95, 0);
  for (const dz of [-0.98, 0.98]) { K.box(hold, 3.8, 0.5, 0.05, WINDOW, -0.6, 2.0, dz); K.box(hold, 5.6, 0.16, 0.06, YELLOW, 0, 1.45, dz * 1.03); }
  for (const dx of [-2.45, -0.6, 1.25]) for (const dz of [-0.95, 0.95]) K.box(hold, 0.1, 0.6, 0.1, 0xc62f22, dx, 2.0, dz);
  for (const [dx, dz] of [[-1.8, -1], [1.9, -1], [1.9, 1], [-1.8, 1]]) K.cyl(hold, 0.42, 0.42, 0.3, DARK, dx, 0.42, dz, 8, Math.PI / 2);
  car(K, group, -96, 40, Math.PI / 2 + 0.2, 0x2f74d0); car(K, group, 74, 83, 0.1, 0xb9c0c6); car(K, group, 10, -22, -0.15, 0x4caf50); car(K, group, -150, 30, Math.PI / 2, 0xf2b300);
  // the platform lashed to the trees of the grove, where a family waits under the leaves
  const gr = P.SURVIVORS.find((s) => s.id === 'grove'), gy = P.ground(gr.x, gr.z);
  K.box(group, 3.2, 0.14, 2.4, WOOD, gr.x, gy + gr.y - 0.1, gr.z);
  for (const [dx, dz] of [[-1.4, -1], [1.4, -1], [1.4, 1], [-1.4, 1]]) K.cyl(group, 0.3, 0.3, gr.y, 0x2f74d0, gr.x + dx, gy + gr.y / 2 - 0.1, gr.z + dz, 8);
  K.box(group, 2.8, 1.4, 0.05, 0x2f8a6a, gr.x, gy + gr.y + 1.3, gr.z - 1.1, 0.5, 0, 0);
}

// ---------- the typhoon ----------

function wind(K, group, rnd) {
  // trees across the streets
  for (const [x, z, yaw] of [[-112, -20, 0.3], [62, -19, -0.4], [192, -21, 0.2], [-18, -110, 0.5], [-90, 22, 1.3], [80, -62, 1.8], [150, 30, 1.4]]) {
    const y = roadY(x, z) + 0.5;
    const hold = new K.T.Group(); hold.position.set(x, y, z); hold.rotation.y = yaw; group.add(hold);
    K.cyl(hold, 0.2, 0.32, 8, TRUNK, 0, 0, 0, 6, Math.PI / 2 - 0.06, 0, 0); K.ball(hold, 2.6, LEAF[Math.floor(rnd() * 3)], 0, 1.2, 5.2); K.ball(hold, 1.8, LEAF[1], 1.4, 0.9, 3.4);
    K.ball(hold, 0.9, 0x6b5e4b, 0, 0.1, -4.1);
  }
  // roofing sheets torn off and thrown about
  for (let k = 0; k < 18; k++) {
    const x = -200 + rnd() * 400, z = -100 + rnd() * 170;
    K.box(group, 2.2, 0.04, 0.9, k % 3 ? 0x9aa3ab : 0x9c4a36, x, Math.max(roadY(x, z), P.ground(x, z)) + 0.15 + rnd() * 0.15, z, (rnd() - 0.5) * 0.5, rnd() * 3, (rnd() - 0.5) * 0.5);
  }
  // a pole down across the river road
  K.cyl(group, 0.13, 0.17, 9, 0x5a5048, -60, roadY(-60, 80) + 0.4, 80, 6, Math.PI / 2 - 0.05, 0.5, 0);
}
