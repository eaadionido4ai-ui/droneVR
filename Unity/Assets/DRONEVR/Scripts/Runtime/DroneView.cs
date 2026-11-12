// The drone as the operator sees it, three ways:
//   - at full size in the barangay, with its propellers spinning and its packages hanging from the hooks;
//   - through its camera, on the big screen of the operations center, with the pilot's display over the
//     picture (battery, height, speed, wind, time, the next target);
//   - from inside it, with the FPV goggles on: the view rides on the drone, the same display floats in front
//     of the eyes, and the edges of the view darken while the drone moves fast or turns, for comfort.
// The camera on the drone hangs in a gimbal: it does not tip as the drone leans. On the screen it looks ahead
// and down; in the goggles it looks where the operator turns their head.
using TMPro;
using UnityEngine;

namespace DroneVR.Vr
{
    public sealed class DroneView
    {
        /// <summary>How far down the camera looks when it shows the drone's view on the screen.</summary>
        public const float GimbalPitch = 28f;

        readonly World world;
        readonly Rig rig;
        public readonly DroneModel Model;
        readonly Camera feed;
        readonly RenderTexture picture;
        readonly Panel screen;
        readonly Osd screenOsd, headOsd;
        readonly Transform headHud;
        readonly Renderer vignette;
        Vector3 tilt;
        float vignetteNow;

        public bool Goggles { get; private set; }
        public Lang Lang;

        public DroneView(World world, Rig rig, Transform parent)
        {
            this.world = world; this.rig = rig;
            Model = new DroneModel(world, parent, 1);

            // the drone's camera, and the big screen it is shown on
            picture = new RenderTexture(768, 432, 16) { name = "Drone camera" };
            picture.Create();
            var cam = new GameObject("Drone camera");
            cam.transform.SetParent(parent, false);
            feed = cam.AddComponent<Camera>();
            feed.targetTexture = picture; feed.fieldOfView = 70; feed.nearClipPlane = 0.15f; feed.farClipPlane = 800;
            feed.stereoTargetEye = StereoTargetEyeMask.None;
            feed.enabled = false;
            screen = new Panel(world, null, "Screen", 3.2f, 1.8f, Palette.Dark);
            screen.PlaceOnWall(world.Point("screen"), world.Facing("screen"));
            screen.Picture(picture, 0, 0, 3.2f, 1.8f);
            screenOsd = new Osd(screen.Root.transform, 3.2f, 1.8f, 0.42f, -0.008f);

            // the goggles: a display that floats in front of the eyes, and a dark edge around the view
            headHud = new GameObject("Goggles display").transform;
            headHud.SetParent(rig.Head.transform, false);
            headHud.localPosition = new Vector3(0, -0.02f, 1.25f);
            headOsd = new Osd(headHud, 1.25f, 0.8f, 0.2f, 0);
            vignette = Ui.Quad(rig.Head.transform, VignetteMaterial(world), 0, 0, 1.5f, 1.5f, 0.3f, false);
            vignette.name = "Comfort vignette";
            headHud.gameObject.SetActive(false);
            vignette.enabled = false;
        }

        static Material VignetteMaterial(World world)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float dx = (i + 0.5f) / n - 0.5f, dy = (j + 0.5f) / n - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
                    float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.38f, 0.9f, r));
                    px[j * n + i] = new Color32(0, 0, 0, (byte)(255 * a));
                }
            tex.SetPixels32(px); tex.Apply();
            var m = world.WaterMaterial(new Color(0, 0, 0, 0));
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            return m;
        }

        /// <summary>The goggles on: the view rides on the drone. Off: back in the operations center.</summary>
        public void SetGoggles(bool on)
        {
            Goggles = on;
            headHud.gameObject.SetActive(on);
            vignette.enabled = on;
            if (!on) rig.StopFollowing();
        }

        /// <summary>The camera's view: in the goggles, where the head looks; on the screen, ahead and down.</summary>
        public Aim AimNow(DroneState d)
        {
            if (Goggles) return new Aim(d.Position, Coords.Direction(rig.Head.transform.forward));
            var dir = Coords.Heading(d.Heading) * Quaternion.Euler(GimbalPitch, 0, 0) * Vector3.forward;
            return new Aim(d.Position, Coords.Direction(dir));
        }

        public void Tick(Flight f, float dt, string tool, bool thermal)
        {
            var d = f.Drone;
            // the drone leans into its speed through the air, as a quadcopter does
            float cos = Mathf.Cos((float)(d.Heading * Compass.Deg)), sin = Mathf.Sin((float)(d.Heading * Compass.Deg));
            float fwd = (float)(d.AirVx * sin - d.AirVz * cos), side = (float)(d.AirVx * cos + d.AirVz * sin);
            var want = d.Crashed ? new Vector3(35, 0, 20) : d.Landed ? Vector3.zero : new Vector3(Mathf.Clamp(fwd * 1.4f, -25, 25), 0, Mathf.Clamp(-side * 1.4f, -25, 25));
            tilt = Vector3.Lerp(tilt, want, dt * 4);
            Model.Pose(d, tilt, dt);
            Model.Hang(f.Mission.Cargo.Items);
            Model.Show(!Goggles);

            var cam = Model.Cam;
            var eye = cam != null ? cam.position : Model.Root.transform.position + Vector3.up * 0.2f;
            feed.enabled = !Goggles;
            feed.transform.position = eye;
            feed.transform.rotation = Coords.Heading(d.Heading) * Quaternion.Euler(GimbalPitch, 0, 0);
            if (Goggles) rig.Follow(eye, (float)d.Heading);

            // the display
            var lines = Osd.Lines(f, Lang, tool, thermal, rig.Desktop);
            screenOsd.Show(lines);
            if (Goggles) headOsd.Show(lines);

            // comfort: the edges darken with speed and with turning
            float turn = Mathf.Abs(Mathf.DeltaAngle(lastHeading, (float)d.Heading)) / Mathf.Max(dt, 1e-3f);
            lastHeading = (float)d.Heading;
            float amount = Goggles ? Mathf.Clamp01((float)d.Speed / 10f * 0.6f + turn / 90f * 0.5f) * 0.85f : 0;
            vignetteNow = Mathf.MoveTowards(vignetteNow, amount, dt * 2);
            vignette.sharedMaterial.SetColor("_BaseColor", new Color(1, 1, 1, vignetteNow));
        }
        float lastHeading;

        /// <summary>Between missions: the screen shows the barangay from the drone on its pad.</summary>
        public void Idle(DroneState d)
        {
            Model.Pose(d, Vector3.zero, 0);
            Model.Hang(new string[0]);
            Model.Show(true);
            feed.enabled = false;
            screenOsd.Show(null);
        }

        /// <summary>The pilot's display: the same words on the screen and in the goggles.</summary>
        sealed class Osd
        {
            readonly TextMeshPro topLeft, topRight, bottom, middle, cross;

            public Osd(Transform parent, float w, float h, float size, float z)
            {
                topLeft = Make(parent, -w / 4 + 0.02f, h / 2 - h * 0.17f, w / 2 - 0.08f, h * 0.3f, size, TextAlignmentOptions.TopLeft, z);
                topRight = Make(parent, w / 4 - 0.02f, h / 2 - h * 0.17f, w / 2 - 0.08f, h * 0.3f, size, TextAlignmentOptions.TopRight, z);
                bottom = Make(parent, 0, -h / 2 + h * 0.16f, w - 0.1f, h * 0.28f, size * 0.9f, TextAlignmentOptions.Bottom, z);
                middle = Make(parent, 0, -h * 0.12f, w * 0.8f, h * 0.18f, size * 1.1f, TextAlignmentOptions.Center, z);
                cross = Make(parent, 0, 0, h * 0.2f, h * 0.2f, size * 1.6f, TextAlignmentOptions.Center, z);
                cross.text = "+";
            }

            static TextMeshPro Make(Transform parent, float x, float y, float w, float h, float size, TextAlignmentOptions align, float z)
            {
                var t = Ui.Text(parent, "", x, y, w, h, size, Palette.White, align, true);
                t.transform.localPosition += new Vector3(0, 0, z);
                t.outlineWidth = 0.18f; t.outlineColor = new Color32(0, 0, 0, 200);
                return t;
            }

            public void Show(string[] lines)
            {
                bool on = lines != null;
                topLeft.gameObject.SetActive(on); topRight.gameObject.SetActive(on); bottom.gameObject.SetActive(on); middle.gameObject.SetActive(on); cross.gameObject.SetActive(on);
                if (!on) return;
                topLeft.text = lines[0]; topRight.text = lines[1]; bottom.text = lines[2]; middle.text = lines[3];
                cross.gameObject.SetActive(lines[4] != null);
            }

            /// <summary>The words: top left, top right, bottom, the middle (a warning), and whether to show the cross.</summary>
            public static string[] Lines(Flight f, Lang L, string tool, bool thermal, bool desktop)
            {
                var d = f.Drone; var m = f.Mission;
                string Warn(string s, bool when) { return when ? "<color=" + Palette.Html(Palette.Gold) + ">" + s + "</color>" : s; }
                double above = d.Y - DroneSpec.Gear - m.Town.SurfaceBelow(d.X, d.Y, d.Z);
                string tl = Warn(Strings.Format("hud.battery", L, Mathf.RoundToInt((float)(d.Battery * 100))), d.Battery < DroneSpec.LowBattery) + "\n"
                    + Strings.Format("hud.height", L, Mathf.RoundToInt((float)System.Math.Max(0, above))) + "\n"
                    + Strings.Format("hud.speed", L, ((float)d.Speed).ToString("0.0"));
                double t = m.Time, dx = TownPlan.PadX - d.X, dz = TownPlan.PadZ - d.Z;
                string tr = Strings.Format("hud.wind", L, ((float)m.Weather.SpeedAt(t)).ToString("0"), Compass.Point(m.Weather.FromAt(t))) + "\n"
                    + Warn(Strings.Format("hud.time", L, Strings.Clock(m.Remaining)), m.Remaining < 60) + "\n"
                    + Strings.Format("hud.home", L, Mathf.RoundToInt((float)System.Math.Sqrt(dx * dx + dz * dz)));
                if (m.Cargo.Items.Count > 0) tr += "\n" + Strings.Format("hud.hooks", L, string.Join(", ", m.Cargo.Items.ConvertAll(i => Strings.Get("supply." + i, L))));
                string bottom = Next(f, L);
                if (f.Manual) bottom += "\n<size=70%>" + Strings.Format(desktop ? "hud.controls.desktop" : "hud.controls", L, Strings.Get(tool, L))
                    + (f.Mode == Mode.Hybrid ? " · " + Strings.Get("hud.controls.hybrid", L) : "") + "</size>";
                string middle = d.Crashed ? Strings.Get("console.state.lost", L) : thermal ? Strings.Get("hud.thermal", L) : !f.Manual && f.Auto.Active ? Strings.Get("hud.auto", L) : "";
                if (d.Crashed) middle = "<color=" + Palette.Html(Palette.Red) + ">" + middle + "</color>";
                return new[] { tl, tr, bottom, middle, f.Manual && tool != "tool.none" ? "+" : null };
            }

            /// <summary>Where to go next, and how far: the next ring, a reported hazard, a site waiting for supplies.</summary>
            static string Next(Flight f, Lang L)
            {
                var d = f.Drone; var m = f.Mission;
                System.Func<double, double, int> far = (x, z) => Mathf.RoundToInt((float)System.Math.Sqrt((x - d.X) * (x - d.X) + (z - d.Z) * (z - d.Z)));
                var school = m as FlightSchool;
                if (school != null)
                {
                    if (school.Passed < TownPlan.Gates.Length) { var g = TownPlan.Gates[school.Passed]; return Strings.Format("hud.next.gate", L, school.Passed + 1, Mathf.RoundToInt((float)(new P3(d.X, d.Y, d.Z) - g.Center).Length)); }
                    return Strings.Format("hud.next.pad", L, far(TownPlan.PadX, TownPlan.PadZ));
                }
                var patrol = m as HazardPatrol;
                if (patrol != null)
                {
                    int best = -1; int bd = int.MaxValue;
                    for (int i = 0; i < patrol.Quality.Length; i++)
                        if (patrol.Quality[i] < HazardPatrol.Enough) { int k = far(TownPlan.Hazards[i].ReportX, TownPlan.Hazards[i].ReportZ); if (k < bd) { bd = k; best = i; } }
                    return best < 0 ? Strings.Format("hud.next.pad", L, far(TownPlan.PadX, TownPlan.PadZ)) : Strings.Format("hud.next.report", L, bd);
                }
                var sar = m as SearchRescue;
                if (sar != null) return Strings.Format("hud.next.groups", L, sar.Count, sar.Total);
                var relief = m as ReliefFlights;
                if (relief != null)
                {
                    int best = -1, bd = int.MaxValue;
                    for (int i = 0; i < TownPlan.Sites.Length; i++)
                    {
                        if (relief.StateOf(i) == 2) continue;
                        bool carried = m.Cargo.Items.Count == 0 || relief.StillNeeds(i).Exists(n => m.Cargo.Items.Contains(n));
                        int k = far(TownPlan.Sites[i].X, TownPlan.Sites[i].Z);
                        if (carried && k < bd) { bd = k; best = i; }
                    }
                    if (best < 0) return Strings.Format("hud.next.pad", L, far(TownPlan.PadX, TownPlan.PadZ));
                    var s = TownPlan.Sites[best];
                    string line = Strings.Format("hud.next.site", L, Strings.Capital(Strings.Get("site." + s.Id, L)), bd);
                    if (bd < 20 && !d.Landed)
                    {
                        double h = d.Y - 0.3 - s.Point.Y;
                        line += "\n" + (h <= 4 ? "<color=" + Palette.Html(Palette.Lime) + ">" + Strings.Format("hud.drop.height", L, h.ToString("0.0")) + "</color>"
                            : "<color=" + Palette.Html(Palette.Gold) + ">" + Strings.Format("hud.drop.high", L, h.ToString("0.0")) + "</color>");
                    }
                    return line;
                }
                return "";
            }
        }
    }
}
