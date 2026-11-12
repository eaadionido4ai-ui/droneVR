// The barangay during a mission, at full size: the scene of the mission's disaster (its sets), the sky, the
// water at the flood's level, the rain, the rings of the flight school, the hazards' smoke, the people waiting
// on the roofs and in the trees, the rescue boats, the packages that fall, the other drones of the class, and
// the look of the thermal camera. The drone itself is DroneView's.
using System.Collections.Generic;
using UnityEngine;

namespace DroneVR.Vr
{
    /// <summary>A drone's model: its body, its four propellers that spin, its hooks and what hangs from them.</summary>
    public sealed class DroneModel
    {
        public readonly GameObject Root;
        readonly World world;
        readonly List<Transform> props = new List<Transform>();
        readonly Transform hook, cam;
        readonly List<GameObject> hanging = new List<GameObject>();
        string hangingKey = "";
        float spin;

        public DroneModel(World world, Transform parent, float scale)
        {
            this.world = world;
            Root = world.Spawn("drone", parent);
            Root.transform.localScale = Vector3.one * scale;
            for (int i = 1; i <= 4; i++)
            {
                var piece = World.Find(Root, "M__prop" + i); var at = World.Find(Root, "A__prop" + i);
                if (piece == null || at == null) continue;
                var pivot = new GameObject("spin" + i).transform;
                pivot.SetParent(at.parent, false);
                pivot.position = at.position; pivot.rotation = at.parent.rotation;
                piece.SetParent(pivot, true);
                props.Add(pivot);
            }
            hook = World.Find(Root, "A__hook");
            cam = World.Find(Root, "A__cam");
        }

        /// <summary>The camera's eye, under the drone's nose.</summary>
        public Transform Cam { get { return cam; } }

        /// <summary>Puts the drone where the rules say it is: its feet are its height less the landing gear.</summary>
        public void Pose(DroneState d, Vector3 tilt, float dt)
        {
            Root.transform.position = Coords.ToUnity(d.X, d.Y - DroneSpec.Gear, d.Z);
            Root.transform.rotation = Coords.Heading(d.Heading) * Quaternion.Euler(tilt);
            // propellers: idle on the ground, faster with the effort of flying
            float rate = d.Crashed ? 0 : d.Landed ? 0 : 1600 + 900 * Mathf.Clamp01((float)(d.AirSpeed / DroneSpec.MaxAirSpeed));
            spin += rate * dt;
            for (int i = 0; i < props.Count; i++) props[i].localRotation = Quaternion.Euler(0, (i % 2 == 0 ? spin : -spin), 0);
        }

        /// <summary>What hangs from the hooks: one model of each package, side by side.</summary>
        public void Hang(IList<string> items)
        {
            string key = string.Join(",", items);
            if (key == hangingKey || hook == null) return;
            hangingKey = key;
            foreach (var h in hanging) Object.Destroy(h);
            hanging.Clear();
            for (int k = 0; k < items.Count; k++)
            {
                var m = world.Spawn("supply_" + items[k], hook);
                if (m == null) continue;
                m.transform.localPosition = new Vector3((k - (items.Count - 1) / 2f) * 0.17f, -0.32f, 0);
                hanging.Add(m);
            }
        }

        public void Show(bool on) { Root.SetActive(on); }
    }

    public sealed class MissionScene
    {
        readonly World world;
        readonly Transform root;
        Flight flight;
        Renderer water;
        ParticleSystem rain, smoke;
        Color skyColor;
        readonly List<Renderer> gates = new List<Renderer>();
        readonly List<GameObject> marks = new List<GameObject>();
        readonly List<Group> groups = new List<Group>();
        readonly List<GameObject> boats = new List<GameObject>();
        readonly List<Renderer> beacons = new List<Renderer>();
        readonly Dictionary<Package, GameObject> packages = new Dictionary<Package, GameObject>();
        readonly Dictionary<string, DroneModel> others = new Dictionary<string, DroneModel>();
        // the thermal camera's look
        bool thermal;
        readonly Dictionary<Renderer, Material> saved = new Dictionary<Renderer, Material>();
        Material cold, hot;

        sealed class Group
        {
            public int Index;
            public SurvivorSpot Spot;
            public readonly List<Transform> People = new List<Transform>();
            public Renderer Beacon, Heat;
        }

        public MissionScene(World world)
        {
            this.world = world;
            root = new GameObject("Mission").transform;
            cold = new Material(world.Palette); cold.SetColor("_BaseColor", new Color(0.3f, 0.34f, 0.42f)); cold.color = new Color(0.3f, 0.34f, 0.42f);
            hot = world.FlatMaterial(new Color(1f, 0.93f, 0.62f));
            // the river and the flood: one sheet of water over everything, at the water's level
            water = Ui.Quad(world.Root, world.WaterMaterial(new Color(0.3f, 0.5f, 0.62f, 0.82f)), 0, 0, 2400, 2400, 0, false);
            water.name = "Water";
            water.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Sky("lipad");
        }

        public bool Thermal { get { return thermal; } }

        // ---------- a mission begins and ends ----------

        public void Begin(Flight f)
        {
            Clear();
            flight = f;
            world.ShowSets(f.Mission.Plan.Sets);
            Sky(f.Mission.Id);
            var m = f.Mission;
            if (m is FlightSchool) MakeGates();
            if (m is HazardPatrol) MakeHazards();
            if (m is SearchRescue) MakeSurvivors();
            if (m is ReliefFlights) MakeSites();
            if (m.Id == "sagip") rain = Rain();
        }

        /// <summary>Takes away what the last mission added; the barangay goes back to a calm day.</summary>
        public void Clear()
        {
            SetThermal(false);
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
            gates.Clear(); marks.Clear(); groups.Clear(); boats.Clear(); beacons.Clear(); packages.Clear(); others.Clear();
            rain = null; smoke = null; flight = null;
            world.ShowSets(TownPlan.Mission("lipad").Sets);
            Sky("lipad");
            water.transform.position = Vector3.zero;
        }

        /// <summary>The color of the sky and how far one sees: a clear morning, dust after the earthquake, rain, wind.</summary>
        void Sky(string id)
        {
            skyColor = id == "bantay" ? Palette.Hex(0xc9bfa8) : id == "sagip" ? Palette.Hex(0x8e9aa3) : id == "hatid" ? Palette.Hex(0xa3b4c2) : Palette.Hex(0xa9cfe8);
            float near = id == "sagip" ? 60 : id == "bantay" ? 90 : 160, far = id == "sagip" ? 480 : id == "bantay" ? 600 : 900;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = skyColor;
            RenderSettings.fogStartDistance = near; RenderSettings.fogEndDistance = far;
            float light = id == "sagip" ? 0.72f : id == "hatid" ? 0.85f : 1f;
            RenderSettings.ambientSkyColor = new Color(1f, 0.98f, 0.94f) * 0.95f * light;
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.82f, 0.75f) * 0.9f * light;
            RenderSettings.ambientGroundColor = new Color(0.6f, 0.54f, 0.45f) * 0.9f * light;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Paint(cam);
        }

        /// <summary>A camera sees the sky of the mission (or the dark of the thermal camera).</summary>
        public void Paint(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = thermal ? new Color(0.08f, 0.09f, 0.12f) : skyColor;
        }

        // ---------- every frame ----------

        public void Tick(Scenario scenario, float dt)
        {
            if (flight == null) return;
            var m = flight.Mission;
            double t = m.Time, level = m.Weather.Level(t);
            water.transform.position = new Vector3(0, (float)level, 0);
            var d = flight.Drone;
            if (rain != null)
            {
                double wx, wz; m.Weather.WindAt(t, 20, out wx, out wz);
                rain.transform.position = Coords.ToUnity(d.X, d.Y + 14, d.Z);
                var v = rain.velocityOverLifetime;
                v.x = new ParticleSystem.MinMaxCurve((float)wx); v.y = new ParticleSystem.MinMaxCurve(-14f); v.z = new ParticleSystem.MinMaxCurve((float)-wz);
            }
            if (smoke != null)
            {
                double wx, wz; m.Weather.WindAt(t, 15, out wx, out wz);
                var v = smoke.velocityOverLifetime;
                v.x = new ParticleSystem.MinMaxCurve((float)wx * 0.6f); v.y = new ParticleSystem.MinMaxCurve(2.2f); v.z = new ParticleSystem.MinMaxCurve((float)-wz * 0.6f);
            }
            var school = m as FlightSchool;
            if (school != null)
                for (int i = 0; i < gates.Count; i++)
                    gates[i].sharedMaterial = world.FlatMaterial(i < school.Passed ? Palette.Lime : i == school.Passed ? Palette.Gold : Palette.White);
            var patrol = m as HazardPatrol;
            if (patrol != null)
                for (int i = 0; i < marks.Count && i < patrol.Quality.Length; i++)
                {
                    double q = patrol.Quality[i];
                    marks[i].SetActive(q > 0);
                    if (q > 0) marks[i].GetComponent<Renderer>().sharedMaterial = world.FlatMaterial(q >= HazardPatrol.Enough ? Palette.Lime : Palette.Amber);
                    beacons[i].enabled = q < HazardPatrol.Enough;
                }
            var sar = m as SearchRescue;
            if (sar != null) TickSurvivors(sar, scenario, (float)level, dt);
            var relief = m as ReliefFlights;
            if (relief != null)
                for (int i = 0; i < beacons.Count; i++)
                {
                    int state = relief.StateOf(i);
                    beacons[i].sharedMaterial.SetColor("_BaseColor", state == 2 ? new Color(0.37f, 0.83f, 0.37f, 0.35f) : new Color(1f, 0.48f, 0.1f, 0.45f));
                }
            TickPackages(m, dt);
            TickOthers(scenario, dt);
        }

        // ---------- the flight school ----------

        void MakeGates()
        {
            var g = TownPlan.Gates;
            for (int i = 0; i < g.Length; i++)
            {
                var from = i == 0 ? new P3(TownPlan.PadX, 0, TownPlan.PadZ) : g[i - 1].Center;
                var model = world.Spawn("gate", root);
                model.transform.position = Coords.ToUnity(g[i].Center);
                model.transform.rotation = Coords.Heading(Compass.Towards(from.X, from.Z, g[i].X, g[i].Z));
                var r = model.GetComponentInChildren<MeshRenderer>();
                if (r != null) gates.Add(r);
            }
        }

        // ---------- the hazard patrol ----------

        void MakeHazards()
        {
            foreach (var h in TownPlan.Hazards)
            {
                // a mark over each hazard once it has been photographed, and a beam over the place the residents reported
                var mark = Ui.Ball(root, world.FlatMaterial(Palette.Lime), 1.1f).gameObject;
                mark.transform.position = Coords.ToUnity(h.Point) + Vector3.up * 6;
                mark.SetActive(false);
                marks.Add(mark);
                beacons.Add(Beam(h.ReportX, h.ReportZ, new Color(1f, 0.77f, 0.24f, 0.3f), 40, 1.2f));
            }
            var fire = System.Array.Find(TownPlan.Hazards, h => h.Id == "fire");
            if (fire != null) smoke = Smoke(Coords.ToUnity(fire.Point) + Vector3.up * 2);
        }

        Renderer Beam(double x, double z, Color c, float height, float width)
        {
            var s = Ui.Stick(root, world.WaterMaterial(c), width);
            float g = (float)Terrain.Ground(x, z);
            s.transform.position = Coords.ToUnity(x, g + height / 2, z);
            var sc = s.transform.localScale; sc.y = height; s.transform.localScale = sc;
            return s;
        }

        // ---------- the search and rescue ----------

        static readonly string[] Standing = { "person_red", "person_yellow", "person_blue", "person_pink" };

        void MakeSurvivors()
        {
            for (int i = 0; i < TownPlan.Survivors.Length; i++)
            {
                var s = TownPlan.Survivors[i];
                var g = new Group { Index = i, Spot = s };
                bool sit = s.Where == "raft" || s.Where == "bleachers";
                int n = Mathf.Min(4, s.People);
                for (int k = 0; k < n; k++)
                {
                    var p = world.Spawn(sit ? "person_sit" : Standing[(i + k) % Standing.Length], root);
                    if (p == null) continue;
                    float a = k * Mathf.PI * 2 / Mathf.Max(1, n), r = n == 1 ? 0 : 0.6f;
                    // the man in the tree holds on beside its trunk
                    if (s.Where == "tree") { a = 0; r = 0.5f; }
                    p.transform.position = Coords.ToUnity(s.Point) + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    p.transform.rotation = Quaternion.Euler(0, k * 77 + i * 31, 0);
                    g.People.Add(p.transform);
                }
                // above them, once marked: green (a boat is coming), orange (a life ring first); and in the thermal camera, their warmth
                g.Beacon = Beam(s.X, s.Z, new Color(0.37f, 0.83f, 0.37f, 0.35f), 30, 0.8f);
                g.Beacon.transform.position = Coords.ToUnity(s.Point) + Vector3.up * 17;
                g.Beacon.enabled = false;
                g.Heat = Ui.Ball(root, hot, 1.6f);
                double cover = flight.Mission.Town.SurfaceFromAbove(s.X, s.Z);
                g.Heat.transform.position = Coords.ToUnity(s.X, System.Math.Max(s.Point.Y + 0.8, cover + 0.6), s.Z);
                g.Heat.enabled = false;
                groups.Add(g);
            }
            for (int b = 0; b < RescueFleet.Homes.Length; b++)
            {
                var boat = world.Spawn("boat", root);
                if (boat != null) boats.Add(boat);
            }
        }

        void TickSurvivors(SearchRescue sar, Scenario scenario, float level, float dt)
        {
            var d = flight.Drone;
            var dronePos = Coords.ToUnity(d.X, d.Y, d.Z);
            float now = Time.time;
            foreach (var g in groups)
            {
                bool aboard = sar.Aboard(g.Index);
                bool shownAboard = aboard;
                if (scenario.BoatsFromGama) foreach (var b in scenario.Boats) if (b.Job == g.Index && b.Phase >= 3) shownAboard = true;
                for (int k = 0; k < g.People.Count; k++)
                {
                    var p = g.People[k];
                    p.gameObject.SetActive(!shownAboard);
                    if (shownAboard) continue;
                    // they turn to the drone when it is near, and wave
                    var to = dronePos - p.position; to.y = 0;
                    float want = to.magnitude < 80 && to.sqrMagnitude > 0.01f ? Coords.HeadingOf(to) : p.eulerAngles.y;
                    float sway = Mathf.Sin(now * 3.1f + k + g.Index) * 12;
                    p.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(p.eulerAngles.y, want + sway, 120 * dt), 0);
                    p.GetComponentInChildren<MeshRenderer>().sharedMaterial = thermal ? hot : world.Palette;
                }
                g.Beacon.enabled = sar.Found(g.Index) && !aboard;
                if (g.Beacon.enabled) g.Beacon.sharedMaterial.SetColor("_BaseColor", sar.NeedsRing(g.Index) ? new Color(1f, 0.48f, 0.1f, 0.4f) : new Color(0.37f, 0.83f, 0.37f, 0.35f));
                g.Heat.enabled = thermal && !shownAboard;
            }
            // the boats: GAMA's while they come, otherwise this headset's own
            for (int b = 0; b < boats.Count; b++)
            {
                double x, z, h; bool waiting;
                if (scenario.BoatsFromGama && b < scenario.Boats.Count) { var p = scenario.Boats[b]; x = p.X; z = p.Z; h = p.Heading; waiting = p.Phase == 0; }
                else { var p = sar.Fleet.Boats[b]; x = p.X; z = p.Z; h = p.Heading; waiting = p.Phase == BoatPhase.Waiting; }
                if (waiting) { x += (b - 1) * 4; }
                boats[b].transform.position = Coords.ToUnity(x, level - 0.15, z);
                boats[b].transform.rotation = Quaternion.Slerp(boats[b].transform.rotation, Coords.Heading(h), dt * 4);
            }
        }

        // ---------- the relief flights ----------

        void MakeSites()
        {
            foreach (var s in TownPlan.Sites)
            {
                var beam = Beam(s.X, s.Z, new Color(1f, 0.48f, 0.1f, 0.45f), 45, 0.9f);
                beam.transform.position = Coords.ToUnity(s.Point) + Vector3.up * 26;
                beacons.Add(beam);
            }
        }

        // ---------- what falls, and the other drones ----------

        void TickPackages(Mission m, float dt)
        {
            foreach (var p in m.Packages)
            {
                GameObject go;
                if (!packages.TryGetValue(p, out go))
                {
                    go = world.Spawn("supply_" + p.Item, root);
                    if (go == null) continue;
                    packages[p] = go;
                    go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                }
                var pos = Coords.ToUnity(p.X, p.Y, p.Z);
                if (p.Fate == PackageFate.Sunk)
                {
                    // it sinks out of sight
                    pos = go.transform.position + Vector3.down * dt * 0.4f;
                }
                else if (p.Fate == PackageFate.Broken && go.transform.localScale.y > 0.6f)
                {
                    go.transform.localScale = new Vector3(1.2f, 0.55f, 1.2f);
                    go.transform.rotation = go.transform.rotation * Quaternion.Euler(0, 0, 25);
                }
                go.transform.position = pos;
            }
        }

        void TickOthers(Scenario scenario, float dt)
        {
            var seen = new HashSet<string>();
            foreach (var o in scenario.Others)
            {
                seen.Add(o.Id);
                DroneModel model;
                if (!others.TryGetValue(o.Id, out model)) others[o.Id] = model = new DroneModel(world, root, 1);
                var s = new DroneState { X = o.X, Y = o.Y, Z = o.Z, Heading = o.Heading, Landed = false };
                s.AirVx = 6;
                model.Pose(s, Vector3.zero, dt);
            }
            var gone = new List<string>();
            foreach (var kv in others) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { Object.Destroy(others[id].Root); others.Remove(id); }
        }

        // ---------- the weather's particles ----------

        ParticleSystem Rain()
        {
            var ps = Particles("Rain", world.WaterMaterial(new Color(0.82f, 0.88f, 0.95f, 0.55f)), 1500, 1.4f, 0.025f);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(70, 1, 70);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1; r.velocityScale = 0.06f;
            ps.Play();
            return ps;
        }

        ParticleSystem Smoke(Vector3 at)
        {
            var ps = Particles("Smoke", world.WaterMaterial(new Color(0.33f, 0.34f, 0.36f, 0.5f)), 14, 9f, 2.2f);
            ps.transform.position = at;
            var main = ps.main; main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.2f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        ParticleSystem Particles(string name, Material mat, float rate, float life, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = life; main.startSpeed = 0; main.startSize = size; main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0;
            var emission = ps.emission; emission.rateOverTime = rate;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0); vel.y = new ParticleSystem.MinMaxCurve(-14f); vel.z = new ParticleSystem.MinMaxCurve(0);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        // ---------- the thermal camera ----------

        /// <summary>The thermal camera sees the barangay cold and dark, and people bright with their warmth.</summary>
        public void SetThermal(bool on)
        {
            if (on == thermal) return;
            thermal = on;
            List<Renderer> town;
            if (world.Drawn.TryGetValue("town", out town))
            {
                if (on) foreach (var r in town) { if (r == null) continue; saved[r] = r.sharedMaterial; if (r.sharedMaterial == world.Palette) r.sharedMaterial = cold; }
                else { foreach (var kv in saved) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value; saved.Clear(); }
            }
            water.sharedMaterial.SetColor("_BaseColor", on ? new Color(0.06f, 0.07f, 0.1f, 0.9f) : new Color(0.3f, 0.5f, 0.62f, 0.82f));
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Paint(cam);
        }
    }
}
