// The Command Desk: the map table of the operations center, and the two consoles at its corners.
//
// On the table lies a model of the whole barangay, one metre for every 200, in the mission's scene (rubble
// after the earthquake, water at the flood's level). On it: the drone (made larger, so it can be seen) and the
// drones of the classmates, the route, its waypoints, and what the mission is about: the rings of the flight
// school, the places the residents reported, the groups found and the boats going to them, the sites that asked
// for supplies.
//
// Pointing at the table and pulling the trigger puts a waypoint there, at the height the console shows. Over a
// site that asked for supplies the drone will come down and drop; on a hazard patrol it will stop and look; near
// a ring of the flight school it goes through the ring's middle (Flight.AddWaypoint).
//
// The console (left) shows the drone and the route, and has the buttons: the waypoints' height, undo, clear,
// launch, come home, take the sticks or hand them back, the goggles, and the end of the mission. The other
// console (right) shows what the mission still needs, and the radio's last messages.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DroneVR.Vr
{
    public sealed class Desk
    {
        public static float Scale { get { return (float)TownPlan.MapScale; } }

        readonly DroneApp app;
        readonly World world;
        readonly Transform table;
        readonly World.Miniature mini;
        readonly Renderer water, cursor, cursorStick;
        readonly DroneModel miniDrone;
        readonly Renderer droneStick;
        readonly LineRenderer route;
        readonly Transform marks;
        readonly List<Renderer> waypoints = new List<Renderer>(), waypointSticks = new List<Renderer>();
        readonly Dictionary<string, DroneModel> others = new Dictionary<string, DroneModel>();
        Panel console, needs;
        TextMeshPro status, routeText, altText, cargoText, needsText, radioText, titleText;
        Button undo, clear, launch, home, take, goggles, end, higher, lower;
        // the mission's markers on the table
        readonly List<Renderer> targetMarks = new List<Renderer>();
        readonly List<Transform> boatMarks = new List<Transform>();
        Flight flight;
        int altIndex = 3;
        float refresh;

        Lang L { get { return app.Lang; } }

        public Desk(DroneApp app)
        {
            this.app = app; world = app.World;
            table = new GameObject("Map table").transform;
            table.position = world.Point("table");
            mini = world.MakeMiniature(table, Scale);
            water = Ui.Quad(table, world.WaterMaterial(new Color(0.3f, 0.52f, 0.68f, 0.85f)), 0, 0, 480 / Scale, 360 / Scale, 0, false);
            water.transform.localRotation = Quaternion.Euler(90, 0, 0);
            marks = new GameObject("Marks").transform; marks.SetParent(table, false);

            // the drone, eleven times its size on this map, on a stick down to the ground
            miniDrone = new DroneModel(world, table, 0.055f);
            droneStick = Ui.Stick(table, world.FlatMaterial(Palette.Safety), 0.003f);
            route = Ui.Line(table, world.FlatMaterial(new Color(0.35f, 0.9f, 1f)), 0.004f);
            cursor = Ui.Ball(table, world.FlatMaterial(Palette.Gold), 0.018f);
            cursorStick = Ui.Stick(table, world.FlatMaterial(Palette.Gold), 0.002f);
            cursor.enabled = cursorStick.enabled = false;
            // the pad
            var pad = Disc(TownPlan.PadX, TownPlan.PadZ, 9, Palette.Gold, 0.9f); pad.transform.SetParent(table, true);

            // pointing at the table puts a waypoint there
            Collider top;
            if (world.Solids.TryGetValue("eoc_table", out top))
            {
                var p = top.gameObject.AddComponent<Pointable>();
                p.Reach = 6;
                p.CanPress = () => flight != null && flight.CanPlan && !flight.Mission.Over;
                p.OnPressAt = Pressed;
            }
            else Debug.LogWarning("DRONE VR: no box for the map table.");
            BuildConsole();
            BuildNeeds();
            Show(null);
        }

        // ---------- the map ----------

        /// <summary>Where a point of the barangay lies on the table.</summary>
        public Vector3 ToTable(double x, double y, double z) { return table.position + new Vector3((float)x, (float)y, (float)-z) / Scale; }

        public bool FromTable(Vector3 p, out double x, out double z)
        {
            var d = (p - table.position) * Scale;
            x = d.x; z = -d.z;
            return Terrain.Inside(x, z);
        }

        void Pressed(Vector3 at)
        {
            double x, z;
            if (flight == null || !FromTable(at, out x, out z)) return;
            var w = flight.AddWaypoint(x, z);
            if (w == null) { Sfx.Play("sad", 0.5f); return; }
            Sfx.Play(w.Kind == WaypointKind.Drop ? "drop" : "pop", 0.6f);
            refresh = 0;
        }

        /// <summary>A flat round mark on the map: radius in metres of the barangay.</summary>
        Renderer Disc(double x, double z, double radius, Color c, float alpha)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(d.GetComponent<Collider>());
            d.transform.SetParent(marks, false);
            d.transform.position = ToTable(x, System.Math.Max(0, Terrain.Ground(x, z)) + 0.6, z);
            d.transform.localScale = new Vector3((float)(2 * radius / Scale), 0.0008f, (float)(2 * radius / Scale));
            var r = d.GetComponent<Renderer>();
            r.sharedMaterial = alpha < 1 ? world.WaterMaterial(new Color(c.r, c.g, c.b, alpha)) : world.FlatMaterial(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        Renderer Pin(double x, double y, double z, Color c)
        {
            var b = Ui.Ball(marks, world.FlatMaterial(c), 0.022f);
            b.transform.position = ToTable(x, y, z) + Vector3.up * 0.045f;
            var s = Ui.Stick(b.transform, world.FlatMaterial(c), 0.12f);
            s.transform.localPosition = new Vector3(0, -1.1f, 0); s.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
            return b;
        }

        // ---------- a mission on the table ----------

        /// <summary>Shows a mission on the table, or nothing (null) between missions.</summary>
        public void Show(Flight f)
        {
            flight = f;
            for (int i = marks.childCount - 1; i >= 0; i--) Object.Destroy(marks.GetChild(i).gameObject);
            targetMarks.Clear(); boatMarks.Clear();
            foreach (var w in waypoints) Object.Destroy(w.gameObject);
            foreach (var s in waypointSticks) Object.Destroy(s.gameObject);
            waypoints.Clear(); waypointSticks.Clear();
            mini.ShowSets((f != null ? f.Mission.Plan : TownPlan.Mission("lipad")).Sets);
            route.positionCount = 0;
            console.Visible = needs.Visible = f != null;
            cursor.enabled = cursorStick.enabled = false;
            if (f == null)
            {
                // between missions the drone waits on its pad
                miniDrone.Root.transform.position = ToTable(TownPlan.PadX, TownPlan.PadY, TownPlan.PadZ);
                miniDrone.Root.transform.rotation = Coords.Heading(90);
                Ui.StickBetween(droneStick, miniDrone.Root.transform.position, 0);
                foreach (var o in others.Values) Object.Destroy(o.Root);
                others.Clear();
                return;
            }
            var m = f.Mission;
            if (m is FlightSchool)
                foreach (var g in TownPlan.Gates)
                {
                    var ring = world.Spawn("gate", marks);
                    ring.transform.localScale = Vector3.one * 2.4f / Scale;
                    ring.transform.position = ToTable(g.Center.X, g.Center.Y, g.Center.Z);
                    targetMarks.Add(ring.GetComponentInChildren<MeshRenderer>());
                }
            if (m is HazardPatrol)
                foreach (var h in TownPlan.Hazards)
                {
                    // where the residents said to look; the hazard itself shows once photographed
                    targetMarks.Add(Disc(h.ReportX, h.ReportZ, 16, Palette.Amber, 0.45f));
                    var pin = Pin(h.X, h.Point.Y, h.Z, Palette.Lime); pin.gameObject.SetActive(false); targetMarks.Add(pin);
                }
            if (m is SearchRescue)
            {
                foreach (var s in TownPlan.Survivors) { var pin = Pin(s.X, s.Point.Y, s.Z, Palette.Lime); pin.gameObject.SetActive(false); targetMarks.Add(pin); }
                foreach (var st in TownPlan.Staging) Disc(st[0], st[1], 6, Palette.White, 1);
                for (int b = 0; b < RescueFleet.Homes.Length; b++)
                {
                    var boat = world.Spawn("boat", marks);
                    boat.transform.localScale = Vector3.one * 6f / Scale;
                    boatMarks.Add(boat.transform);
                }
            }
            if (m is ReliefFlights)
                foreach (var s in TownPlan.Sites) targetMarks.Add(Disc(s.X, s.Z, s.R * 2.5, Palette.Safety, 1));
            altIndex = System.Array.IndexOf(Autopilot.Altitudes, f.WaypointAlt);
            if (altIndex < 0) { altIndex = 3; f.WaypointAlt = Autopilot.Altitudes[altIndex]; }
            refresh = 0;
        }

        public void Tick(Scenario scenario, float dt)
        {
            if (flight == null) return;
            var m = flight.Mission; var d = flight.Drone;
            double level = m.Weather.Level(m.Time);
            water.transform.position = table.position + Vector3.up * ((float)level / Scale + 0.0005f);
            // the drone
            var at = ToTable(d.X, d.Y - DroneSpec.Gear, d.Z);
            miniDrone.Root.transform.position = at; miniDrone.Root.transform.rotation = Coords.Heading(d.Heading);
            miniDrone.Show(true);
            float ground = (float)Terrain.Ground(d.X, d.Z) / Scale + table.position.y;
            Ui.StickBetween(droneStick, new Vector3(at.x, ground, at.z), Mathf.Max(0, at.y - ground));
            Others(scenario);
            // the route: from the drone (or the pad) through the waypoints ahead, and home
            var ahead = flight.Ahead();
            Sync(ahead);
            var pts = new List<Vector3> { at + Vector3.up * 0.01f };
            for (int i = 0; i < ahead.Count; i++) pts.Add(waypoints[i].transform.position);
            if (ahead.Count > 0) pts.Add(ToTable(TownPlan.PadX, TownPlan.PadY + Autopilot.ReturnAlt, TownPlan.PadZ));
            route.positionCount = pts.Count > 1 ? pts.Count : 0;
            if (pts.Count > 1) route.SetPositions(pts.ToArray());
            // where the pointer is on the table
            var p = app.Pointer;
            double cx, cz;
            Collider top;
            bool onTable = p != null && p.On && world.Solids.TryGetValue("eoc_table", out top) && p.HitCollider == top && flight.CanPlan && FromTable(p.HitPoint, out cx, out cz);
            cursor.enabled = cursorStick.enabled = onTable;
            if (onTable)
            {
                FromTable(p.HitPoint, out cx, out cz);
                var foot = ToTable(cx, Terrain.Ground(cx, cz), cz);
                cursor.transform.position = ToTable(cx, Terrain.Ground(cx, cz) + flight.WaypointAlt, cz);
                Ui.StickBetween(cursorStick, foot, cursor.transform.position.y - foot.y);
            }
            Targets(scenario);
            refresh -= dt;
            if (refresh <= 0) { refresh = 0.25f; Refresh(); }
        }

        void Sync(List<Waypoint> ahead)
        {
            while (waypoints.Count < ahead.Count) { waypoints.Add(Ui.Ball(table, world.FlatMaterial(Palette.White), 0.02f)); waypointSticks.Add(Ui.Stick(table, world.FlatMaterial(Palette.White), 0.0025f)); }
            while (waypoints.Count > ahead.Count) { Object.Destroy(waypoints[waypoints.Count - 1].gameObject); waypoints.RemoveAt(waypoints.Count - 1); Object.Destroy(waypointSticks[waypointSticks.Count - 1].gameObject); waypointSticks.RemoveAt(waypointSticks.Count - 1); }
            for (int i = 0; i < ahead.Count; i++)
            {
                var w = ahead[i];
                var c = w.Kind == WaypointKind.Drop ? Palette.Safety : w.Kind == WaypointKind.Hover ? Palette.Gold : Palette.White;
                waypoints[i].sharedMaterial = world.FlatMaterial(c);
                waypoints[i].transform.position = ToTable(w.X, w.Y, w.Z);
                waypointSticks[i].sharedMaterial = world.FlatMaterial(c);
                var foot = ToTable(w.X, Terrain.Ground(w.X, w.Z), w.Z);
                Ui.StickBetween(waypointSticks[i], foot, waypoints[i].transform.position.y - foot.y);
            }
        }

        void Others(Scenario scenario)
        {
            var seen = new HashSet<string>();
            foreach (var o in scenario.Others)
            {
                seen.Add(o.Id);
                DroneModel model;
                if (!others.TryGetValue(o.Id, out model)) others[o.Id] = model = new DroneModel(world, table, 0.045f);
                model.Root.transform.position = ToTable(o.X, o.Y - DroneSpec.Gear, o.Z);
                model.Root.transform.rotation = Coords.Heading(o.Heading);
            }
            var gone = new List<string>();
            foreach (var kv in others) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { Object.Destroy(others[id].Root); others.Remove(id); }
        }

        void Targets(Scenario scenario)
        {
            var m = flight.Mission;
            var school = m as FlightSchool;
            if (school != null)
                for (int i = 0; i < targetMarks.Count; i++)
                    if (targetMarks[i] != null) targetMarks[i].sharedMaterial = world.FlatMaterial(i < school.Passed ? Palette.Lime : i == school.Passed ? Palette.Gold : Palette.White);
            var patrol = m as HazardPatrol;
            if (patrol != null)
                for (int i = 0; i < patrol.Quality.Length; i++)
                {
                    double q = patrol.Quality[i];
                    targetMarks[2 * i].sharedMaterial.SetColor("_BaseColor", q >= HazardPatrol.Enough ? new Color(0.37f, 0.83f, 0.37f, 0.35f) : new Color(0.95f, 0.64f, 0.23f, 0.45f));
                    targetMarks[2 * i + 1].gameObject.SetActive(q > 0);
                    if (q > 0) targetMarks[2 * i + 1].sharedMaterial = world.FlatMaterial(q >= HazardPatrol.Enough ? Palette.Lime : Palette.Amber);
                }
            var sar = m as SearchRescue;
            if (sar != null)
            {
                for (int i = 0; i < targetMarks.Count; i++)
                {
                    targetMarks[i].gameObject.SetActive(sar.Found(i));
                    targetMarks[i].sharedMaterial = world.FlatMaterial(sar.Aboard(i) ? Palette.Grey : sar.NeedsRing(i) ? Palette.Safety : Palette.Lime);
                }
                double level = m.Weather.Level(m.Time);
                for (int b = 0; b < boatMarks.Count; b++)
                {
                    double x, z, h;
                    if (scenario.BoatsFromGama && b < scenario.Boats.Count) { x = scenario.Boats[b].X; z = scenario.Boats[b].Z; h = scenario.Boats[b].Heading; }
                    else { var boat = sar.Fleet.Boats[b]; x = boat.X + (boat.Phase == BoatPhase.Waiting ? (b - 1) * 4 : 0); z = boat.Z; h = boat.Heading; }
                    boatMarks[b].position = ToTable(x, level, z);
                    boatMarks[b].rotation = Coords.Heading(h);
                }
            }
            var relief = m as ReliefFlights;
            if (relief != null)
                for (int i = 0; i < targetMarks.Count; i++)
                {
                    int s = relief.StateOf(i);
                    targetMarks[i].sharedMaterial = world.FlatMaterial(s == 2 ? Palette.Lime : s == 1 ? Palette.Gold : Palette.Safety);
                }
        }

        // ---------- the console ----------

        void BuildConsole()
        {
            console = new Panel(world, null, "Console", 1.0f, 0.86f, Palette.Navy);
            console.PlaceOnWall(world.Point("console"), world.Facing("console"));
            console.Tilt(-28);
            titleText = console.Text("", 0, 0.375f, 0.94f, 0.08f, 0.34f, Palette.Gold, TextAlignmentOptions.Left, true);
            status = console.Text("", 0, 0.235f, 0.94f, 0.2f, 0.24f, Palette.Cream);
            routeText = console.Text("", 0, 0.085f, 0.94f, 0.1f, 0.22f, Palette.White);
            lower = console.Button("", -0.38f, -0.01f, 0.2f, 0.07f, Palette.Teal, () => Alt(-1));
            altText = console.Text("", 0, -0.01f, 0.5f, 0.07f, 0.24f, Palette.White, TextAlignmentOptions.Center, true);
            higher = console.Button("", 0.38f, -0.01f, 0.2f, 0.07f, Palette.Teal, () => Alt(1));
            undo = console.Button("", -0.36f, -0.11f, 0.22f, 0.08f, Palette.Grey, () => { if (flight != null && flight.UndoWaypoint()) Sfx.Play("drop", 0.5f); refresh = 0; });
            clear = console.Button("", -0.12f, -0.11f, 0.22f, 0.08f, Palette.Grey, () => { if (flight != null) flight.ClearRoute(); refresh = 0; });
            launch = console.Button("", 0.12f, -0.11f, 0.22f, 0.08f, Palette.Green, () => app.Launch());
            home = console.Button("", 0.36f, -0.11f, 0.22f, 0.08f, Palette.Orange, () => app.Recall());
            take = console.Button("", -0.3f, -0.21f, 0.34f, 0.08f, Palette.Teal, () => app.ToggleSticks());
            goggles = console.Button("", 0.05f, -0.21f, 0.3f, 0.08f, Palette.Teal, () => app.ToggleGoggles());
            end = console.Button("", 0.34f, -0.21f, 0.24f, 0.08f, Palette.Red, () => app.EndMission());
            cargoText = console.Text("", 0, -0.33f, 0.94f, 0.14f, 0.2f, Palette.Cream);
        }

        void Alt(int step)
        {
            if (flight == null) return;
            altIndex = Mathf.Clamp(altIndex + step, 0, Autopilot.Altitudes.Length - 1);
            flight.WaypointAlt = Autopilot.Altitudes[altIndex];
            refresh = 0;
        }

        void BuildNeeds()
        {
            needs = new Panel(world, null, "Needs", 1.0f, 0.86f, Palette.Paper);
            needs.PlaceOnWall(world.Point("needs"), world.Facing("needs"));
            needs.Tilt(-28);
            needsText = needs.Text("", 0, 0.16f, 0.94f, 0.5f, 0.24f, Palette.Navy);
            needs.Box(0, -0.205f, 1.0f, 0.42f, Palette.Navy);
            radioText = needs.Text("", 0, -0.205f, 0.94f, 0.39f, 0.2f, Palette.Cream);
        }

        /// <summary>Writes the consoles again (four times a second, or at once after a press).</summary>
        public void Refresh()
        {
            if (flight == null) return;
            var m = flight.Mission; var d = flight.Drone;
            titleText.text = Strings.Get("mission." + m.Id + ".name", L) + " · " + Strings.Get("mode." + flight.Mode.ToString().ToLowerInvariant(), L);
            double t = m.Time;
            string state = d.Crashed ? "console.state.lost" : flight.Manual ? "console.state.manual" : flight.Auto.Active
                ? (flight.Auto.Waiting ? "console.state.wait" : flight.Auto.Returning ? (flight.Auto.LowBatteryReturn ? "console.state.homelow" : "console.state.home")
                   : flight.Auto.Phase == PilotPhase.Descend || flight.Auto.Phase == PilotPhase.Settle || flight.Auto.Phase == PilotPhase.Release ? "console.state.drop" : "console.state.auto")
                : d.Landed && TownPlan.OnPad(d.X, d.Z) ? "console.state.pad" : "console.state.ground";
            status.text = Strings.Format("console.status", L, Mathf.RoundToInt((float)(d.Battery * 100)), Mathf.RoundToInt((float)System.Math.Max(0, d.AboveGround - DroneSpec.Gear)), ((float)d.Speed).ToString("0.0")) + "\n"
                + Strings.Format("console.wind", L, ((float)m.Weather.SpeedAt(t)).ToString("0"), Strings.Direction(m.Weather.FromAt(t), L))
                + (m.Weather.Level(t) > 0.05 ? " · " + Strings.Format("console.flood", L, m.Weather.Level(t).ToString("0.0")) : "") + "\n"
                + Strings.Format("console.time", L, Strings.Clock(m.Remaining)) + " · " + Task(m) + "\n"
                + Strings.Format(state, L, flight.Auto.Next + 1, flight.Auto.Route.Count);
            if (!flight.CanPlan) routeText.text = Strings.Get("console.route.pilot", L);
            else if (flight.Ahead().Count == 0) routeText.text = Strings.Get("console.route.none", L);
            else
            {
                var e = flight.Estimate();
                routeText.text = Strings.Format("console.route", L, flight.Ahead().Count, Mathf.RoundToInt((float)e.Metres), Strings.Clock(e.Seconds), Mathf.RoundToInt((float)(e.Battery * 100)))
                    + (e.Battery > d.Battery - Autopilot.Reserve ? "\n<color=" + Palette.Html(Palette.Gold) + ">" + Strings.Get("console.route.toofar", L) + "</color>" : "");
            }
            altText.text = Strings.Format("console.alt", L, flight.WaypointAlt);
            lower.Text = "-"; higher.Text = "+";
            undo.Text = Strings.Get("btn.undo", L); clear.Text = Strings.Get("btn.clear", L);
            launch.Text = Strings.Get("btn.launch", L); home.Text = Strings.Get("btn.recall", L);
            take.Text = Strings.Get(flight.Manual ? "btn.give" : "btn.take", L);
            goggles.Text = Strings.Get(app.Goggles ? "btn.view.screen" : "btn.fly", L);
            end.Text = Strings.Get("btn.end", L);
            bool plan = flight.CanPlan && !m.Over;
            foreach (var b in new[] { lower, higher, undo, clear, home }) b.Enabled(plan);
            launch.Enabled(plan && flight.Auto.Route.Count > 0 && (!flight.Auto.Active || flight.Manual));
            take.Visible = flight.Mode == Mode.Hybrid;
            goggles.Enabled(flight.Manual || flight.Mode == Mode.Hybrid);
            var items = m.Cargo.Items;
            cargoText.text = items.Count == 0 ? Strings.Get("console.cargo.none", L)
                : Strings.Format("console.cargo", L, string.Join(", ", items.ConvertAll(i => Strings.Get("supply." + i, L))), m.Cargo.Kg.ToString("0"), DroneSpec.MaxPayloadKg.ToString("0"));
            needsText.text = Needs(m);
            radioText.text = app.RadioLog();
        }

        string Task(Mission m)
        {
            var sar = m as SearchRescue;
            if (sar != null) { int aboard = 0; for (int i = 0; i < sar.Total; i++) if (sar.Aboard(i)) aboard++; return Strings.Format("console.tasks.sagip", L, sar.Count, sar.Total, aboard); }
            return Strings.Format("console.tasks." + m.Id, L, m.Count, m.Total);
        }

        /// <summary>The right console: what the mission still needs. (The font has no tick or arrow: + is done, > on its way, ! needs something.)</summary>
        string Needs(Mission m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(Strings.Get("mission." + m.Id + ".title", L)).Append("</b>\n");
            var patrol = m as HazardPatrol;
            if (patrol != null)
            {
                sb.Append("<size=85%>").Append(Strings.Get("report.title", L)).Append("</size>\n");
                for (int i = 0; i < patrol.Quality.Length; i++)
                {
                    var h = TownPlan.Hazards[i];
                    string mark = patrol.Quality[i] >= HazardPatrol.Enough ? "<color=#128253>+</color> " : patrol.Quality[i] > 0 ? "~ " : "· ";
                    sb.Append(mark).Append(patrol.Quality[i] > 0 ? Strings.Capital(Strings.Get("hazard." + h.Id, L)) + " " + Mathf.RoundToInt((float)(patrol.Quality[i] * 100)) + "%" : "?").Append('\n');
                }
                return sb.ToString();
            }
            var sar = m as SearchRescue;
            if (sar != null)
            {
                for (int i = 0; i < sar.Total; i++)
                {
                    if (!sar.Found(i)) continue;
                    var s = TownPlan.Survivors[i];
                    string st = sar.Aboard(i) ? "<color=#128253>+</color> " : sar.NeedsRing(i) ? "<color=#d9650a>!</color> " : "> ";
                    sb.Append(st).Append(Strings.Capital(Strings.Get("survivor." + s.Id, L))).Append(" (").Append(s.People).Append(")\n");
                }
                if (sar.Count == 0) sb.Append(Strings.Get("mission.sagip.tip", L));
                return sb.ToString();
            }
            var relief = m as ReliefFlights;
            if (relief != null)
            {
                for (int i = 0; i < TownPlan.Sites.Length; i++)
                {
                    var left = relief.StillNeeds(i);
                    string name = Strings.Capital(Strings.Get("site." + TownPlan.Sites[i].Id, L));
                    sb.Append(left.Count == 0 ? "<color=#128253>+</color> " + Strings.Format("need.done", L, name) : "· " + Strings.Format("need.line", L, name, string.Join(", ", left.ConvertAll(n => Strings.Get("supply." + n, L) + " " + TownPlan.Supply(n).Kg + " kg")))).Append('\n');
                }
                return sb.ToString();
            }
            sb.Append(Strings.Get("mission." + m.Id + ".task", L)).Append("\n\n<size=85%>").Append(Strings.Get("mission." + m.Id + ".tip", L)).Append("</size>");
            return sb.ToString();
        }
    }
}
