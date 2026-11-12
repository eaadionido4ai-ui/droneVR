// The plan of the riverside barangay that DRONE VR flies over: its ground, its streets and buildings, and
// what each mission looks for (the gates of the flight school, the hazards, the survivors, the drop sites).
// Everything else is made from this file:
//   - the models (town.js builds the town from it, in three.js, for Blender and Unity);
//   - the C# data (gen-data.mjs writes Unity/Assets/DRONEVR/Scripts/Core/Generated/TownPlan.g.cs);
//   - the GAML data (gen-data.mjs writes GAMA/models/DRONE Town.gaml).
// So a house moved here moves in the model, in the drone's collisions and on GAMA's map at once.
//
// Coordinates are DRONE VR's own: metres, x to the east, z to the south, y up. The barangay is 480 m from
// west to east (x -240..240) and 360 m from north to south (z -180..180). Heights are above the river's
// normal level. In Unity a point (x, y, z) is at (x, y, -z); in GAMA it is at (x + 240, z + 180).
//
// The ground is a formula (ground(x, z) below), so that the same ground can be computed in C#
// (Core/Terrain.cs), in GAML (DRONE.gaml) and in Python (Tools/flight-reference/flight.py). Its numbers are
// TERRAIN; Tools/check_constants.py checks that the four places agree.

export const WORLD = { x0: -240, x1: 240, z0: -180, z1: 180, cell: 10 };

export const TERRAIN = {
  riverNorth: 100, riverSouth: 140,     // the river runs from west to east between these two z
  riverBank: 4, riverDepth: 3.1,        // its banks slope over 4 m, down to 3.1 m below the bank
  northBank: 0.6, northSlope: 0.010,    // north of the river the land rises 1 cm per metre
  valleyBank: 0.3, valleySlope: 0.004, valleySide: 0.022, // the creek's valley: lower, rising away from the creek
  creekX: -40, creekHalf: 4, creekDepth: 1.5,             // the creek runs south into the river at x = -40
  southBank: 0.8, southSlope: 0.03,     // the far bank, where the sitio is
  hillX: 150, hillZ: -110, hillR: 90, hillPeak: 6, hillTop: 4,   // the school's hill, flat on top
  riseX: -170, riseZ: -125, riseR: 70, risePeak: 3, riseTop: 1.5, // the rise the barangay hall stands on
};

const clamp01 = (v) => Math.max(0, Math.min(1, v));

// The height of the ground at a point, in metres above the river's normal level.
export function ground(x, z) {
  const T = TERRAIN;
  if (z >= T.riverSouth) return T.southBank + (z - T.riverSouth) * T.southSlope;
  const side = Math.max(0, Math.abs(x - T.creekX) - T.creekHalf) * T.valleySide;
  const cut = T.creekDepth * clamp01(T.creekHalf - Math.abs(x - T.creekX));
  if (z > T.riverNorth) {
    const bank = Math.min(T.northBank, T.valleyBank + side) - cut;
    const k = (z - T.riverNorth) / (T.riverSouth - T.riverNorth);
    const dip = clamp01(Math.min(z - T.riverNorth, T.riverSouth - z) / T.riverBank);
    return bank + (T.southBank - bank) * k - T.riverDepth * dip;
  }
  const up = T.riverNorth - z;
  let g = Math.min(T.northBank + up * T.northSlope, T.valleyBank + up * T.valleySlope + side);
  g += Math.min(T.hillTop, T.hillPeak * Math.max(0, 1 - Math.hypot(x - T.hillX, z - T.hillZ) / T.hillR));
  g += Math.min(T.riseTop, T.risePeak * Math.max(0, 1 - Math.hypot(x - T.riseX, z - T.riseZ) / T.riseR));
  return g - cut;
}

// ---------- streets ----------
// Each is a straight strip: from (x0, z0) to (x1, z1), so many metres wide.
export const ROADS = [
  { id: 'north', x0: -225, z0: -110, x1: 225, z1: -110, w: 7 },
  { id: 'main', x0: -235, z0: -20, x1: 235, z1: -20, w: 8 },
  { id: 'river', x0: -225, z0: 80, x1: 225, z1: 80, w: 6 },
  { id: 'st1', x0: -150, z0: -128, x1: -150, z1: 80, w: 6 },
  { id: 'st2', x0: -90, z0: -110, x1: -90, z1: 80, w: 6 },
  { id: 'st3', x0: 20, z0: -110, x1: 20, z1: 97, w: 7 },
  { id: 'st4', x0: 80, z0: -110, x1: 80, z1: 80, w: 6 },
  { id: 'st5', x0: 150, z0: -110, x1: 150, z1: 80, w: 6 },
  { id: 'sitio', x0: -10, z0: 160, x1: 100, z1: 160, w: 5 },
  { id: 'sitio_link', x0: 20, z0: 143, x1: 20, z1: 160, w: 6 },
];

// The bridge to the sitio, on street 3: its deck is 3.4 m above the river's normal level.
export const BRIDGE = { x: 20, z0: 97, z1: 143, w: 8, deck: 3.4, thick: 0.6, spanZ0: 112, spanZ1: 128 };

// ---------- the operations center, the home pad and the places that have a name ----------
// The operations center is a room under the barangay hall, where the operators stand: its middle, its size and
// its floor, which is 6 m below the river's normal level so that no flood ever shows in it.
export const EOC = { x: -172, z: -134, floor: -6, w: 16, d: 10, h: 3.4 };
// The map table in its middle shows the whole barangay, one metre for every 200.
export const TABLE = { x: -172, z: -134.3, top: 0.86, w: 2.8, d: 2.2, scale: 200 };
export const PAD = { x: -148, z: -134, size: 8 };          // where the drones take off and land, beside the hall
export const STAGING = [                                    // where the rescue boats wait and bring people
  { id: 'west', x: -150, z: -16 },
  { id: 'east', x: 92, z: -20 },
];

// A building is a box with a roof: its center, its size (w along x, d along z), the height of its walls,
// its roof ('gable', 'flat', 'open' for a roof on posts) and how much higher the roof's top is.
// 'set' ties it to a scene set (see SETS): it is only there when that set is shown.
const SPECIAL = [
  { id: 'hall', kind: 'hall', x: -172, z: -134, w: 18, d: 12, h: 7, roof: 'flat', roofH: 0.4, wall: 0xf0e6cf, top: 0x8a8f96 },
  { id: 'school', kind: 'school', x: 150, z: -140, w: 46, d: 10, h: 3.8, roof: 'gable', roofH: 2.2, wall: 0xf3e6b8, top: 0x3f7a4e },
  { id: 'court', kind: 'court', x: 150, z: -92, w: 26, d: 18, h: 6.6, roof: 'open', roofH: 1.6, wall: 0xdcdcdc, top: 0x3a6ea5 },
  { id: 'health', kind: 'health', x: 62, z: -46, w: 14, d: 10, h: 3.6, roof: 'flat', roofH: 0.4, wall: 0xf7f4ee, top: 0xcfd6dc },
  { id: 'chapel', kind: 'chapel', x: -112, z: 26, w: 12, d: 20, h: 6, roof: 'gable', roofH: 3, wall: 0xf6efe0, top: 0x8c4a3a },
  { id: 'belfry', kind: 'tower', x: -112, z: 12, w: 4, d: 4, h: 13, roof: 'gable', roofH: 2, wall: 0xf6efe0, top: 0x8c4a3a },
  { id: 'market', kind: 'market', x: 52, z: 36, w: 30, d: 20, h: 5, roof: 'flat', roofH: 0.5, wall: 0xe9d9b0, top: 0xb5553c },
  { id: 'gym', kind: 'court', x: 114, z: 44, w: 20, d: 28, h: 7.2, roof: 'open', roofH: 1.8, wall: 0xdcdcdc, top: 0x2f8a6a },
  { id: 'store', kind: 'store', x: -126, z: 56, w: 7, d: 6, h: 3.2, roof: 'flat', roofH: 0.3, wall: 0xf2f2f2, top: 0xd43b2f },
  { id: 'canopy', kind: 'court', x: -116, z: 64, w: 14, d: 10, h: 4.8, roof: 'open', roofH: 0.6, wall: 0xf2f2f2, top: 0xd43b2f },
  { id: 'mill', kind: 'warehouse', x: 188, z: 50, w: 24, d: 16, h: 7, roof: 'gable', roofH: 2.6, wall: 0xc9c2b2, top: 0x7d8790 },
  { id: 'roofhouse', kind: 'house', x: -68, z: 90, w: 10, d: 9, h: 6.2, roof: 'flat', roofH: 0.5, wall: 0xcfe3ea, top: 0xb9b4a8 },
  { id: 'sitio1', kind: 'house', x: 4, z: 169, w: 8, d: 7, h: 3, roof: 'gable', roofH: 1.6, wall: 0xe8d4b0, top: 0x9c4a36 },
  { id: 'sitio2', kind: 'house', x: 22, z: 172, w: 7, d: 7, h: 3, roof: 'gable', roofH: 1.5, wall: 0xcfe0c0, top: 0x5f7f8f },
  { id: 'sitio3', kind: 'house', x: 58, z: 172, w: 8, d: 7, h: 3, roof: 'flat', roofH: 0.4, wall: 0xf0d9d0, top: 0xb9b4a8 },
  { id: 'sitio4', kind: 'house', x: 74, z: 151, w: 7, d: 8, h: 3, roof: 'gable', roofH: 1.6, wall: 0xe4e0c8, top: 0x3f7a4e },
  { id: 'sitio5', kind: 'house', x: 92, z: 170, w: 8, d: 7, h: 3, roof: 'gable', roofH: 1.5, wall: 0xd8d0e0, top: 0x9c4a36 },
  { id: 'kapilya', kind: 'chapel', x: 40, z: 175, w: 7, d: 8, h: 3.6, roof: 'gable', roofH: 2, wall: 0xf6efe0, top: 0x8c4a3a },
];

// Round things a drone can hit: the water tower's tank on its legs, the mobile phone tower.
export const TOWERS = [
  { id: 'watertower', kind: 'watertower', x: -200, z: -62, r: 3.2, h: 18 },
  { id: 'celltower', kind: 'celltower', x: 205, z: -160, r: 2.6, h: 42 },
];

// The houses that fall in the earthquake: they stand in the 'normal' set and lie as rubble in the 'quake' set.
const FALLEN = [{ x: -62, z: -34 }, { x: 108, z: -34 }, { x: 34, z: 66 }];

const WALLS = [0xf3e9d2, 0xe8d4b0, 0xcfe0c0, 0xf0d9d0, 0xd8d0e0, 0xcfe3ea, 0xf2e2a8, 0xe4e0c8, 0xd9c3a0, 0xbfd3c1];
const ROOFS = [0x9c4a36, 0xb5553c, 0x5f7f8f, 0x3f7a4e, 0x7d8790, 0x8a5a3a, 0x4a6fa5, 0xa33f3f];

function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const rect = (x, z, w, d, m = 0) => ({ x0: x - w / 2 - m, x1: x + w / 2 + m, z0: z - d / 2 - m, z1: z + d / 2 + m });
const overlap = (a, b) => a.x0 < b.x1 && a.x1 > b.x0 && a.z0 < b.z1 && a.z1 > b.z0;
const roadRect = (r, m = 0) => ({ x0: Math.min(r.x0, r.x1) - (r.x0 === r.x1 ? r.w / 2 : 0) - m, x1: Math.max(r.x0, r.x1) + (r.x0 === r.x1 ? r.w / 2 : 0) + m,
  z0: Math.min(r.z0, r.z1) - (r.z0 === r.z1 ? r.w / 2 : 0) - m, z1: Math.max(r.z0, r.z1) + (r.z0 === r.z1 ? r.w / 2 : 0) + m });

function makeBuildings() {
  const rnd = mulberry32(20261111);
  const pick = (list) => list[Math.floor(rnd() * list.length)];
  const out = SPECIAL.map((b) => Object.assign({ set: null }, b));
  // where no house may stand: the streets, the creek and the river, the hall's yard, the school's hill, the
  // open ground around the places with a name, the pad, the boats' landings, and the fields at the edges
  const keepOut = ROADS.map((r) => roadRect(r, 1.5)).concat(SPECIAL.map((b) => rect(b.x, b.z, b.w, b.d, 5)), TOWERS.map((t) => rect(t.x, t.z, 12, 12)), [
    { x0: TERRAIN.creekX - 13, x1: TERRAIN.creekX + 13, z0: -180, z1: 100 }, { x0: -240, x1: 240, z0: 96, z1: 147 },
    { x0: -200, x1: -134, z0: -156, z1: -116 }, rect(150, -116, 96, 78), rect(PAD.x, PAD.z, 22, 22),
    rect(62, -32, 16, 14), rect(40, 165, 16, 12), rect(-150, -16, 24, 20), rect(92, -20, 22, 18), rect(100, 2, 26, 22), rect(-20, 60, 12, 12),
  ]);
  const add = (x, z, w, d) => {
    const r = rect(x, z, w, d, 1.5);
    if (x - w / 2 < WORLD.x0 + 8 || x + w / 2 > WORLD.x1 - 8 || z - d / 2 < WORLD.z0 + 8) return;
    if (keepOut.some((k) => overlap(r, k)) || out.some((b) => overlap(r, rect(b.x, b.z, b.w, b.d)))) return;
    const two = rnd() < 0.22, flat = rnd() < 0.2;
    const b = { id: 'h' + String(out.length - SPECIAL.length + 1).padStart(3, '0'), kind: 'house', x: Math.round(x * 2) / 2, z: Math.round(z * 2) / 2, w, d,
      h: two ? 5.8 : 3 + Math.round(rnd() * 4) / 10, roof: flat ? 'flat' : 'gable', roofH: flat ? 0.4 : 1.4 + Math.round(rnd() * 8) / 10, wall: pick(WALLS), top: pick(ROOFS), set: null };
    if (FALLEN.some((f) => Math.hypot(f.x - b.x, f.z - b.z) < 12 && !f.taken && (f.taken = b.id))) b.set = 'normal';
    out.push(b);
  };
  // houses along the streets that run east to west, on both sides
  for (const r of ROADS.filter((q) => q.z0 === q.z1 && q.id !== 'sitio')) {
    for (const s of [-1, 1]) {
      for (let x = r.x0 + 8; x < r.x1 - 6; x += 15 + rnd() * 5) {
        const w = 7 + Math.round(rnd() * 3), d = 7 + Math.round(rnd() * 2);
        add(x + rnd() * 2, r.z0 + s * (r.w / 2 + 3 + d / 2 + rnd() * 2.5), w, d);
      }
    }
  }
  // and along the streets that run north to south
  for (const r of ROADS.filter((q) => q.x0 === q.x1 && q.id.startsWith('st'))) {
    for (const s of [-1, 1]) {
      for (let z = r.z0 + 12; z < Math.min(r.z1, 70); z += 15 + rnd() * 5) {
        const w = 7 + Math.round(rnd() * 2), d = 7 + Math.round(rnd() * 3);
        add(r.x0 + s * (r.w / 2 + 3 + w / 2 + rnd() * 2.5), z + rnd() * 2, w, d);
      }
    }
  }
  for (const b of out) {
    b.base = Math.round(Math.max(ground(b.x, b.z), ground(b.x - b.w / 2, b.z), ground(b.x + b.w / 2, b.z), ground(b.x, b.z - b.d / 2), ground(b.x, b.z + b.d / 2)) * 100) / 100 + 0.15;
  }
  return out;
}

export const BUILDINGS = makeBuildings();
export const building = (id) => BUILDINGS.find((b) => b.id === id);
const nearestHouse = (x, z, want) => BUILDINGS.filter((b) => b.kind === 'house' && !b.set && (!want || want(b)))
  .reduce((best, b) => (!best || Math.hypot(b.x - x, b.z - z) < Math.hypot(best.x - x, best.z - z) ? b : best), null);
/** The height of the top of a building: its ridge, or its flat roof. */
export const roofTop = (b) => b.base + b.h + b.roofH;

// What a fallen house leaves: a low heap (the 'quake' set).
export const RUBBLE = BUILDINGS.filter((b) => b.set === 'normal').map((b, i) => ({ id: 'rubble' + (i + 1), x: b.x, z: b.z, w: b.w + 2, d: b.d + 2, h: 1.8, base: b.base - 0.15, of: b.id }));

// ---------- trees and poles ----------
function makeTrees() {
  const rnd = mulberry32(77031);
  const out = [];
  const blocked = (x, z, m) => ROADS.some((r) => overlap(rect(x, z, m, m), roadRect(r, 1))) || BUILDINGS.some((b) => overlap(rect(x, z, m, m), rect(b.x, b.z, b.w, b.d, 1.5)))
    || Math.hypot(x - PAD.x, z - PAD.z) < 16 || (z > 97 && z < 143) || Math.abs(x - TERRAIN.creekX) < 6 && z < 100
    || [[62, -32], [40, 165], [150, -122], [-150, -16], [92, -20], [-20, 60]].some(([px, pz]) => Math.hypot(x - px, z - pz) < 9);
  for (let i = 0; i < 400 && out.length < 150; i++) {
    const x = WORLD.x0 + 6 + rnd() * (WORLD.x1 - WORLD.x0 - 12), z = WORLD.z0 + 6 + rnd() * (WORLD.z1 - WORLD.z0 - 12);
    if (blocked(x, z, 4) || out.some((t) => Math.hypot(t.x - x, t.z - z) < 5)) continue;
    const palm = rnd() < 0.55;
    out.push({ kind: palm ? 'palm' : 'tree', x: Math.round(x * 10) / 10, z: Math.round(z * 10) / 10, h: Math.round((palm ? 8 + rnd() * 4 : 5 + rnd() * 3) * 10) / 10, r: palm ? 2.2 : 2.6 });
  }
  // the grove whose leaves hide a family from the sky (see SURVIVORS), and the tree a man holds on to
  for (const [dx, dz] of [[-4, -3], [3, -4], [5, 2], [-2, 4], [-6, 1], [0, 0]]) out.push({ kind: 'tree', x: 100 + dx, z: 2 + dz, h: 6.5, r: 3 });
  out.push({ kind: 'palm', x: -20, z: 60, h: 9, r: 2.2 });
  return out;
}
export const TREES = makeTrees();

// Electric poles along the main street, 9 m tall. In the 'quake' set the wire between the poles at x = 135 and
// x = 165 is down (the 'wire' hazard lies under it).
export const POLES = Array.from({ length: 16 }, (_, i) => ({ x: -225 + i * 30, z: -26, h: 9 })).filter((p) => Math.abs(p.x - TERRAIN.creekX) > 8);

// ---------- scene sets ----------
// The same town is shown in four ways. A set is a group of pieces that Unity switches on or off.
export const SETS = {
  normal: 'the houses that fall in the earthquake, standing; the upright poles and tower',
  quake: 'rubble, the fire, the fallen wire, the cracked bridge, the landslide, the spill, the dam of debris',
  flood: 'what the flood carries and strands: the jeepney, drifting debris',
  wind: 'what the typhoon tore down: trees across the streets, roofing sheets',
  bridge_ok: 'the middle of the bridge',
  bridge_out: 'the middle of the bridge, fallen',
};

// ---------- the missions ----------
// Each mission flies in its own weather: wind is the mean speed in m/s at 60 m up and gust is how much it
// swings; windFrom is where it blows from (degrees, 0 north, 90 east); flood lists the water's level over the
// minutes of the mission (metres above the river's normal level).
export const MISSIONS = [
  { id: 'lipad', sets: ['normal', 'bridge_ok'], wind: 1.5, gust: 0.2, windFrom: 60, flood: [[0, 0]], minutes: 6 },
  { id: 'bantay', sets: ['quake', 'bridge_ok'], wind: 3, gust: 0.3, windFrom: 120, flood: [[0, 0]], minutes: 8 },
  { id: 'sagip', sets: ['normal', 'flood', 'bridge_ok'], wind: 5, gust: 0.4, windFrom: 200, flood: [[0, 2.2], [6, 2.8], [10, 2.8]], minutes: 10 },
  { id: 'hatid', sets: ['normal', 'wind', 'bridge_out'], wind: 9, gust: 0.5, windFrom: 250, flood: [[0, 1.5], [8, 1.1]], minutes: 10 },
];

// LIPAD, the flight school: rings to fly through, in order. y is the height of the ring's center above the ground.
export const GATES = [
  { x: -120, z: -134, y: 12, r: 5 },
  { x: -96, z: -150, y: 18, r: 5 },
  { x: -70, z: -128, y: 26, r: 5 },
  { x: -96, z: -84, y: 20, r: 5 },
  { x: -130, z: -90, y: 14, r: 5 },
  { x: -148, z: -112, y: 10, r: 5 },
];

// BANTAY, the hazard patrol after the earthquake. y is the height of the thing to photograph above the ground;
// near is how close a drone may safely come; report is where the residents said to look (not quite the spot).
const fallen = (i) => BUILDINGS.find((b) => b.id === RUBBLE[i].of);
export const HAZARDS = [
  { id: 'fire', x: fallen(0).x, z: fallen(0).z, y: 2.5, near: 12, report: [14, -10] },
  { id: 'wire', x: 150, z: -26, y: 4, near: 10, report: [-12, 14] },
  { id: 'bridge', x: BRIDGE.x, z: 120, y: BRIDGE.deck, near: 6, report: [10, -16] },
  { id: 'slide', x: 96, z: -128, y: 3, near: 6, report: [-14, 12] },
  { id: 'spill', x: -114, z: 73, y: 0.5, near: 10, report: [14, -12] },
  { id: 'tank', x: -200, z: -62, y: 15, near: 8, report: [16, 10] },
  { id: 'dam', x: TERRAIN.creekX, z: 80, y: 1, near: 6, report: [-10, -14] },
];

// SAGIP, the search and rescue in the flood. y is how high the people are above the ground; people is how
// many; hidden means they cannot be seen from above (under a roof, under leaves): the thermal camera finds
// them, from close; limit is how many minutes they can safely wait; ring means they are in fast water, and a
// boat can only pull them in once a life ring has been dropped to them.
const s1 = nearestHouse(-104, 90), s3 = nearestHouse(-62, 30), s8 = building('sitio3');
export const SURVIVORS = [
  { id: 'roof1', where: 'roof', x: s1.x, z: s1.z, y: roofTop(s1) - s1.base + 0.15, people: 3, hidden: false, limit: 6, ring: false, on: s1.id },
  { id: 'market', where: 'roof', x: 46, z: 33, y: 5.65, people: 2, hidden: false, limit: 12, ring: false, on: 'market' },
  { id: 'roof2', where: 'roof', x: s3.x, z: s3.z, y: roofTop(s3) - s3.base + 0.15, people: 2, hidden: false, limit: 7, ring: false, on: s3.id },
  { id: 'tree', where: 'tree', x: -20, z: 60, y: 2.6, people: 1, hidden: false, limit: 5, ring: true, on: null },
  { id: 'jeep', where: 'jeepney', x: 44, z: -20, y: 2.5, people: 2, hidden: false, limit: 7, ring: false, on: null },
  { id: 'grove', where: 'raft', x: 100, z: 2, y: 1.4, people: 2, hidden: true, limit: 8, ring: false, on: null },
  { id: 'gym', where: 'bleachers', x: 114, z: 50, y: 2.4, people: 4, hidden: true, limit: 12, ring: false, on: 'gym' },
  { id: 'sitio', where: 'roof', x: s8.x, z: s8.z, y: roofTop(s8) - s8.base + 0.15, people: 2, hidden: false, limit: 9, ring: false, on: s8.id },
];

// HATID, the relief flights. r is the radius of the place to drop into; y is its height above the ground
// (the roof of the house by the river); needs lists what to bring.
const rh = building('roofhouse');
export const SITES = [
  { id: 'evac', x: 150, z: -122, y: 0, r: 6, needs: ['food', 'water'] },
  { id: 'sitio', x: 40, z: 165, y: 0, r: 5, needs: ['water', 'medicine'] },
  { id: 'health', x: 62, z: -32, y: 0, r: 4, needs: ['firstaid', 'radio'] },
  { id: 'roof', x: rh.x, z: rh.z, y: roofTop(rh) - ground(rh.x, rh.z), r: 2.5, needs: ['food'] },
];

// What a drone can carry: kilograms each. A drone lifts 8 kg, on 3 hooks.
export const SUPPLIES = [
  { id: 'water', kg: 4, color: 0x2f74d0 },
  { id: 'food', kg: 3, color: 0xd9650a },
  { id: 'medicine', kg: 1, color: 0xc62f22 },
  { id: 'firstaid', kg: 2, color: 0x128253 },
  { id: 'radio', kg: 1, color: 0x4f6572 },
  { id: 'ring', kg: 2, color: 0xff7a1a },
];

// ---------- the boats' ways through the flood ----------
// For each staging place and each survivor: the way a boat takes between them, found on a grid of 5 m cells
// through water deep enough for it when the SAGIP flood begins, around the houses. The same lists of points
// are used by Unity and by GAMA, so the boats move alike in both.
const NAV = 5, DRAFT = 0.35;
function navigable(level) {
  const nx = Math.round((WORLD.x1 - WORLD.x0) / NAV), nz = Math.round((WORLD.z1 - WORLD.z0) / NAV);
  const ok = new Uint8Array(nx * nz);
  const solid = BUILDINGS.filter((b) => b.roof !== 'open').map((b) => rect(b.x, b.z, b.w, b.d, 1.6));
  for (let j = 0; j < nz; j++) for (let i = 0; i < nx; i++) {
    const x = WORLD.x0 + (i + 0.5) * NAV, z = WORLD.z0 + (j + 0.5) * NAV;
    const c = { x0: x - 1, x1: x + 1, z0: z - 1, z1: z + 1 };
    ok[j * nx + i] = ground(x, z) < level - DRAFT && !solid.some((s) => overlap(c, s)) ? 1 : 0;
  }
  return { nx, nz, ok };
}

function route(grid, from, to) {
  const { nx, nz, ok } = grid;
  const cellOf = (p) => {
    let best = -1, bd = 1e9;
    for (let j = 0; j < nz; j++) for (let i = 0; i < nx; i++) {
      if (!ok[j * nx + i]) continue;
      const d = Math.hypot(WORLD.x0 + (i + 0.5) * NAV - p.x, WORLD.z0 + (j + 0.5) * NAV - p.z);
      if (d < bd) { bd = d; best = j * nx + i; }
    }
    return best;
  };
  const a = cellOf(from), b = cellOf(to);
  const dist = new Float64Array(nx * nz).fill(1e18), prev = new Int32Array(nx * nz).fill(-1), done = new Uint8Array(nx * nz);
  dist[a] = 0;
  for (;;) { // Dijkstra on a small grid: plain and exact
    let u = -1, du = 1e18;
    for (let k = 0; k < dist.length; k++) if (!done[k] && dist[k] < du) { du = dist[k]; u = k; }
    if (u < 0 || u === b) break;
    done[u] = 1;
    const ui = u % nx, uj = (u - ui) / nx;
    for (let dj = -1; dj <= 1; dj++) for (let di = -1; di <= 1; di++) {
      if (!di && !dj) continue;
      const vi = ui + di, vj = uj + dj;
      if (vi < 0 || vj < 0 || vi >= nx || vj >= nz) continue;
      const v = vj * nx + vi;
      if (!ok[v] || (di && dj && (!ok[uj * nx + vi] || !ok[vj * nx + ui]))) continue;
      const nd = du + Math.hypot(di, dj) * NAV;
      if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
    }
  }
  if (prev[b] < 0 && a !== b) return null;
  const cells = [];
  for (let k = b; k >= 0; k = prev[k]) cells.push(k);
  cells.reverse();
  const pts = cells.map((k) => ({ x: WORLD.x0 + (k % nx + 0.5) * NAV, z: WORLD.z0 + (Math.floor(k / nx) + 0.5) * NAV }));
  // keep only the corners: drop a point when the boat can go straight from the one before to the one after
  const clear = (p, q) => {
    const n = Math.ceil(Math.hypot(q.x - p.x, q.z - p.z) / 1.5);
    for (let s = 0; s <= n; s++) {
      const x = p.x + (q.x - p.x) * s / n, z = p.z + (q.z - p.z) * s / n;
      const i = Math.floor((x - WORLD.x0) / NAV), j = Math.floor((z - WORLD.z0) / NAV);
      if (i < 0 || j < 0 || i >= nx || j >= nz || !ok[j * nx + i]) return false;
    }
    return true;
  };
  const out = [pts[0]];
  for (let i = 1, last = 0; i < pts.length; i++) {
    if (i === pts.length - 1 || !clear(pts[last], pts[i + 1])) { out.push(pts[i]); last = i; }
  }
  return out.map((p) => [Math.round(p.x * 10) / 10, Math.round(p.z * 10) / 10]);
}

let boatRoutes = null;
/** routes[staging][survivor]: the points of the way from that staging place to that survivor. */
export function BOAT_ROUTES() {
  if (boatRoutes) return boatRoutes;
  const grid = navigable(MISSIONS.find((m) => m.id === 'sagip').flood[0][1]);
  boatRoutes = STAGING.map((s) => SURVIVORS.map((v) => {
    const r = route(grid, s, v);
    if (!r) throw new Error('no way for a boat from ' + s.id + ' to ' + v.id);
    return r;
  }));
  return boatRoutes;
}
