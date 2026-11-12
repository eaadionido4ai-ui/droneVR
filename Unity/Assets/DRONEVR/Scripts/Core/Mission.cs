// A mission: one flight task in the barangay, with its weather, its clock and its score.
//   LIPAD   the flight school: fly through the rings and land on the pad (FlightSchool)
//   BANTAY  environmental safety: find and photograph the hazards after the earthquake (HazardPatrol)
//   SAGIP   search and rescue: find the people stranded by the flood, so the boats can reach them (SearchRescue)
//   HATID   logistics: carry the right supplies to the places that asked, and drop them on the mark (ReliefFlights)
//
// Every mission can be flown in three ways (Mode): from the map table, where you draw a route and the
// autopilot flies it; from the drone itself, with the sticks; or both, taking the sticks when it matters.
// The rules below do not care who flies. They watch the drone, keep the time, change the battery on the pad,
// send a spare drone when one is lost, follow the packages that fall, and give the result.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public enum Mode
    {
        /// <summary>Command Desk: plan on the map table, and the autopilot flies.</summary>
        Desk,
        /// <summary>FPV Pilot: fly from the drone, with the sticks.</summary>
        Pilot,
        /// <summary>Plan on the table, and take the sticks when it matters.</summary>
        Hybrid
    }

    public enum Note { Info, Good, Bad, Warn }

    /// <summary>Something to tell the operator: a key of Strings and what goes in its blanks.</summary>
    public sealed class MissionEvent
    {
        public readonly Note Kind;
        public readonly string Key;
        public readonly object[] Args;
        public MissionEvent(Note kind, string key, params object[] args) { Kind = kind; Key = key; Args = args; }
        public string Text(Lang lang) { return Strings.Format(Key, lang, Strings.Names(Args, lang)); }
    }

    /// <summary>Where the camera is and the way it looks. Dir is one metre long.</summary>
    public struct Aim
    {
        public P3 From, Dir;
        public Aim(P3 from, P3 dir) { From = from; double l = dir.Length; Dir = l > 1e-9 ? dir * (1 / l) : new P3(0, -1, 0); }

        /// <summary>Degrees between the way the camera looks and the way to a point.</summary>
        public double AngleTo(P3 p)
        {
            var to = p - From; double l = to.Length;
            if (l < 1e-6) return 0;
            double c = P3.Dot(Dir, to) / l;
            return Math.Acos(c < -1 ? -1 : c > 1 ? 1 : c) / Compass.Deg;
        }

        /// <summary>The camera of a drone on autopilot: it turns to whatever it is asked to look at.</summary>
        public static Aim At(DroneState d, P3 p) { return new Aim(d.Position, p - d.Position); }
    }

    public sealed class MissionResult
    {
        public int Stars;
        /// <summary>From 0 to 100.</summary>
        public int Score;
        /// <summary>The shares of the score: the task itself, flying safely, and time.</summary>
        public double Tasks, Safety, Pace;
        public readonly List<MissionEvent> Lines = new List<MissionEvent>();

        public List<string> Text(Lang lang)
        {
            var list = new List<string>();
            foreach (var l in Lines) list.Add(l.Text(lang));
            return list;
        }
    }

    public abstract class Mission
    {
        /// <summary>What the score is made of.</summary>
        public const double TaskShare = 0.70, SafetyShare = 0.18, PaceShare = 0.12;
        /// <summary>Seconds before a spare drone stands on the pad after one is lost.</summary>
        public const double SpareSeconds = 5;

        public readonly string Id;
        public readonly MissionPlan Plan;
        public readonly Town Town;
        public readonly Weather Weather;
        public readonly Cargo Cargo = new Cargo();
        public readonly List<Package> Packages = new List<Package>();
        public readonly Queue<MissionEvent> Events = new Queue<MissionEvent>();

        /// <summary>Seconds since the mission began.</summary>
        public double Time { get; protected set; }
        public int Crashes { get; private set; }
        /// <summary>Times the drone did something unsafe that did not break it (flying too close to a fire).</summary>
        public int Strikes { get; protected set; }
        /// <summary>The operator ended the mission, or the time ran out.</summary>
        public bool Ended { get; private set; }
        /// <summary>When every task was done and the drone was back on the pad (seconds), or null.</summary>
        public double? FinishedAt { get; private set; }

        double onPad, sinceCrash;
        bool lowTold, criticalTold;

        protected Mission(string id)
        {
            Id = id; Plan = TownPlan.Mission(id); Town = Town.For(Plan); Weather = new Weather(Plan);
        }

        public static Mission Create(string id)
        {
            switch (id)
            {
                case "lipad": return new FlightSchool();
                case "bantay": return new HazardPatrol();
                case "sagip": return new SearchRescue();
                case "hatid": return new ReliefFlights();
            }
            throw new ArgumentException("no mission " + id);
        }

        public static readonly string[] Ids = { "lipad", "bantay", "sagip", "hatid" };

        public double Seconds { get { return Plan.Minutes * 60; } }
        public double Remaining { get { return Math.Max(0, Seconds - Time); } }
        public bool Over { get { return Ended || FinishedAt != null; } }

        /// <summary>How many things there are to do, and how many are done.</summary>
        public abstract int Total { get; }
        public abstract int Count { get; }
        public bool AllDone { get { return Count >= Total; } }
        /// <summary>The supplies this mission's loading dock offers.</summary>
        public virtual string[] Offers { get { return new string[0]; } }

        protected void Tell(Note kind, string key, params object[] args) { Events.Enqueue(new MissionEvent(kind, key, args)); }

        public void End() { Ended = true; }

        /// <summary>One step of DroneSpec.Dt seconds, after the drone has moved. auto: the autopilot has the sticks.</summary>
        public void Tick(DroneState d, bool auto)
        {
            if (Over) return;
            const double dt = DroneSpec.Dt;
            Time += dt;

            // the packages in the air
            for (int i = 0; i < Packages.Count; i++)
            {
                var p = Packages[i];
                if (!p.Falling) continue;
                double wx, wz;
                Weather.WindAt(Time, p.Y - Terrain.Ground(p.X, p.Z), out wx, out wz);
                if (PackageFall.Step(p, Town, wx, wz, Weather.Level(Time))) PackageLanded(p);
            }

            // a lost drone: a spare one takes its place on the pad, with nothing on its hooks
            if (d.Crashed)
            {
                if (sinceCrash == 0)
                {
                    Crashes++;
                    Tell(Note.Bad, d.Crash == CrashKind.Water ? "crash.water" : d.Crash == CrashKind.HardLanding ? "crash.hard" : "crash." + d.HitBy.ToString().ToLowerInvariant());
                    Cargo.Items.Clear();
                }
                sinceCrash += dt;
                if (sinceCrash >= SpareSeconds) { d.ResetOnPad(); sinceCrash = 0; onPad = 0; lowTold = criticalTold = false; Tell(Note.Info, "crash.spare"); }
            }
            else
            {
                d.PayloadKg = Cargo.Kg;
                // on the pad the battery is changed
                bool home = d.Landed && TownPlan.OnPad(d.X, d.Z);
                onPad = home ? onPad + dt : 0;
                if (home && d.Battery < 0.999 && onPad >= DroneSpec.SwapSeconds) { d.Battery = 1; lowTold = criticalTold = false; Tell(Note.Good, "battery.swapped"); }
                if (!d.Landed && !lowTold && d.Battery < DroneSpec.LowBattery) { lowTold = true; Tell(Note.Warn, "battery.low"); }
                if (!d.Landed && !criticalTold && d.Battery < DroneSpec.CriticalBattery) { criticalTold = true; Tell(Note.Bad, "battery.critical"); }
                Watch(d, auto);
                if (AllDone && home && FinishedAt == null) { FinishedAt = Time; Tell(Note.Good, "mission.complete"); }
            }
            if (Time >= Seconds && !Over) { Ended = true; Tell(Note.Warn, "mission.timeup"); }
        }

        /// <summary>What this mission looks at in each step.</summary>
        protected abstract void Watch(DroneState d, bool auto);

        /// <summary>The camera's button: a photograph, a mark on a survivor. Returns false when there was nothing to do.</summary>
        public virtual bool Primary(DroneState d, Aim aim, bool thermal) { return false; }

        /// <summary>A classmate's drone did this (GAMA says so): it counts for the whole class.</summary>
        public virtual void DoneByClass(int target, double value) { }

        /// <summary>How far each thing to do has come, for GAMA and the map table: 0 open, 1 found, 2 done.</summary>
        public abstract int StateOf(int target);

        // ---------- loading and dropping ----------

        /// <summary>Supplies can be changed only while the drone stands on the pad.</summary>
        public bool CanLoad(DroneState d) { return !Over && d.Landed && !d.Crashed && TownPlan.OnPad(d.X, d.Z); }

        public bool Load(string item, DroneState d)
        {
            if (!CanLoad(d)) { Tell(Note.Warn, "load.pad"); return false; }
            string why = Cargo.WhyNot(item);
            if (why != null) { Tell(Note.Warn, why); return false; }
            Cargo.Add(item); d.PayloadKg = Cargo.Kg;
            return true;
        }

        public bool Unload(string item, DroneState d)
        {
            if (!CanLoad(d) || !Cargo.Remove(item)) return false;
            d.PayloadKg = Cargo.Kg;
            return true;
        }

        /// <summary>Which package to let go here: by default the first one loaded.</summary>
        protected virtual string Choose(DroneState d) { return Cargo.Items[0]; }

        /// <summary>Lets one package go. Returns it, or null if there is none, or the drone is on the ground.</summary>
        public Package Release(DroneState d)
        {
            if (Over || d.Crashed) return null;
            if (Cargo.Items.Count == 0) { Tell(Note.Warn, "drop.empty"); return null; }
            if (d.Landed) { Tell(Note.Warn, "drop.landed"); return null; }
            string item = Choose(d);
            Cargo.Remove(item); d.PayloadKg = Cargo.Kg;
            var p = new Package(item) { X = d.X, Y = d.Y - 0.3, Z = d.Z, Vx = d.Vx, Vy = Math.Min(0, d.Vy), Vz = d.Vz };
            Packages.Add(p);
            return p;
        }

        protected virtual void PackageLanded(Package p)
        {
            if (p.Fate == PackageFate.Sunk) Tell(Note.Bad, "drop.sunk", "supply." + p.Item);
            else if (p.Fate == PackageFate.Broken) Tell(Note.Bad, "drop.broken", "supply." + p.Item);
        }

        // ---------- the result ----------

        /// <summary>From 0 to 1: how much of the task itself was done, and how well.</summary>
        protected abstract double TaskScore(DroneState d);
        /// <summary>The lines of the result that are this mission's own.</summary>
        protected abstract void Describe(MissionResult r, DroneState d);

        public MissionResult Result(DroneState d)
        {
            var r = new MissionResult();
            bool home = d.Landed && !d.Crashed && TownPlan.OnPad(d.X, d.Z);
            r.Tasks = Clamp01(TaskScore(d));
            r.Safety = Clamp01(1 - 0.34 * Crashes - 0.15 * Strikes - (home ? 0 : 0.3));
            // finished with time to spare: full marks up to six tenths of the time, less after
            double used = (FinishedAt ?? Seconds) / Seconds;
            r.Pace = FinishedAt == null ? 0 : Clamp01(used <= 0.6 ? 1 : 1 - (used - 0.6) / 0.4 * 0.7);
            r.Score = (int)Math.Round(100 * (TaskShare * r.Tasks + SafetyShare * r.Safety + PaceShare * r.Pace));
            r.Stars = r.Score >= 85 ? 3 : r.Score >= 60 ? 2 : r.Score >= 35 ? 1 : 0;
            Describe(r, d);
            if (Crashes > 0) r.Lines.Add(new MissionEvent(Note.Bad, "end.crashes", Crashes));
            if (Strikes > 0) r.Lines.Add(new MissionEvent(Note.Warn, "end.strikes", Strikes));
            r.Lines.Add(new MissionEvent(home ? Note.Good : Note.Warn, home ? "end.home" : "end.nothome"));
            if (FinishedAt != null) r.Lines.Add(new MissionEvent(Note.Info, "end.time", Strings.Clock(FinishedAt.Value), Strings.Clock(Seconds)));
            else r.Lines.Add(new MissionEvent(Note.Warn, "end.unfinished"));
            return r;
        }

        protected static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        protected static double Distance(DroneState d, P3 p) { return (d.Position - p).Length; }
    }

    // ------------------------------------------------------------------------------------------------------

    /// <summary>LIPAD, the flight school: take off, fly through the rings in order, and land on the pad.</summary>
    public sealed class FlightSchool : Mission
    {
        public FlightSchool() : base("lipad") { }

        /// <summary>How many rings have been flown through. The next one is Gates[Passed].</summary>
        public int Passed { get; private set; }
        public bool LandedHome { get; private set; }
        bool tookOff;

        public override int Total { get { return TownPlan.Gates.Length + 1; } }
        public override int Count { get { return Passed + (LandedHome ? 1 : 0); } }
        public override int StateOf(int target) { return target < Passed ? 2 : 0; }

        protected override void Watch(DroneState d, bool auto)
        {
            if (!d.Landed) tookOff = true;
            if (Passed < TownPlan.Gates.Length)
            {
                var g = TownPlan.Gates[Passed];
                if (Distance(d, g.Center) <= g.R)
                {
                    Passed++;
                    Tell(Note.Good, Passed < TownPlan.Gates.Length ? "lipad.gate" : "lipad.gates.done", Passed, TownPlan.Gates.Length);
                }
            }
            else if (!LandedHome && tookOff && d.Landed && TownPlan.OnPad(d.X, d.Z)) { LandedHome = true; }
        }

        public override void DoneByClass(int target, double value) { }

        protected override double TaskScore(DroneState d) { return (Passed + (LandedHome ? 2.0 : 0)) / (TownPlan.Gates.Length + 2.0); }

        protected override void Describe(MissionResult r, DroneState d)
        {
            r.Lines.Add(new MissionEvent(Note.Info, "end.lipad.gates", Passed, TownPlan.Gates.Length));
        }
    }

    // ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// BANTAY, the hazard patrol. After the earthquake the residents report dangers: smoke, a fallen wire, a
    /// crack in the bridge. The drone goes to look, and brings back a photograph of each, taken from a safe
    /// distance, sharp enough for the barangay to act on.
    /// </summary>
    public sealed class HazardPatrol : Mission
    {
        /// <summary>A photograph can be taken from this far, at most; it is best between Sharp0 and Sharp1 metres.</summary>
        public const double Range = 45, Sharp0 = 12, Sharp1 = 28;
        /// <summary>The hazard must be within this many degrees of the middle of the picture.</summary>
        public const double Cone = 14;
        /// <summary>The drone must be slower than Steady m/s for a sharp picture; at Blur m/s it is at its worst.</summary>
        public const double Steady = 2, Blur = 8;
        /// <summary>A photograph this good documents the hazard; this good is as good as it needs to be.</summary>
        public const double Enough = 0.5, Fine = 0.8;
        /// <summary>The autopilot's camera frames a hazard by itself, but not as well as a pilot can.</summary>
        public const double AutoFraming = 0.7;

        public HazardPatrol() : base("bantay") { Quality = new double[TownPlan.Hazards.Length]; tooClose = new bool[Quality.Length]; }

        /// <summary>The best photograph of each hazard so far, from 0 to 1.</summary>
        public readonly double[] Quality;
        public int Photos { get; private set; }
        readonly bool[] tooClose;

        public override int Total { get { return Quality.Length; } }
        public override int Count { get { int n = 0; foreach (var q in Quality) if (q >= Enough) n++; return n; } }
        public override int StateOf(int target) { return Quality[target] >= Enough ? 2 : Quality[target] > 0 ? 1 : 0; }

        /// <summary>How good a photograph of a hazard would be from here: 0 if it cannot be taken.</summary>
        public static double QualityOf(HazardSpot h, DroneState d, double angle)
        {
            double dist = Distance(d, h.Point);
            if (dist > Range || angle > Cone) return 0;
            double far = dist <= Sharp1 ? 1 : 1 - 0.6 * (dist - Sharp1) / (Range - Sharp1);
            double near = dist >= Sharp0 ? 1 : dist < h.Near ? 0.5 : 0.8;
            double aim = 1 - 0.5 * angle / Cone;
            double v = Math.Sqrt(d.Vx * d.Vx + d.Vy * d.Vy + d.Vz * d.Vz);
            double steady = v <= Steady ? 1 : v >= Blur ? 0.4 : 1 - 0.6 * (v - Steady) / (Blur - Steady);
            return far * near * aim * steady;
        }

        public override bool Primary(DroneState d, Aim aim, bool thermal)
        {
            if (Over || d.Crashed) return false;
            Photos++;
            int best = -1; double bq = 0;
            for (int i = 0; i < Quality.Length; i++)
            {
                double q = QualityOf(TownPlan.Hazards[i], d, aim.AngleTo(TownPlan.Hazards[i].Point));
                if (q > bq) { bq = q; best = i; }
            }
            if (best < 0) { Tell(Note.Info, "photo.nothing"); return false; }
            Keep(best, bq);
            return true;
        }

        void Keep(int i, double q)
        {
            bool had = Quality[i] >= Enough;
            if (q <= Quality[i]) { Tell(Note.Info, "photo.nobetter", "hazard." + TownPlan.Hazards[i].Id); return; }
            Quality[i] = q;
            Tell(q >= Fine ? Note.Good : q >= Enough ? Note.Info : Note.Warn, q >= Fine ? "photo.fine" : q >= Enough ? "photo.enough" : "photo.poor", "hazard." + TownPlan.Hazards[i].Id, (int)Math.Round(q * 100));
            if (!had && q >= Enough && AllDone) Tell(Note.Good, "bantay.alldone");
        }

        protected override void Watch(DroneState d, bool auto)
        {
            for (int i = 0; i < Quality.Length; i++)
            {
                var h = TownPlan.Hazards[i];
                double dist = Distance(d, h.Point);
                if (!tooClose[i] && !d.Landed && dist < h.Near) { tooClose[i] = true; Strikes++; Tell(Note.Bad, "bantay.tooclose", "hazard." + h.Id); }
                // on autopilot the camera looks for hazards by itself and keeps its best picture
                if (auto && !d.Landed && dist <= Range)
                {
                    double q = QualityOf(h, d, 0) * AutoFraming;
                    if (q > Quality[i] + 0.05 && (q >= Enough || Quality[i] == 0)) { Photos++; Keep(i, q); }
                }
            }
        }

        public override void DoneByClass(int target, double value)
        {
            if (target >= 0 && target < Quality.Length && value > Quality[target]) Quality[target] = value;
        }

        protected override double TaskScore(DroneState d)
        {
            double s = 0;
            foreach (var q in Quality) s += q >= Enough ? 0.6 + 0.4 * Math.Min(1, (q - Enough) / (Fine - Enough)) : 0;
            return s / Quality.Length;
        }

        protected override void Describe(MissionResult r, DroneState d)
        {
            r.Lines.Add(new MissionEvent(Note.Info, "end.bantay.count", Count, Total, Photos));
            for (int i = 0; i < Quality.Length; i++)
                if (Quality[i] < Enough) r.Lines.Add(new MissionEvent(Note.Warn, "end.bantay.missed", "hazard." + TownPlan.Hazards[i].Id));
        }
    }

    // ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// HATID, the relief flights. The typhoon has passed, the wind is still strong and the bridge is out. Four
    /// places have asked for supplies. The drone carries them, a few at a time, and drops each on its mark.
    /// </summary>
    public sealed class ReliefFlights : Mission
    {
        public ReliefFlights() : base("hatid")
        {
            var sites = TownPlan.Sites;
            Got = new bool[sites.Length][]; Accuracy = new double[sites.Length][];
            for (int i = 0; i < sites.Length; i++) { Got[i] = new bool[sites[i].Needs.Length]; Accuracy[i] = new double[sites[i].Needs.Length]; needs += sites[i].Needs.Length; }
        }

        /// <summary>Got[site][need]: that need has been met; Accuracy: how near the mark the package fell, from 0.5 to 1.</summary>
        public readonly bool[][] Got;
        public readonly double[][] Accuracy;
        public int Lost { get; private set; }
        readonly int needs;

        public override string[] Offers { get { return new[] { "water", "food", "medicine", "firstaid", "radio" }; } }
        public override int Total { get { return needs; } }
        public override int Count { get { int n = 0; foreach (var g in Got) foreach (var b in g) if (b) n++; return n; } }
        public override int StateOf(int target) { int n = 0; foreach (var b in Got[target]) if (b) n++; return n == Got[target].Length ? 2 : n > 0 ? 1 : 0; }

        /// <summary>What a site still needs.</summary>
        public List<string> StillNeeds(int site)
        {
            var list = new List<string>();
            for (int k = 0; k < Got[site].Length; k++) if (!Got[site][k]) list.Add(TownPlan.Sites[site].Needs[k]);
            return list;
        }

        /// <summary>Everything the sites still need, all together.</summary>
        public List<string> Outstanding()
        {
            var list = new List<string>();
            for (int i = 0; i < Got.Length; i++) list.AddRange(StillNeeds(i));
            return list;
        }

        /// <summary>The site the drone is over (within twice its radius), or -1.</summary>
        public static int SiteUnder(double x, double z)
        {
            for (int i = 0; i < TownPlan.Sites.Length; i++)
            {
                var s = TownPlan.Sites[i];
                if (Math.Sqrt((s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z)) <= s.R * 2) return i;
            }
            return -1;
        }

        /// <summary>Over a site, the drone lets go of something that site asked for, if it carries it.</summary>
        protected override string Choose(DroneState d)
        {
            int site = SiteUnder(d.X, d.Z);
            if (site >= 0) foreach (var need in StillNeeds(site)) if (Cargo.Items.Contains(need)) return need;
            return Cargo.Items[0];
        }

        public override bool Primary(DroneState d, Aim aim, bool thermal) { return Release(d) != null; }

        protected override void PackageLanded(Package p)
        {
            if (p.Fate != PackageFate.Missed) { Lost++; base.PackageLanded(p); return; }
            // which site did it land in, if any? On the roof means on the roof, not in the water beside the house.
            for (int i = 0; i < TownPlan.Sites.Length; i++)
            {
                var s = TownPlan.Sites[i];
                double off = Math.Sqrt((s.X - p.X) * (s.X - p.X) + (s.Z - p.Z) * (s.Z - p.Z));
                if (off > s.R || Math.Abs(p.Y - s.Point.Y) > 1.5) continue;
                p.Target = i; p.Off = off;
                int k = Array.FindIndex(s.Needs, n => n == p.Item);
                while (k >= 0 && Got[i][k]) k = Array.FindIndex(s.Needs, k + 1, n => n == p.Item);
                if (k < 0) { p.Fate = PackageFate.NotNeeded; Lost++; Tell(Note.Warn, "drop.notneeded", "supply." + p.Item, "site." + s.Id); return; }
                Got[i][k] = true; Accuracy[i][k] = 1 - 0.5 * off / s.R;
                p.Fate = PackageFate.Delivered;
                Tell(Note.Good, "drop.delivered", "supply." + p.Item, "site." + s.Id, off.ToString("0.0"));
                if (AllDone) Tell(Note.Good, "hatid.alldone");
                return;
            }
            Lost++;
            Tell(Note.Bad, "drop.missed", "supply." + p.Item);
        }

        protected override void Watch(DroneState d, bool auto) { }

        public override void DoneByClass(int target, double value)
        {
            // value: the number of the need that a classmate's drone met at this site
            int k = (int)value;
            if (target >= 0 && target < Got.Length && k >= 0 && k < Got[target].Length && !Got[target][k]) { Got[target][k] = true; Accuracy[target][k] = 0.75; }
        }

        protected override double TaskScore(DroneState d)
        {
            double s = 0;
            for (int i = 0; i < Got.Length; i++) for (int k = 0; k < Got[i].Length; k++) if (Got[i][k]) s += 0.5 + 0.5 * Accuracy[i][k] / 1.0;
            return Math.Max(0, s / needs - 0.05 * Lost);
        }

        protected override void Describe(MissionResult r, DroneState d)
        {
            r.Lines.Add(new MissionEvent(Note.Info, "end.hatid.count", Count, Total));
            for (int i = 0; i < Got.Length; i++)
            {
                var left = StillNeeds(i);
                if (left.Count > 0) r.Lines.Add(new MissionEvent(Note.Warn, "end.hatid.waiting", "site." + TownPlan.Sites[i].Id, left.Count));
            }
            if (Lost > 0) r.Lines.Add(new MissionEvent(Note.Warn, "end.hatid.lost", Lost));
        }
    }
}
