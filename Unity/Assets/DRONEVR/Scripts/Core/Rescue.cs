// SAGIP, the search and rescue. The river is over its banks and still rising. People are on roofs, in a tree,
// on a stranded jeepney; some cannot be seen from the sky at all. The drone finds them; the barangay's rescue
// boats go and get them. Every group the drone marks is a boat sent to the right place, in time. One man is
// in fast water, holding on to a tree: a boat can only pull him in once the drone has dropped him a life ring.
//
// The boats are agents: each waits at its landing, takes the job it can reach soonest, follows the flooded
// streets to the group, takes the people aboard and brings them back. GAMA runs the same boats, by the same
// rules (GAMA/models/DRONE.gaml), and shows them to the whole class; the ways they take (TownPlan.BoatRoutes)
// are worked out once, from the plan, for both.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public enum BoatPhase { Waiting, Out, Loading, Back, Unloading }

    public sealed class Boat
    {
        /// <summary>The landing it works from (TownPlan.Staging).</summary>
        public readonly int Home;
        public double X, Z;
        /// <summary>Degrees: 0 faces north, 90 east.</summary>
        public double Heading;
        public BoatPhase Phase = BoatPhase.Waiting;
        /// <summary>The group it is going to, or carrying; -1 when it waits.</summary>
        public int Survivor = -1;
        /// <summary>The second at which it will be back at its landing with nothing more to do.</summary>
        public double FreeAt;
        internal readonly Queue<int> Jobs = new Queue<int>();
        internal double[] Route;
        internal int Leg;
        internal double Timer;

        public Boat(int home)
        {
            Home = home; X = TownPlan.Staging[home][0]; Z = TownPlan.Staging[home][1];
        }
    }

    public sealed class RescueFleet
    {
        /// <summary>Metres per second, and the seconds it takes to get people aboard and ashore.</summary>
        public const double Speed = 6, LoadSeconds = 6, UnloadSeconds = 4;
        /// <summary>The landing of each boat: two at the west landing, one at the east.</summary>
        public static readonly int[] Homes = { 0, 0, 1 };

        public readonly Boat[] Boats;
        /// <summary>For each group: the second its boat took it aboard, and the second it was ashore (NaN until then).</summary>
        public readonly double[] AboardAt, AshoreAt;
        public double Now { get; private set; }

        public RescueFleet()
        {
            Boats = new Boat[Homes.Length];
            for (int i = 0; i < Boats.Length; i++) Boats[i] = new Boat(Homes[i]);
            int n = TownPlan.Survivors.Length;
            AboardAt = new double[n]; AshoreAt = new double[n];
            for (int i = 0; i < n; i++) AboardAt[i] = AshoreAt[i] = double.NaN;
        }

        /// <summary>The length of the way from a landing to a group, in metres.</summary>
        public static double Way(int home, int survivor)
        {
            var r = TownPlan.BoatRoutes[home][survivor];
            double len = 0;
            for (int k = 2; k + 1 < r.Length; k += 2) len += Math.Sqrt((r[k] - r[k - 2]) * (r[k] - r[k - 2]) + (r[k + 1] - r[k - 1]) * (r[k + 1] - r[k - 1]));
            return len;
        }

        /// <summary>
        /// A group has been found: the boat that can be there soonest is given the job. Returns the boat's
        /// number and, in eta, the second it will reach the group.
        /// </summary>
        public int Send(int survivor, out double eta)
        {
            int best = 0; eta = double.MaxValue;
            for (int b = 0; b < Boats.Length; b++)
            {
                double t = Math.Max(Now, Boats[b].FreeAt) + Way(Boats[b].Home, survivor) / Speed;
                if (t < eta - 1e-9) { eta = t; best = b; }
            }
            var boat = Boats[best];
            boat.FreeAt = Math.Max(Now, boat.FreeAt) + 2 * Way(boat.Home, survivor) / Speed + LoadSeconds + UnloadSeconds;
            boat.Jobs.Enqueue(survivor);
            return best;
        }

        public bool Busy
        {
            get { foreach (var b in Boats) if (b.Phase != BoatPhase.Waiting || b.Jobs.Count > 0) return true; return false; }
        }

        /// <summary>Moves the boats for dt seconds.</summary>
        public void Step(double dt)
        {
            Now += dt;
            foreach (var b in Boats)
            {
                switch (b.Phase)
                {
                    case BoatPhase.Waiting:
                        if (b.Jobs.Count == 0) break;
                        b.Survivor = b.Jobs.Dequeue();
                        b.Route = TownPlan.BoatRoutes[b.Home][b.Survivor];
                        b.Leg = 1; b.Phase = BoatPhase.Out;
                        break;
                    case BoatPhase.Out:
                        if (Sail(b, dt, +1)) { b.Phase = BoatPhase.Loading; b.Timer = 0; }
                        break;
                    case BoatPhase.Loading:
                        b.Timer += dt;
                        if (b.Timer >= LoadSeconds) { AboardAt[b.Survivor] = Now; b.Phase = BoatPhase.Back; b.Leg = b.Route.Length / 2 - 2; }
                        break;
                    case BoatPhase.Back:
                        if (Sail(b, dt, -1)) { b.Phase = BoatPhase.Unloading; b.Timer = 0; }
                        break;
                    case BoatPhase.Unloading:
                        b.Timer += dt;
                        if (b.Timer >= UnloadSeconds) { AshoreAt[b.Survivor] = Now; b.Survivor = -1; b.Phase = BoatPhase.Waiting; }
                        break;
                }
            }
        }

        /// <summary>Moves a boat along its way, forwards (+1) or back (-1). Returns true at the end of the way.</summary>
        static bool Sail(Boat b, double dt, int dir)
        {
            double go = Speed * dt;
            int points = b.Route.Length / 2;
            while (go > 0 && b.Leg >= 0 && b.Leg < points)
            {
                double tx = b.Route[2 * b.Leg], tz = b.Route[2 * b.Leg + 1];
                double dx = tx - b.X, dz = tz - b.Z, d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 1e-6) b.Heading = Compass.Towards(b.X, b.Z, tx, tz);
                if (go >= d) { b.X = tx; b.Z = tz; go -= d; b.Leg += dir; }
                else { b.X += dx / d * go; b.Z += dz / d * go; go = 0; }
            }
            return b.Leg < 0 || b.Leg >= points;
        }

        /// <summary>Lets the boats finish every job they have, and returns when the last one is back.</summary>
        public void Finish()
        {
            for (int guard = 0; Busy && guard < 200000; guard++) Step(0.1);
        }
    }

    public sealed class SearchRescue : Mission
    {
        /// <summary>People in the open are seen from this far; hidden ones only by the thermal camera, from this far.</summary>
        public const double SeeRange = 70, ThermalRange = 30;
        /// <summary>To mark a group, it must be within this many degrees of the middle of the picture.</summary>
        public const double Cone = 9;
        /// <summary>On autopilot the camera sees a circle on the ground this many times as wide as the drone is high, up to AutoMax metres.</summary>
        public const double AutoSwath = 0.9, AutoMax = 45;
        /// <summary>On autopilot, hidden people are found only from below AutoThermalAlt metres, within AutoThermalRange.</summary>
        public const double AutoThermalAlt = 26, AutoThermalRange = 18;
        /// <summary>A life ring can be reached if it lands within this many metres of the person.</summary>
        public const double RingReach = 6;

        public SearchRescue() : base("sagip")
        {
            int n = TownPlan.Survivors.Length;
            FoundAt = new double[n]; RingAt = new double[n]; BoatDue = new double[n]; lateTold = new bool[n];
            for (int i = 0; i < n; i++) FoundAt[i] = RingAt[i] = BoatDue[i] = double.NaN;
        }

        public readonly RescueFleet Fleet = new RescueFleet();
        /// <summary>The second each group was marked, and the second a life ring reached it (NaN until then).</summary>
        public readonly double[] FoundAt, RingAt;
        /// <summary>The second by which the boat sent to each group will have it aboard (NaN while no boat is sent).</summary>
        public readonly double[] BoatDue;
        readonly bool[] lateTold;

        public override string[] Offers { get { return new[] { "ring" }; } }
        public override int Total { get { return FoundAt.Length; } }
        public override int Count { get { int n = 0; foreach (var t in FoundAt) if (!double.IsNaN(t)) n++; return n; } }
        public bool Found(int i) { return !double.IsNaN(FoundAt[i]); }
        public bool Aboard(int i) { return !double.IsNaN(Fleet.AboardAt[i]); }
        public override int StateOf(int target) { return Aboard(target) ? 2 : Found(target) ? 1 : 0; }

        public bool Ringed(int i) { return !double.IsNaN(RingAt[i]); }
        /// <summary>A boat has been sent to this group.</summary>
        public bool BoatSent(int i) { return !double.IsNaN(BoatDue[i]); }
        /// <summary>This group is found, and waits for a life ring before a boat can come.</summary>
        public bool NeedsRing(int i) { return TownPlan.Survivors[i].Ring && !Ringed(i) && !Aboard(i); }

        /// <summary>The second by which a group must be aboard a boat.</summary>
        public double Deadline(int i) { return TownPlan.Survivors[i].LimitMinutes * 60; }

        /// <summary>True if the group was taken aboard in time, or will be by the time its boat is due.</summary>
        public bool InTime(int i)
        {
            if (!Found(i) || !BoatSent(i)) return false;
            return (Aboard(i) ? Fleet.AboardAt[i] : BoatDue[i]) <= Deadline(i);
        }

        public bool CanSee(int i, DroneState d, bool thermal)
        {
            var s = TownPlan.Survivors[i];
            double dist = Distance(d, s.Point);
            return s.Hidden ? thermal && dist <= ThermalRange : dist <= SeeRange;
        }

        void Mark(int i, bool byClass)
        {
            if (Found(i)) return;
            FoundAt[i] = Time;
            var s = TownPlan.Survivors[i];
            if (NeedsRing(i)) Tell(Note.Warn, "sagip.needsring", "survivor." + s.Id);
            else
            {
                double eta = SendBoat(i);
                if (!byClass) Tell(Note.Good, "sagip.found", "survivor." + s.Id, s.People, Strings.Clock(Math.Max(0, eta - Time)));
            }
            if (byClass) Tell(Note.Info, "sagip.found.class", "survivor." + s.Id);
            if (AllDone) Tell(Note.Good, "sagip.alldone");
        }

        /// <summary>Sends the boat that can be there soonest. Returns the second it will reach the group.</summary>
        double SendBoat(int i)
        {
            double eta;
            Fleet.Send(i, out eta);
            BoatDue[i] = eta + RescueFleet.LoadSeconds;
            return eta;
        }

        /// <summary>The camera's button: marks the group in the middle of the picture.</summary>
        public override bool Primary(DroneState d, Aim aim, bool thermal)
        {
            if (Over || d.Crashed) return false;
            int best = -1; double ba = Cone; bool hiddenNear = false;
            for (int i = 0; i < FoundAt.Length; i++)
            {
                if (Found(i)) continue;
                var s = TownPlan.Survivors[i];
                double angle = aim.AngleTo(s.Point);
                if (angle > Cone) continue;
                if (!CanSee(i, d, thermal)) { if (s.Hidden && !thermal && Distance(d, s.Point) <= ThermalRange) hiddenNear = true; continue; }
                if (angle <= ba) { ba = angle; best = i; }
            }
            if (best < 0) { Tell(Note.Info, hiddenNear ? "sagip.trythermal" : "sagip.nothing"); return false; }
            Mark(best, false);
            return true;
        }

        protected override void Watch(DroneState d, bool auto)
        {
            Fleet.Step(DroneSpec.Dt);
            for (int i = 0; i < FoundAt.Length; i++)
            {
                var s = TownPlan.Survivors[i];
                // on autopilot the camera recognises people by itself
                if (auto && !d.Landed && !Found(i))
                {
                    double flat = Math.Sqrt((d.X - s.X) * (d.X - s.X) + (d.Z - s.Z) * (d.Z - s.Z));
                    double high = d.Y - s.Point.Y;
                    bool seen = s.Hidden ? high <= AutoThermalAlt && flat <= AutoThermalRange : flat <= Math.Min(AutoMax, AutoSwath * high);
                    if (seen && high > 0) Mark(i, false);
                }
                if (!lateTold[i] && !Aboard(i) && Time > Deadline(i)) { lateTold[i] = true; Tell(Note.Bad, "sagip.late", "survivor." + s.Id); }
            }
        }

        protected override string Choose(DroneState d) { return Cargo.Items.Contains("ring") ? "ring" : Cargo.Items[0]; }

        protected override void PackageLanded(Package p)
        {
            if (p.Item != "ring") { base.PackageLanded(p); return; }
            for (int i = 0; i < FoundAt.Length; i++)
            {
                var s = TownPlan.Survivors[i];
                if (!s.Ring || Ringed(i)) continue;
                double off = Math.Sqrt((s.X - p.X) * (s.X - p.X) + (s.Z - p.Z) * (s.Z - p.Z));
                if (off > RingReach) continue;
                RingAt[i] = Time; p.Fate = PackageFate.Reached; p.Target = i; p.Off = off;
                // now a boat can pull him in: it is sent if he has been marked, or as soon as he is
                if (Found(i) && !BoatSent(i)) Tell(Note.Good, "sagip.ring", "survivor." + s.Id, Strings.Clock(Math.Max(0, SendBoat(i) - Time)));
                else Tell(Note.Good, "sagip.ring.unmarked", "survivor." + s.Id);
                return;
            }
            Tell(Note.Warn, "sagip.ring.missed");
        }

        /// <summary>A classmate found this group (value 1), or dropped it the life ring it needed (value 2).</summary>
        public override void DoneByClass(int target, double value)
        {
            if (target < 0 || target >= FoundAt.Length) return;
            if (value >= 2 && TownPlan.Survivors[target].Ring && !Ringed(target)) { RingAt[target] = Time; if (Found(target) && !BoatSent(target)) SendBoat(target); }
            Mark(target, true);
        }

        /// <summary>The boats' work is part of the result: let them finish what they were sent to do.</summary>
        void Settle() { Fleet.Finish(); }

        protected override double TaskScore(DroneState d)
        {
            Settle();
            double s = 0, all = 0;
            for (int i = 0; i < FoundAt.Length; i++)
            {
                int people = TownPlan.Survivors[i].People;
                all += people;
                if (Found(i)) s += people * (InTime(i) ? 1.0 : 0.4);
            }
            return s / all;
        }

        protected override void Describe(MissionResult r, DroneState d)
        {
            Settle();
            int people = 0, saved = 0, late = 0;
            for (int i = 0; i < FoundAt.Length; i++)
            {
                var s = TownPlan.Survivors[i];
                people += s.People;
                if (!Found(i)) { r.Lines.Add(new MissionEvent(Note.Warn, "end.sagip.missed", "survivor." + s.Id, s.People)); continue; }
                if (InTime(i)) saved += s.People;
                else { late += s.People; r.Lines.Add(new MissionEvent(Note.Warn, NeedsRing(i) ? "end.sagip.noring" : "end.sagip.late", "survivor." + s.Id)); }
            }
            r.Lines.Insert(0, new MissionEvent(Note.Info, "end.sagip.count", Count, Total, saved, people));
        }
    }
}
