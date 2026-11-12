// Runs in a headless browser (see ../export.mjs). Builds the barangay, the operations center and the props in
// three.js, and hands the results to Node: glTF models and textures. With ?preview in the address it also
// takes pictures of what it built (out/preview/*.png), to look at before Blender has its turn.
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import '@fontsource/nunito/latin-800.css';
import '@fontsource/nunito/latin-900.css';

window.THREE = THREE;
const log = (t) => { document.getElementById('log').textContent += '\n' + t; if (window.__log) window.__log(t); };

const toBase64 = (blob) => new Promise((res, rej) => { const r = new FileReader(); r.onload = () => res(String(r.result).split(',')[1]); r.onerror = rej; r.readAsDataURL(blob); });
async function save(path, blob) {
  const b64 = await toBase64(blob), CH = 4 * 1024 * 1024, n = Math.max(1, Math.ceil(b64.length / CH));
  for (let i = 0; i < n; i++) await window.__save(path, i, n, b64.slice(i * CH, (i + 1) * CH));
}
const toGLB = (objects) => new Promise((res, rej) => {
  const scene = new THREE.Scene();
  objects.forEach((o) => scene.add(o));
  try { new GLTFExporter().parse(scene, (buf) => res(new Blob([buf], { type: 'model/gltf-binary' })), { binary: true, embedImages: true, onlyVisible: false }); } catch (e) { rej(e); }
});
const toPNG = (cv) => new Promise((res) => cv.toBlob((b) => res(b), 'image/png'));
const triangles = (meshes) => meshes.reduce((n, m) => n + (m.geometry.index ? m.geometry.index.count : m.geometry.attributes.position.count) / 3, 0);

async function run() {
  await Promise.all(['900 20px Nunito', '800 20px Nunito'].map((f) => document.fonts.load(f)));
  await document.fonts.ready;
  const { buildTown } = await import('./town.js');
  const { buildEoc } = await import('./eoc.js');
  const { buildProps } = await import('./props.js');

  log('building the barangay');
  const town = buildTown(THREE);
  log('  ' + town.meshes.length + ' pieces, ' + Math.round(triangles(town.meshes)) + ' triangles, ' + town.textures.size + ' signs');
  log('building the operations center');
  const eoc = buildEoc(THREE);
  log('  ' + eoc.meshes.length + ' pieces, ' + Math.round(triangles(eoc.meshes)) + ' triangles, ' + eoc.extra.length + ' markers');
  const props = buildProps(THREE);
  log('  ' + Object.keys(props).length + ' props: ' + Object.keys(props).map((n) => n + ' ' + Math.round(triangles(props[n].meshes))).join(', '));

  if (new URLSearchParams(location.search).has('preview')) await preview(town, eoc, props);

  await save('glb/town.glb', await toGLB(town.meshes.concat(town.extra)));
  await save('glb/eoc.glb', await toGLB(eoc.meshes.concat(eoc.extra)));
  for (const name of Object.keys(props)) await save('glb/' + name + '.glb', await toGLB(props[name].meshes.concat(props[name].extra)));
  log('models saved');
  const textures = new Map([...town.textures, ...eoc.textures]);
  for (const [name, cv] of textures) await save('textures/' + name + '.png', await toPNG(cv));
  log(textures.size + ' textures saved');
  log('done');
  window.__done && window.__done('ok');
}

// Pictures of the models as three.js draws them: the whole barangay from above in each mission's scene, a
// drone's view over the flooded streets, the operations center, and the props side by side.
async function preview(town, eoc, props) {
  const P = await import('./plan.js');
  const W = 1280, H = 800;
  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(W, H);
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xbfd9ea);
  scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f80, 0.62));
  const sun = new THREE.DirectionalLight(0xffffff, 0.5); sun.position.set(-0.5, 1, 0.35); scene.add(sun);
  const show = (m) => { const c = m.clone(); c.material = m.material.map ? new THREE.MeshBasicMaterial({ map: m.material.map, transparent: true, side: THREE.DoubleSide }) : new THREE.MeshLambertMaterial({ color: parseInt(m.name.split('__')[3], 16), side: THREE.DoubleSide }); return c; };
  const townGroup = new THREE.Group(); scene.add(townGroup);
  const bySet = new Map();
  for (const m of town.meshes) {
    const c = show(m); townGroup.add(c);
    const p = m.name.split('__');
    if (p[0] === 'W') { if (!bySet.has(p[1])) bySet.set(p[1], []); bySet.get(p[1]).push(c); }
  }
  const water = new THREE.Mesh(new THREE.PlaneGeometry(2400, 2400), new THREE.MeshLambertMaterial({ color: 0x4f8fc0, transparent: true, opacity: 0.78 }));
  water.rotation.x = -Math.PI / 2; scene.add(water);
  const cam = new THREE.PerspectiveCamera(55, W / H, 0.1, 3000);
  const shot = async (name, from, to, fov) => {
    cam.fov = fov || 55; cam.updateProjectionMatrix();
    cam.position.set(...from); cam.lookAt(...to);
    renderer.render(scene, cam);
    await save('preview/' + name + '.png', await toPNG(renderer.domElement));
  };
  const scene_of = (id) => {
    const m = P.MISSIONS.find((q) => q.id === id);
    for (const [name, list] of bySet) list.forEach((o) => { o.visible = m.sets.includes(name); });
    water.position.y = m.flood[m.flood.length > 1 ? 1 : 0][1];
    if (id === 'hatid') water.position.y = m.flood[0][1];
  };
  for (const id of ['lipad', 'bantay', 'sagip', 'hatid']) { scene_of(id); await shot('town_' + id, [-40, 330, 300], [-10, 0, 0], 50); }
  scene_of('lipad');
  await shot('pad', [P.PAD.x + 26, P.ground(P.PAD.x, P.PAD.z) + 14, P.PAD.z + 24], [P.PAD.x - 6, P.ground(P.PAD.x, P.PAD.z) + 3, P.PAD.z]);
  await shot('school', [150, 40, -40], [150, 8, -120]);
  scene_of('sagip');
  await shot('flood_fpv', [-60, 32, -40], [40, 0, 60], 70);
  await shot('flood_market', [100, 26, 80], [60, 2, 20], 60);
  scene_of('bantay');
  await shot('quake_fire', [-40, 22, -10], [-66, 3, -33], 60);
  await shot('quake_wire', [150, 16, 4], [150, 3, -26], 60);
  await shot('quake_spill', [-100, 14, 90], [-114, 1, 72], 60);
  await shot('quake_slide', [70, 22, -100], [96, 5, -128], 60);
  await shot('quake_dam', [-20, 12, 95], [-40, 0, 76], 60);
  await shot('quake_tank', [-170, 20, -40], [-200, 10, -62], 60);
  await shot('quake_bridge', [50, 20, 100], [20, 3, 120], 60);
  scene_of('hatid');
  await shot('wind_bridge', [70, 30, 90], [20, 2, 125], 60);
  await shot('wind_roof', [-50, 18, 110], [-68, 6, 90], 60);

  // the operations center, with its ceiling lifted off
  townGroup.visible = false; water.visible = false;
  const eocGroup = new THREE.Group(); scene.add(eocGroup);
  for (const m of eoc.meshes) { const c = show(m); eocGroup.add(c); }
  const E = P.EOC;
  scene.background = new THREE.Color(0x20262b);
  cam.near = 0.05;
  await shot('eoc_inside', [E.x + 0.5, E.floor + 1.6, E.z + 4.6], [E.x - 0.3, E.floor + 1.0, E.z - 2], 80);
  await shot('eoc_dock', [E.x + 3.5, E.floor + 1.6, E.z + 1.5], [E.x + 8, E.floor + 0.9, E.z - 1.5], 75);
  await shot('eoc_pilot', [E.x - 1, E.floor + 1.7, E.z - 3.5], [E.x + 5, E.floor + 0.8, E.z + 3.5], 75);
  eocGroup.visible = false;

  // the props, in a row
  scene.background = new THREE.Color(0xdfe7ea);
  const row = new THREE.Group(); scene.add(row);
  let x = 0;
  for (const name of Object.keys(props)) {
    const g = new THREE.Group();
    for (const m of props[name].meshes) g.add(show(m));
    const box = new THREE.Box3().setFromObject(g), size = new THREE.Vector3(); box.getSize(size);
    const k = name === 'gate' ? 0.12 : name === 'boat' ? 0.5 : 1;
    g.scale.setScalar(k); g.position.x = x + size.x * k / 2; x += size.x * k + 0.35;
    if (name === 'gate') g.position.y = 0.7;
    row.add(g);
  }
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(40, 20), new THREE.MeshLambertMaterial({ color: 0xc9d2d6 })); floor.rotation.x = -Math.PI / 2; row.add(floor);
  await shot('props', [x / 2, 1.5, -4.2], [x / 2, 0.45, 0], 60);
  await shot('drone', [0.9, 1.0, -1.3], [0.45, 0.3, 0], 50);
  renderer.dispose();
  log('pictures saved');
}

run().catch((e) => { log('ERROR ' + (e && e.stack || e)); window.__done && window.__done('error: ' + (e && e.message || e)); });
