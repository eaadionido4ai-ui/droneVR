// One mission being flown: the drone, the autopilot, the mission's rules and its weather, stepped together.
// Unity gives it the pilot's sticks and the time that has passed; it does the rest. It is also what the tests
// fly, without Unity.
//
// The three ways to fly (Mode):
//   Desk    the operator stays at the map table: draws the route, loads the hooks, launches. The autopilot flies.
//   Pilot   the operator flies from the drone (FPV) from take-off to landing. No autopilot.
//   Hybrid  the route is drawn and flown as on the desk, and the operator takes the sticks at any moment,
//           then hands them back: the autopilot goes on from where the drone is.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public sealed class Flight
    {
        /// <summary>A route has at most this many waypoints.</summary>
        public const int MaxWaypoints = 24;
        /// <summary>A waypoint put down this near a ring of the flight school goes through its middle.</summary>
        public const double GateSnap = 12;
        /// <summary>The autopilot drops a life ring this many metres north of the person it is for.</summary>
        public const double RingAside = 3.6;

        public readonly Mission Mission;
        public readonly Mode Mode;
        public readonly DroneState Drone = new DroneState();
        public readonly Autopilot Auto = new Autopilot();
        /// <summary>The pilot has the sticks.</summary>
        public bool Manual { get; private set; }
        /// <summary>The thermal camera is on.</summary>
        public bool Thermal;
        /// <summary>The height given to the next waypoint put on the map, in metres above the ground.</summary>
        public double WaypointAlt = 30;

        double accum;

        public Flight(string mission, Mode mode)
        {
            Mission = Mission.Create(mission); Mode = mode;
            Drone.ResetOnPad();
            Manual = mode == Mode.Pilot;
        }

        public bool CanPlan { get { return Mode != Mode.Pilot; } }
        public bool CanFly { get { return Mode != Mode.Desk; } }
        public double Time { get { return Mission.Time; } }

        // ---------- the map table ----------

        /// <summary>
        /// Puts a waypoint on the route at a point of the map. What the drone does there depends on the place:
        /// over a site that asked for supplies it comes down and drops; on a hazard patrol it stops to look;
        /// near a ring of the flight school it flies through the ring. Returns null if it cannot be added.
        /// </summary>
        public Waypoint AddWaypoint(double x, double z)
        {
            if (!CanPlan || Mission.Over || Auto.Route.Count >= MaxWaypoints || !Terrain.Inside(x, z)) return null;
            var w = new Waypoint(x, z, WaypointAlt);
            if (Mission is ReliefFlights)
            {
                int site = ReliefFlights.SiteUnder(x, z);
                if (site >= 0) { w.X = TownPlan.Sites[site].X; w.Z = TownPlan.Sites[site].Z; w.Kind = WaypointKind.Drop; }
            }
            else if (Mission is SearchRescue)
            {
                var sar = (SearchRescue)Mission;
                for (int i = 0; i < TownPlan.Survivors.Length; i++)
                {
                    var s = TownPlan.Survivors[i];
                    // a life ring is dropped beside the person, clear of the tree he holds on to
                    if (s.Ring && sar.Found(i) && Math.Sqrt((s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z)) < 12) { w.X = s.X; w.Z = s.Z - RingAside; w.Kind = WaypointKind.Drop; }
                }
            }
            else if (Mission is HazardPatrol) w.Kind = WaypointKind.Hover;
            else if (Mission is FlightSchool)
            {
                foreach (var g in TownPlan.Gates)
                    if (Math.Sqrt((g.X - x) * (g.X - x) + (g.Z - z) * (g.Z - z)) < GateSnap) { w.X = g.X; w.Z = g.Z; w.Alt = g.Y; }
            }
            Auto.Add(w);
            return w;
        }

        public bool UndoWaypoint() { return CanPlan && Auto.Undo(); }
        public void ClearRoute() { if (CanPlan) Auto.ClearAhead(Drone); }

        /// <summary>The waypoints still to fly.</summary>
        public List<Waypoint> Ahead()
        {
            var list = new List<Waypoint>();
            for (int i = Auto.Active ? Auto.Next : 0; i < Auto.Route.Count; i++) list.Add(Auto.Route[i]);
            return list;
        }

        /// <summary>What the route on the table will cost, from the pad and back, with what is on the hooks.</summary>
        public RouteEstimate Estimate()
        {
            var kg = new List<double>();
            foreach (var item in Mission.Cargo.Items) kg.Add(TownPlan.Supply(item).Kg);
            return RouteEstimate.For(Ahead(), Mission.Cargo.Kg, kg, Mission.Weather.MeanSpeed, Mission.Plan.WindFrom);
        }

        /// <summary>Sends the drone on its route (Desk and Hybrid).</summary>
        public bool Launch()
        {
            if (!CanPlan || Mission.Over || Drone.Crashed || Auto.Route.Count == 0) return false;
            if (Auto.Active && !Manual) return false;
            if (Auto.Active) Auto.Resume(Drone); else Auto.Launch();
            Manual = false;
            return true;
        }

        /// <summary>Calls the drone home (Desk and Hybrid): the autopilot flies it back to the pad.</summary>
        public bool Return()
        {
            if (!CanPlan || Mission.Over || Drone.Crashed) return false;
            if (!Auto.Active) Auto.Launch();
            Auto.Return(Drone);
            Manual = false;
            return true;
        }

        // ---------- the sticks ----------

        /// <summary>The pilot takes the sticks (Hybrid). The autopilot waits.</summary>
        public bool TakeControl()
        {
            if (Mode != Mode.Hybrid || Mission.Over || Drone.Crashed) return false;
            Manual = true;
            return true;
        }

        /// <summary>The pilot hands the sticks back (Hybrid): the autopilot goes on with the route, or comes home.</summary>
        public bool HandBack()
        {
            if (Mode != Mode.Hybrid || !Manual) return false;
            Manual = false;
            if (Auto.Active) Auto.Resume(Drone);
            else if (Auto.Route.Count > 0) Auto.Launch();
            else if (!Drone.Landed) { Auto.Launch(); Auto.Return(Drone); }
            return true;
        }

        /// <summary>The camera's button, pressed by the pilot.</summary>
        public bool Primary(Aim aim)
        {
            if (!Manual) return false;
            bool done = Mission.Primary(Drone, aim, Thermal);
            if (done && Mission is ReliefFlights) Auto.DroppedByHand(Drone.X, Drone.Z);
            return done;
        }

        /// <summary>Lets one package go, by the pilot's hand.</summary>
        public Package Release()
        {
            if (!Manual) return null;
            var p = Mission.Release(Drone);
            if (p != null) Auto.DroppedByHand(Drone.X, Drone.Z);
            return p;
        }

        // ---------- time ----------

        /// <summary>One step of DroneSpec.Dt seconds.</summary>
        public void Step(DroneInput sticks)
        {
            var m = Mission;
            if (m.Over) return;
            bool auto = !Manual && Auto.Active;
            double level = m.Weather.Level(m.Time);
            DroneInput input = Manual ? sticks : Auto.Step(Drone, m.Town, level);
            // a battery almost empty: the drone starts down by itself; the pilot can still steer it to a dry spot
            if (Manual && !Drone.Landed && Drone.Battery < DroneSpec.CriticalBattery) input.Up = Math.Min(input.Up, -0.6);
            double wx, wz, sx, sz, high = Drone.AboveGround;
            m.Weather.WindAt(m.Time, high, out wx, out wz);
            m.Weather.SteadyAt(m.Time, high, out sx, out sz);
            DroneModel.Step(Drone, input, m.Town, wx, wz, sx, sz, level);
            if (auto && Auto.ReleaseNow) m.Release(Drone);
            m.Tick(Drone, auto);
            if (Drone.Crashed && (Auto.Active || Auto.Route.Count > 0)) { Auto.Abort(); Auto.Route.Clear(); }
            // home again: the route is flown, the table is free for the next one
            if (Auto.Phase == PilotPhase.Done) { Auto.Abort(); Auto.Route.Clear(); }
        }

        /// <summary>Lets this many seconds pass, in whole steps. Returns how many steps were made.</summary>
        public int Advance(double seconds, DroneInput sticks)
        {
            accum += seconds;
            int n = 0;
            while (accum >= DroneSpec.Dt && n < 25) { Step(sticks); accum -= DroneSpec.Dt; n++; }
            if (accum > DroneSpec.Dt * 5) accum = 0; // a long hitch: do not try to catch up
            return n;
        }

        public MissionResult Result() { return Mission.Result(Drone); }
    }

    /// <summary>
    /// One session at the operations center: the missions flown and their best results. The summary on the
    /// board at the end is made here.
    /// </summary>
    public sealed class Operation
    {
        public Lang Lang = Lang.En;
        public Mode Mode = Mode.Hybrid;
        readonly Dictionary<string, MissionResult> best = new Dictionary<string, MissionResult>();
        readonly Dictionary<string, Mode> bestMode = new Dictionary<string, Mode>();
        public int Flown { get; private set; }

        public void Record(string mission, Mode mode, MissionResult r)
        {
            Flown++;
            MissionResult old;
            if (best.TryGetValue(mission, out old) && old.Score >= r.Score) return;
            best[mission] = r; bestMode[mission] = mode;
        }

        public MissionResult Best(string mission) { MissionResult r; return best.TryGetValue(mission, out r) ? r : null; }
        public int Stars(string mission) { var r = Best(mission); return r == null ? -1 : r.Stars; }
        public int TotalStars { get { int n = 0; foreach (var r in best.Values) n += r.Stars; return n; } }

        /// <summary>True once the three applications of drones (BANTAY, SAGIP, HATID) have each been flown.</summary>
        public bool AllFlown { get { return best.ContainsKey("bantay") && best.ContainsKey("sagip") && best.ContainsKey("hatid"); } }

        /// <summary>The lines of the board: each mission's best result, and what the operator has shown.</summary>
        public List<string> Summary()
        {
            var lines = new List<string>();
            foreach (var id in Mission.Ids)
            {
                var r = Best(id);
                if (r == null) lines.Add(Strings.Format("sum.notflown", Lang, Strings.Get("mission." + id + ".name", Lang)));
                else lines.Add(Strings.Format("sum.line", Lang, Strings.Get("mission." + id + ".name", Lang), Strings.Stars(r.Stars, Lang), r.Score, Strings.Get("mode." + bestMode[id].ToString().ToLowerInvariant(), Lang)));
            }
            lines.Add(Strings.Format("sum.total", Lang, TotalStars, Mission.Ids.Length * 3));
            lines.Add(Strings.Get(AllFlown ? "sum.all" : "sum.more", Lang));
            return lines;
        }
    }
}
