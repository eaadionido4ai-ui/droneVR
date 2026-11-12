// Loads the barangay and the operations center (Resources/DRONEVR/Models, made by Tools/blender from the plan)
// and gets them ready: the right materials, colliders for the invisible boxes, places to teleport to, the
// switchable sets of each mission's scene, and a list of the named boxes and points the rest of the code looks
// up. It also makes the miniature of the barangay for the map table, and copies of the props (the drone, the
// supplies, the people, the boat). The names are explained in Tools/world-export/src/tag.js.
using System.Collections.Generic;
using UnityEngine;

namespace DroneVR.Vr
{
    public sealed class World
    {
        public const string ModelPath = "DRONEVR/Models/", TexturePath = "DRONEVR/Textures/", MaterialPath = "DRONEVR/Materials/";

        public readonly Transform Root;
        public Material Palette, Picture, Flat, Water;
        /// <summary>The boxes you can point at (HOT__) and drop things into (ZONE__), by id.</summary>
        public readonly Dictionary<string, Collider> Hot = new Dictionary<string, Collider>();
        public readonly Dictionary<string, Collider> Zones = new Dictionary<string, Collider>();
        /// <summary>The anchors (A__), by id: where they are, and the way they face.</summary>
        public readonly Dictionary<string, Vector3> Points = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> Facings = new Dictionary<string, Vector3>();
        /// <summary>The switchable sets (SW__) of the full-size barangay.</summary>
        readonly Dictionary<string, List<GameObject>> sets = new Dictionary<string, List<GameObject>>();
        public readonly List<Collider> Floors = new List<Collider>();
        /// <summary>The boxes you cannot walk through (COL__), by id.</summary>
        public readonly Dictionary<string, Collider> Solids = new Dictionary<string, Collider>();
        /// <summary>What each place is drawn with: the thermal camera paints the barangay's over.</summary>
        public readonly Dictionary<string, List<Renderer>> Drawn = new Dictionary<string, List<Renderer>>();
        readonly Dictionary<string, Material> pictureMats = new Dictionary<string, Material>();
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        readonly Dictionary<Color, Material> flats = new Dictionary<Color, Material>();

        public World(Transform root)
        {
            Root = root;
            Palette = Resources.Load<Material>(MaterialPath + "DronePalette");
            Picture = Resources.Load<Material>(MaterialPath + "DronePicture");
            Flat = Resources.Load<Material>(MaterialPath + "DroneFlat");
            Water = Resources.Load<Material>(MaterialPath + "DroneWater");
            if (Palette == null) Debug.LogError("DRONE VR: the materials are missing from Resources/DRONEVR/Materials (run Tools/make_materials.py).");
        }

        /// <summary>Loads a whole place (the barangay, the operations center) into the world.</summary>
        public GameObject LoadPlace(string name)
        {
            var go = Spawn(name, Root);
            if (go == null) return null;
            var drawn = new List<Renderer>();
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) { Prepare(mr); if (mr.enabled) drawn.Add(mr); }
            Drawn[name] = drawn;
            return go;
        }

        /// <summary>
        /// Makes a copy of a model (a prop, a person) standing at the parent's origin, the right way round.
        /// The model's own calibration points (CAL__O, CAL__X, CAL__Y, CAL__Z) say how the file was turned.
        /// </summary>
        public GameObject Spawn(string name, Transform parent)
        {
            GameObject src;
            if (!prefabs.TryGetValue(name, out src))
            {
                src = Resources.Load<GameObject>(ModelPath + name);
                prefabs[name] = src;
                if (src == null) Debug.LogError("DRONE VR: model not found: Resources/" + ModelPath + name);
            }
            if (src == null) return null;
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var model = Object.Instantiate(src, holder.transform, false);
            model.name = name + " (model)";
            Align(model.transform);
            if (parent != Root) foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true)) PrepareProp(mr);
            return holder;
        }

        static void Align(Transform model)
        {
            Transform o = null, x = null, y = null, z = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CAL__O") o = t; else if (t.name == "CAL__X") x = t; else if (t.name == "CAL__Y") y = t; else if (t.name == "CAL__Z") z = t;
            }
            if (o == null || x == null || y == null || z == null) { Debug.LogWarning("DRONE VR: no calibration points in " + model.name); return; }
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            Vector3 po = model.InverseTransformPoint(o.position);
            Vector3 ex = model.InverseTransformPoint(x.position) - po, ey = model.InverseTransformPoint(y.position) - po, ez = model.InverseTransformPoint(z.position) - po;
            // A maps the file's axes to DRONE VR's axes in Unity: x -> x, y -> y, z -> -z
            var b = Matrix4x4.identity; b.SetColumn(0, ex); b.SetColumn(1, ey); b.SetColumn(2, ez);
            var d = Matrix4x4.identity; d.SetColumn(2, new Vector4(0, 0, -1, 0));
            var a = d * b.inverse;
            Vector3 c0 = a.GetColumn(0), c1 = a.GetColumn(1), c2 = a.GetColumn(2);
            float sx = c0.magnitude, sy = c1.magnitude, sz = c2.magnitude;
            Vector3 r0 = c0 / sx, r1 = c1 / sy, r2 = c2 / sz;
            if (Vector3.Dot(Vector3.Cross(r0, r1), r2) < 0) { r0 = -r0; sx = -sx; }
            model.localRotation = Quaternion.LookRotation(r2, r1);
            model.localScale = new Vector3(sx, sy, sz);
            model.localPosition = -(a.MultiplyPoint3x4(po));
            foreach (var t in new[] { o, x, y, z }) t.gameObject.SetActive(false);
        }

        void Prepare(MeshRenderer mr)
        {
            var go = mr.gameObject;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // flat and bright, and cheaper on a headset
            mr.receiveShadows = false;
            string[] parts = go.name.Split(new[] { "__" }, System.StringSplitOptions.None);
            string kind = parts[0];
            switch (kind)
            {
                case "M":
                    mr.sharedMaterial = Palette; break;
                case "SW":
                    mr.sharedMaterial = Palette; AddToSet(sets, parts[1], go); break;
                case "T":
                    mr.sharedMaterial = PictureMaterial(parts[1]); break;
                case "COL": case "FLOOR": case "HOT": case "ZONE":
                    mr.enabled = false;
                    var box = go.AddComponent<BoxCollider>();
                    string id = parts.Length > 1 ? parts[1] : go.name;
                    if (kind == "FLOOR") Floors.Add(box);
                    if (kind == "COL") Solids[id] = box;
                    if (kind == "HOT") Hot[id] = box;
                    if (kind == "ZONE") Zones[id] = box;
                    break;
                default:
                    mr.enabled = false; break;
            }
        }

        void PrepareProp(MeshRenderer mr)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (mr.name.StartsWith("M__") || mr.name.StartsWith("SW__")) mr.sharedMaterial = Palette;
            else if (mr.name.StartsWith("T__")) mr.sharedMaterial = PictureMaterial(mr.name.Split(new[] { "__" }, System.StringSplitOptions.None)[1]);
            else mr.enabled = false;
        }

        /// <summary>Reads the anchors (A__) of everything loaded so far.</summary>
        public void CollectPoints()
        {
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("A__")) continue;
                string id = t.name.Substring(3);
                if (id.EndsWith("__N")) continue;
                Points[id] = t.position;
                var n = t.parent != null ? t.parent.Find(t.name + "__N") : null;
                if (n != null) { var f = n.position - t.position; Facings[id] = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward; }
            }
        }

        public Vector3 Point(string id)
        {
            Vector3 p;
            if (Points.TryGetValue(id, out p)) return p;
            Debug.LogWarning("DRONE VR: no point " + id);
            return Vector3.zero;
        }

        public Vector3 Facing(string id) { Vector3 f; return Facings.TryGetValue(id, out f) ? f : Vector3.forward; }

        /// <summary>A named child anywhere under a model (an anchor A__..., a piece M__...).</summary>
        public static Transform Find(GameObject model, string name)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        public Material PictureMaterial(string texture)
        {
            Material m;
            if (pictureMats.TryGetValue(texture, out m)) return m;
            m = new Material(Picture) { name = "Picture " + texture };
            var tex = Resources.Load<Texture2D>(TexturePath + texture);
            if (tex == null) Debug.LogWarning("DRONE VR: texture not found: " + texture);
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            pictureMats[texture] = m;
            return m;
        }

        /// <summary>A plain unlit material of one color (shared: do not change its color).</summary>
        public Material FlatMaterial(Color c)
        {
            Material m;
            if (flats.TryGetValue(c, out m)) return m;
            m = new Material(Flat);
            m.SetColor("_BaseColor", c); m.color = c;
            flats[c] = m;
            return m;
        }

        /// <summary>A see-through material of one color (its own: its color can be changed).</summary>
        public Material WaterMaterial(Color c)
        {
            var m = new Material(Water);
            m.SetColor("_BaseColor", c); m.color = c;
            return m;
        }

        static void AddToSet(Dictionary<string, List<GameObject>> to, string set, GameObject go)
        {
            List<GameObject> list;
            if (!to.TryGetValue(set, out list)) to[set] = list = new List<GameObject>();
            list.Add(go);
        }

        /// <summary>Shows the sets of a mission's scene, and hides the others.</summary>
        public void ShowSets(string[] on) { Show(sets, on); }

        static void Show(Dictionary<string, List<GameObject>> all, string[] on)
        {
            var want = new HashSet<string>(on ?? new string[0]);
            foreach (var kv in all) foreach (var go in kv.Value) go.SetActive(want.Contains(kv.Key));
        }

        /// <summary>
        /// A copy of the barangay, made small, for the map table: one metre for every scale metres, the barangay's
        /// middle at the parent's origin. Without what lies beyond the barangay, and without boxes.
        /// </summary>
        public Miniature MakeMiniature(Transform parent, float scale)
        {
            var holder = new GameObject("Miniature");
            holder.transform.SetParent(parent, false);
            holder.transform.localScale = Vector3.one / scale;
            var model = Spawn("town", holder.transform);
            var mini = new Miniature { Root = holder.transform };
            if (model == null) return mini;
            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name == "M__far") { mr.gameObject.SetActive(false); continue; }
                if (mr.name.StartsWith("SW__")) AddToSet(mini.Sets, mr.name.Substring(4), mr.gameObject);
            }
            return mini;
        }

        public sealed class Miniature
        {
            public Transform Root;
            public readonly Dictionary<string, List<GameObject>> Sets = new Dictionary<string, List<GameObject>>();
            public void ShowSets(string[] on) { Show(Sets, on); }
        }
    }
}
