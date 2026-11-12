// DRONE VR: the operations center of the barangay, and the drone it sends out.
//
// This component sits in the main scene (Assets/DRONEVR/Scenes/Main Scene) next to the SIMPLE template's
// Connection Manager, Game Manager (DroneSimulationManager) and FPS Player. When the scene starts it loads the
// barangay and the operations center under it, finds the operator, and sets up each part:
//   Ops           the start card, the mission board, the briefings, the reports, the wall boards
//   Desk          the map table and its consoles: the Command Desk
//   Dock          the shelf of supplies and the loading dock
//   DroneView     the drone, its camera on the big screen, and the FPV goggles
//   MissionScene  the barangay during a mission: the disaster's scene, the weather, the people, the boats
// Scenario says what GAMA (or, without GAMA, DroneVR.Core) is doing: the class, the weather, the boats.
//
// A mission is flown one of three ways (Mode), chosen in its briefing:
//   Command Desk  the operator stays at the map table, draws the route and loads the hooks; the autopilot flies.
//   FPV Pilot     the operator sits in the pilot's seat, puts the goggles on and flies with the sticks.
//   Hybrid        the route is flown by the autopilot, and the operator takes the sticks (and the goggles)
//                 when it matters, then hands them back.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace DroneVR.Vr
{
    public sealed class DroneApp : MonoBehaviour
    {
        public static DroneApp Instance { get; private set; }

        public World World { get; private set; }
        public Rig Rig { get; private set; }
        public Pointer Pointer { get; private set; }
        public Scenario Scenario { get; private set; }
        public Ops Ops { get; private set; }
        public Desk Desk { get; private set; }
        public Dock Dock { get; private set; }
        public DroneView View { get; private set; }
        public MissionScene Outside { get; private set; }
        public readonly Operation Operation = new Operation();
        /// <summary>The mission being flown, or null.</summary>
        public Flight Flight { get; private set; }
        public Lang Lang { get { return Operation.Lang; } }
        public bool Goggles { get { return View != null && View.Goggles; } }

        enum Phase { Loading, Start, Board, Brief, Waiting, Flying, Results }
        Phase phase = Phase.Loading;
        /// <summary>The radio's last messages, newest last.</summary>
        readonly List<MissionEvent> radio = new List<MissionEvent>();
        DroneState idle;
        Mode pendingMode = Mode.Hybrid;
        Vector3 roomFeet, roomLook;
        float statusTimer;
        bool wasLinked;

        void Awake() { Instance = this; }

        IEnumerator Start()
        {
            SetLighting();
            var root = new GameObject("DRONE VR world").transform;
            World = new World(root);
            World.LoadPlace("town");
            World.LoadPlace("eoc");
            World.CollectPoints();
            yield return null;

            Rig = gameObject.AddComponent<Rig>();
            Rig.Init();
            if (Rig.Origin == null) yield break;
            Rig.Head.farClipPlane = 1500;
            Sfx.Init(Rig.Head.transform);
            Pointer = gameObject.AddComponent<Pointer>();
            Pointer.Rig = Rig;
            SetUpTeleporting();
            Scenario = new Scenario();
            Scenario.EndCalled += () => { if (Flight != null) Flight.Mission.End(); };

            idle = new DroneState(); idle.ResetOnPad();
            // the drone is a prop of its own, not part of the barangay (whose pieces the thermal camera paints over)
            View = new DroneView(World, Rig, new GameObject("Drone").transform) { Lang = Lang };
            Outside = new MissionScene(World);
            Desk = new Desk(this);
            Dock = new Dock(this);
            Ops = new Ops(this);

            // the operator starts in the operations center, facing the map table (GAMA moves them there too)
            Rig.Place(World.Point("spawn"), World.Facing("spawn"));
            Ops.ShowStart();
            Ops.Refresh();
            phase = Phase.Start;
        }

        /// <summary>Soft, even light from the sky and the ground, and a sun with no shadows: flat and light work for a headset.</summary>
        static void SetLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.skybox = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.shadows = LightShadows.None;
                l.intensity = 0.55f;
                l.transform.rotation = Quaternion.Euler(52, -35, 0);
            }
        }

        /// <summary>The floor of the operations center becomes a place to teleport to, like the template's Teleport Area.</summary>
        void SetUpTeleporting()
        {
            var template = FindFirstObjectByType<TeleportationArea>();
            UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask layers;
            if (template != null) layers = template.interactionLayers;
            else layers = UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");
            // the template's own ground stands for GAMA's world: DRONE VR has its own floor
            if (template != null) template.gameObject.SetActive(false);
            foreach (var floor in World.Floors)
            {
                var area = floor.gameObject.AddComponent<TeleportationArea>();
                area.interactionLayers = layers;
                area.colliders.Clear();
                area.colliders.Add(floor);
                area.enabled = false; area.enabled = true; // take the collider into account
            }
        }

        // ---------- between missions ----------

        public void SetLang(Lang l)
        {
            Operation.Lang = l;
            View.Lang = l;
            Ops.Refresh(); Desk.Refresh(); Dock.Refresh();
            Scenario.Hello(l);
        }

        public void OpenBoard()
        {
            if (phase == Phase.Waiting) Scenario.Leave();
            phase = Phase.Board;
            Ops.ShowBoard();
        }

        public void OpenBrief(string id)
        {
            if (phase == Phase.Waiting) Scenario.Leave();
            phase = Phase.Brief;
            Ops.ShowBrief(id);
        }

        /// <summary>The operator pressed Begin: the mission starts now, or when the class is ready.</summary>
        public void Begin(string id, Mode mode)
        {
            pendingMode = mode;
            Operation.Mode = mode;
            Scenario.Ready(id, mode);
            phase = Phase.Waiting;
            if (Scenario.Start.Go) StartMission();
            else if (Scenario.Gama) Ops.ShowWaiting(Scenario.Start.Ready, Scenario.Start.Players);
        }

        public void Leave()
        {
            Scenario.Leave();
            OpenBoard();
        }

        void StartMission()
        {
            var start = Scenario.Start;
            string id = start.Mission ?? "lipad";
            var mode = phase == Phase.Waiting ? pendingMode : Ops.Chosen;
            Flight = new Flight(id, mode);
            Scenario.Begin(Flight, start.ByGama);
            Outside.Begin(Flight);
            Desk.Show(Flight); Dock.Show(Flight);
            Ops.HideFront();
            radio.Clear();
            Say(Note.Info, "mission.go", "mission." + id + ".name");
            phase = Phase.Flying;
            Rig.Flying = Flight.Manual;
            Rig.CanMove(!Flight.Manual);
            if (mode == Mode.Pilot)
            {
                // to the pilot's seat, and the goggles on
                Rig.Place(World.Point("pilot"), World.Facing("pilot"));
                SetGoggles(true);
            }
            Scenario.Event("start", id + " " + mode.ToString().ToLowerInvariant() + (start.ByGama ? " class" : ""));
        }

        void FinishMission()
        {
            var f = Flight;
            var r = f.Result();
            Operation.Record(f.Mission.Id, f.Mode, r);
            Scenario.Finish(r);
            SetGoggles(false);
            Rig.Flying = false;
            Rig.CanMove(true);
            if (f.Mode == Mode.Pilot) Rig.Place(World.Point("spawn"), World.Facing("spawn"));
            Ops.ShowResults(f, r);
            Ops.Refresh();
            Sfx.Play(r.Stars >= 2 ? "cheer" : "sad");
            Flight = null;
            Outside.Clear();
            Desk.Show(null); Dock.Show(null);
            phase = Phase.Results;
        }

        // ---------- the buttons of the consoles and the radio control ----------

        public void Launch()
        {
            if (Flight == null) return;
            bool wasManual = Flight.Manual;
            if (!Flight.Launch()) { Sfx.Play("sad", 0.5f); return; }
            Sfx.Play("pop");
            if (wasManual) Release();
            Desk.Refresh();
        }

        public void Recall()
        {
            if (Flight == null) return;
            bool wasManual = Flight.Manual;
            if (!Flight.Return()) return;
            Sfx.Play("radio");
            if (wasManual) Release();
            Desk.Refresh();
        }

        /// <summary>Hybrid: take the sticks (and put the goggles on), or hand them back to the autopilot.</summary>
        public void ToggleSticks()
        {
            if (Flight == null || Flight.Mode != Mode.Hybrid) return;
            if (Flight.Manual)
            {
                if (!Flight.HandBack()) return;
                Release();
                Say(Note.Info, "pilot.gave");
            }
            else
            {
                if (!Flight.TakeControl()) return;
                Rig.Flying = true;
                Rig.CanMove(false);
                SetGoggles(true);
                Say(Note.Info, "pilot.took");
            }
            Desk.Refresh();
        }

        /// <summary>The autopilot has the drone again: the operator puts the radio control down and can walk.</summary>
        void Release()
        {
            Rig.Flying = false;
            SetGoggles(false);
            Rig.CanMove(true);
        }

        public void ToggleGoggles()
        {
            if (Flight == null) return;
            if (!Flight.Manual)
            {
                if (Flight.Mode == Mode.Hybrid) ToggleSticks();
                else Say(Note.Warn, "pilot.cannot.desk");
                return;
            }
            SetGoggles(!Goggles);
        }

        void SetGoggles(bool on)
        {
            if (View == null || on == View.Goggles) return;
            if (on) { roomFeet = Rig.Feet; roomLook = Rig.Forward; View.SetGoggles(true); Pointer.On = false; }
            else { View.SetGoggles(false); Rig.Place(roomFeet, roomLook); Pointer.On = true; }
            Sfx.Play("click");
        }

        public void EndMission()
        {
            if (Flight != null) Flight.Mission.End();
        }

        /// <summary>What the drone's camera button does in this mission.</summary>
        string Tool()
        {
            var m = Flight.Mission;
            return m is HazardPatrol ? "tool.photo" : m is SearchRescue ? "tool.mark" : m is ReliefFlights ? "tool.drop" : "tool.none";
        }

        void Shoot()
        {
            var m = Flight.Mission;
            var aim = View.AimNow(Flight.Drone);
            if (m is HazardPatrol) { Sfx.Play("shutter"); Rig.Haptic(0.25f, 0.05f); Flight.Primary(aim); }
            else if (m is SearchRescue) { if (Flight.Primary(aim)) { Sfx.Play("pop"); Rig.Haptic(0.3f, 0.08f); } }
            else if (m is ReliefFlights) Drop();
        }

        void Drop()
        {
            var p = Flight.Release();
            if (p != null) { Sfx.Play("drop"); Rig.Haptic(0.4f, 0.08f); }
        }

        // ---------- the radio ----------

        /// <summary>Something for the radio to say (the app's own messages; the mission's come from its events).</summary>
        public void Say(Note kind, string key, params object[] args) { Heard(new MissionEvent(kind, key, args)); }

        void Heard(MissionEvent e)
        {
            radio.Add(e);
            if (radio.Count > 6) radio.RemoveAt(0);
            if (e.Key.StartsWith("crash.") && e.Key != "crash.spare") { Sfx.Play(e.Key == "crash.water" ? "splash" : "crash"); Rig.Haptic(0.9f, 0.4f); Scenario.Event("crash", e.Key); }
            else if (e.Key == "drop.sunk") Sfx.Play("splash");
            else if (e.Key.StartsWith("battery.") && e.Kind != Note.Good) Sfx.Play("alarm");
            else if (e.Key == "mission.complete" || e.Key.EndsWith(".alldone")) Sfx.Play("cheer");
            else Sfx.Play(e.Kind == Note.Good ? "pop" : e.Kind == Note.Bad ? "sad" : "radio", 0.7f);
        }

        /// <summary>The radio's last messages, for the console.</summary>
        public string RadioLog()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = radio.Count - 1; i >= 0; i--)
                sb.Append("<color=").Append(Palette.Html(Palette.Of(radio[i].Kind))).Append('>').Append(radio[i].Text(Lang)).Append("</color>\n");
            return sb.ToString();
        }

        // ---------- every frame ----------

        void Update()
        {
            if (phase == Phase.Loading || Scenario == null) return;
            float dt = Time.deltaTime;
            Scenario.Tick(dt);
            bool linked = Scenario.Gama;
            if (linked && !wasLinked) Scenario.Hello(Lang);
            wasLinked = linked;

            // the class's mission starts: GAMA says so (or, alone, at once)
            if (phase != Phase.Flying && Scenario.Start.Go) StartMission();

            if (phase == Phase.Flying) Fly(dt);
            else
            {
                View.Idle(idle);
                Sfx.Motor(0, 0);
                Sfx.Weather(0, 0);
            }

            statusTimer -= dt;
            if (statusTimer <= 0)
            {
                statusTimer = 0.5f;
                string line = Strings.Get(linked ? "start.gama.on" : "start.gama.off", Lang);
                Ops.Status(line, Scenario.Start.Ready, Scenario.Start.Players, Scenario.Others.Count);
                if (phase == Phase.Waiting && linked && !Scenario.Start.Go) Ops.ShowWaiting(Scenario.Start.Ready, Scenario.Start.Players);
            }
        }

        void Fly(float dt)
        {
            var f = Flight;
            // the radio control's buttons
            if (f.Manual && Rig.APressed) SetGoggles(!Goggles);
            if (f.Manual && Rig.BPressed) { f.Thermal = !f.Thermal; Sfx.Play("click"); }
            if (f.Mode == Mode.Hybrid && Rig.XPressed) ToggleSticks();
            if (f.CanPlan && Rig.YPressed) Recall();
            if (f.Manual && Pointer.UsedFrame != Time.frameCount && Rig.CameraPressed) Shoot();
            // the grip lets a package go, unless the operator is at the dock taking crates
            bool atDock = Vector3.Distance(Rig.Feet, World.Point("dock")) < 2.5f && !Goggles;
            if (f.Manual && Rig.GripPressed && !atDock) Drop();

            f.Advance(dt, Rig.Sticks());
            while (f.Mission.Events.Count > 0) Heard(f.Mission.Events.Dequeue());

            Outside.SetThermal(f.Thermal);
            Outside.Tick(Scenario, dt);
            View.Tick(f, dt, Tool(), f.Thermal);
            Desk.Tick(Scenario, dt);
            Dock.Tick();

            // the motors are loud in the goggles, quieter through the screen; the wind and the rain the same
            var d = f.Drone;
            float effort = d.Landed || d.Crashed ? 0 : Mathf.Clamp01((float)(d.AirSpeed / DroneSpec.MaxAirSpeed + System.Math.Abs(d.Vy) / DroneSpec.MaxClimb * 0.5));
            Sfx.Motor(d.Landed || d.Crashed ? 0 : Goggles ? 1 : 0.3f, effort);
            float wind = (float)(f.Mission.Weather.SpeedAt(f.Time) / 14);
            Sfx.Weather(Goggles ? wind : wind * 0.25f, f.Mission.Id == "sagip" ? (Goggles ? 1 : 0.3f) : 0);

            if (f.Mission.Over) FinishMission();
        }

        void OnDestroy() { if (Scenario != null) Scenario.Dispose(); }
    }
}
