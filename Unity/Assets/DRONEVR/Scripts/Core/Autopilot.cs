// The autopilot: it flies the route drawn on the map table in the operations center, one waypoint after the
// other, then comes home and lands. It works the same sticks as a pilot does (DroneInput), so the drone
// flies by the same rules either way, in the same wind.
//
// A waypoint is a point on the map and a height above the ground there. The drone climbs first, flies level,
// and comes down only when it is near, so that a route drawn over houses stays above them. At a waypoint it
// can fly on, hover a few seconds (to look), or come down low and drop what it carries.
//
// What the autopilot does not do: see obstacles (a route through the mobile phone tower hits it), or aim a
// drop against the wind (the package falls from straight above the mark, and the wind carries it). For a
// small target on a windy day, the pilot takes the sticks. That is the Hybrid mode.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public enum WaypointKind
    {
        /// <summary>Fly through it.</summary>
        Fly,
        /// <summary>Stop over it for a few seconds: time for the camera.</summary>
        Hover,
        /// <summary>Come down low over it and release one package.</summary>
        Drop
    }

    public sealed class Waypoint
    {
        public double X, Z;
        /// <summary>Metres above the ground at this point.</summary>
        public double Alt;
        public WaypointKind Kind;
        public Waypoint(double x, double z, double alt, WaypointKind kind = WaypointKind.Fly) { X = x; Z = z; Alt = alt; Kind = kind; }
        /// <summary>The height to fly at, above the river's normal level.</summary>
        public double Y { get { return Terrain.Ground(X, Z) + Alt; } }
    }

    public enum PilotPhase { Idle, Climb, Cruise, Hold, Descend, Settle, Release, ClimbBack, Land, Done }

    public sealed class Autopilot
    {
        /// <summary>Metres per second between waypoints.</summary>
        public const double Cruise = 10;
        /// <summary>Seconds a Hover waypoint lasts.</summary>
        public const double HoverSeconds = 3;
        /// <summary>A package is released from this high above what is under the drone.</summary>
        public const double DropHeight = 3.5;
        /// <summary>The way home is flown this high above the pad.</summary>
        public const double ReturnAlt = 30;
        /// <summary>From the ground the drone first climbs at least this high, to clear what stands around the pad.</summary>
        public const double MinClimb = 10;
        /// <summary>A waypoint is reached within this many metres on the map, and this many in height.</summary>
        public const double Reach = 3, ReachHigh = 2.5;
        /// <summary>The drone starts down to a waypoint's height when it is this near.</summary>
        public const double DescendWithin = 15;
        /// <summary>The share of the battery kept in hand when deciding to turn for home.</summary>
        public const double Reserve = 0.08;
        /// <summary>The heights the map table offers for a waypoint, in metres above the ground.</summary>
        public static readonly double[] Altitudes = { 10, 15, 20, 30, 45, 60 };

        public readonly List<Waypoint> Route = new List<Waypoint>();
        /// <summary>The waypoint the drone is flying to.</summary>
        public int Next { get; private set; }
        public PilotPhase Phase { get; private set; }
        /// <summary>The route is flown (or the battery is low, or the pilot said so): the drone is on its way home.</summary>
        public bool Returning { get; private set; }
        /// <summary>The autopilot turned for home by itself because the battery would not last.</summary>
        public bool LowBatteryReturn { get; private set; }
        /// <summary>True for one step when a package must be released now. The mission lets it go.</summary>
        public bool ReleaseNow { get; private set; }
        /// <summary>The route was cleared in the air: hover here until there is somewhere to go.</summary>
        public bool Waiting { get; private set; }

        double legY, timer, still, holdX, holdZ;
        bool legStarted;

        public bool Active { get { return Phase != PilotPhase.Idle && Phase != PilotPhase.Done; } }
        public Waypoint Target { get { return !Returning && Next < Route.Count ? Route[Next] : null; } }

        /// <summary>Starts the route from the beginning.</summary>
        public void Launch()
        {
            Next = 0; Returning = false; LowBatteryReturn = false; Waiting = false; legStarted = false;
            Phase = Route.Count > 0 ? PilotPhase.Climb : PilotPhase.Idle;
        }

        /// <summary>Come home now.</summary>
        public void Return(DroneState d)
        {
            if (d.Landed && TownPlan.OnPad(d.X, d.Z)) return;
            Returning = true; Waiting = false; legStarted = false; Phase = PilotPhase.Climb;
        }

        /// <summary>Stops flying the route (the drone crashed, or the mission is over).</summary>
        public void Abort() { Phase = PilotPhase.Idle; Returning = false; Waiting = false; legStarted = false; ReleaseNow = false; }

        /// <summary>Takes the waypoints not yet reached off the route. In the air, the drone hovers where it is.</summary>
        public void ClearAhead(DroneState d)
        {
            if (Next < Route.Count) Route.RemoveRange(Next, Route.Count - Next);
            if (Returning || d.Landed) return;
            Waiting = true; holdX = d.X; holdZ = d.Z; Phase = PilotPhase.Hold;
        }

        /// <summary>Takes the last waypoint off the route, if the drone has not flown past it.</summary>
        public bool Undo()
        {
            if (Route.Count == 0 || Route.Count - 1 < Next) return false;
            if (Route.Count - 1 == Next && Active && Phase != PilotPhase.Climb && Phase != PilotPhase.Cruise) return false;
            Route.RemoveAt(Route.Count - 1);
            return true;
        }

        public void Add(Waypoint w)
        {
            Route.Add(w);
            if (Waiting) { Waiting = false; legStarted = false; Phase = PilotPhase.Climb; }
        }

        /// <summary>The pilot hands the sticks back: go on with the route from where the drone is now.</summary>
        public void Resume(DroneState d)
        {
            legStarted = false; ReleaseNow = false;
            if (Phase == PilotPhase.Idle || Phase == PilotPhase.Done) return;
            if (!Returning && Next >= Route.Count) { Returning = true; }
            Phase = d.Landed && Returning && TownPlan.OnPad(d.X, d.Z) ? PilotPhase.Done : PilotPhase.Climb;
        }

        /// <summary>The pilot dropped a package by hand near the next Drop waypoint: that waypoint is done.</summary>
        public void DroppedByHand(double x, double z)
        {
            var w = Target;
            if (w != null && w.Kind == WaypointKind.Drop && Math.Sqrt((w.X - x) * (w.X - x) + (w.Z - z) * (w.Z - z)) < 20) { Next++; legStarted = false; }
        }

        /// <summary>The share of the battery needed to come home from here, with what the drone carries.</summary>
        public static double BatteryToReturn(DroneState d)
        {
            double dx = TownPlan.PadX - d.X, dz = TownPlan.PadZ - d.Z;
            double climb = Math.Max(0, TownPlan.PadY + ReturnAlt - d.Y);
            double seconds = Math.Sqrt(dx * dx + dz * dz) / Cruise + climb / d.MaxClimbNow + ReturnAlt / 1.8 + 6;
            return seconds * DroneModel.DrainFactor(d.PayloadKg, Cruise * 1.3) / DroneSpec.HoverSeconds;
        }

        /// <summary>One step: what the sticks should say now. level is the flood's level: a drop is made from above the water.</summary>
        public DroneInput Step(DroneState d, Town town, double level = 0)
        {
            const double dt = DroneSpec.Dt;
            ReleaseNow = false;
            if (Phase == PilotPhase.Idle || Phase == PilotPhase.Done || d.Crashed) return DroneInput.None;

            if (!Returning && !d.Landed && d.Battery < BatteryToReturn(d) + Reserve)
            {
                Returning = true; LowBatteryReturn = true; Waiting = false; legStarted = false; Phase = PilotPhase.Climb;
            }
            if (!Returning && !Waiting && Next >= Route.Count) { Returning = true; legStarted = false; Phase = PilotPhase.Climb; }

            double tx, tz, ty;
            var w = Target;
            if (Waiting) { tx = holdX; tz = holdZ; ty = d.Y; }
            else if (w != null) { tx = w.X; tz = w.Z; ty = w.Y; }
            else { tx = TownPlan.PadX; tz = TownPlan.PadZ; ty = TownPlan.PadY + ReturnAlt; }
            if (!legStarted)
            {
                legStarted = true; timer = 0;
                // from the ground, always clear of the pad's surroundings first
                legY = Math.Max(d.Landed ? d.Y + MinClimb : d.Y, ty);
            }
            double dx = tx - d.X, dz = tz - d.Z, dist = Math.Sqrt(dx * dx + dz * dz);

            switch (Phase)
            {
                case PilotPhase.Climb:
                    // straight up to the height of the leg, before going anywhere
                    if (d.Y >= legY - 0.6 || Waiting) { Phase = Waiting ? PilotPhase.Hold : PilotPhase.Cruise; }
                    return Fly(d, d.X, d.Z, legY, 0, false);

                case PilotPhase.Cruise:
                {
                    double y = dist > DescendWithin ? legY : ty;
                    if (dist < Reach && Math.Abs(d.Y - ty) < ReachHigh)
                    {
                        timer = 0;
                        if (w == null) Phase = PilotPhase.Land;
                        else if (w.Kind == WaypointKind.Fly) Advance();
                        else if (w.Kind == WaypointKind.Hover) Phase = PilotPhase.Hold;
                        else Phase = PilotPhase.Descend;
                    }
                    return Fly(d, tx, tz, y, Cruise, true);
                }

                case PilotPhase.Hold:
                    if (!Waiting) { timer += dt; if (timer >= HoverSeconds) Advance(); }
                    return Fly(d, tx, tz, ty, 3, false);

                case PilotPhase.Descend:
                {
                    double low = Math.Max(town.SurfaceBelow(tx, d.Y, tz), level) + DropHeight;
                    if (d.Y <= low + 0.4) { Phase = PilotPhase.Settle; timer = 0; still = 0; }
                    return Fly(d, tx, tz, low, 3, false);
                }

                case PilotPhase.Settle:
                {
                    // wait until the drone hangs still over the mark, but not for ever: a gusty wind never lets it
                    double low = Math.Max(town.SurfaceBelow(tx, d.Y, tz), level) + DropHeight;
                    timer += dt;
                    still = dist < 1.2 && d.Speed < 0.8 ? still + dt : 0;
                    if (still >= 1 || timer >= 8) { Phase = PilotPhase.Release; timer = 0; ReleaseNow = true; }
                    return Fly(d, tx, tz, low, 3, false);
                }

                case PilotPhase.Release:
                    timer += dt;
                    if (timer >= 0.6) Phase = PilotPhase.ClimbBack;
                    return Fly(d, tx, tz, d.Y, 3, false);

                case PilotPhase.ClimbBack:
                    if (d.Y >= ty - 0.6) Advance();
                    return Fly(d, tx, tz, ty, 3, false);

                case PilotPhase.Land:
                {
                    if (d.Landed) { Phase = PilotPhase.Done; return DroneInput.None; }
                    // fast at first, gently for the last metres
                    double above = d.Y - (TownPlan.PadY + DroneSpec.Gear);
                    var input = Fly(d, tx, tz, d.Y, 3, false);
                    input.Up = above > 4 ? -1 : -0.42;
                    return input;
                }
            }
            return DroneInput.None;
        }

        void Advance() { Next++; legStarted = false; Phase = PilotPhase.Climb; timer = 0; }

        /// <summary>The sticks that take the drone towards a point at a height, no faster than a speed.</summary>
        public static DroneInput Fly(DroneState d, double tx, double tz, double ty, double speed, bool turn)
        {
            double dx = tx - d.X, dz = tz - d.Z, dist = Math.Sqrt(dx * dx + dz * dz);
            double v = Math.Min(speed, dist * 0.9);
            double vx = dist > 1e-6 ? dx / dist * v : 0, vz = dist > 1e-6 ? dz / dist * v : 0;
            double max = d.MaxSpeedNow;
            var input = new DroneInput
            {
                Forward = (vx * Compass.ForwardX(d.Heading) + vz * Compass.ForwardZ(d.Heading)) / max,
                Right = (vx * Compass.RightX(d.Heading) + vz * Compass.RightZ(d.Heading)) / max,
            };
            double e = ty - d.Y;
            input.Up = e >= 0 ? Math.Min(1, e * 0.8 / d.MaxClimbNow) : Math.Max(-1, e * 0.8 / DroneSpec.MaxDescent);
            if (turn && dist > 6) input.Yaw = Math.Max(-1, Math.Min(1, Compass.Turn(d.Heading, Compass.Towards(d.X, d.Z, tx, tz)) / 30));
            return input;
        }
    }

    /// <summary>What a route will cost, worked out before the drone flies it: shown on the map table.</summary>
    public struct RouteEstimate
    {
        /// <summary>Metres over the ground, there and back to the pad.</summary>
        public double Metres;
        public double Seconds;
        /// <summary>The share of a full battery it will use.</summary>
        public double Battery;

        /// <summary>
        /// For a route flown from the pad and back, carrying payloadKg at the start (each Drop leaves one package:
        /// dropKg is the weight of each in turn), in a steady wind (meanWind m/s at Wind.FullHeight, from windFrom).
        /// </summary>
        public static RouteEstimate For(IList<Waypoint> route, double payloadKg, IList<double> dropKg, double meanWind, double windFrom)
        {
            var e = new RouteEstimate();
            double x = TownPlan.PadX, z = TownPlan.PadZ, y = TownPlan.PadY, kg = payloadKg;
            int drop = 0;
            for (int i = 0; i <= route.Count; i++)
            {
                bool home = i == route.Count;
                double tx = home ? TownPlan.PadX : route[i].X, tz = home ? TownPlan.PadZ : route[i].Z;
                double ty = home ? TownPlan.PadY + Autopilot.ReturnAlt : route[i].Y;
                double share = Math.Min(1, kg / DroneSpec.MaxPayloadKg);
                double climbRate = DroneSpec.MaxClimb * (1 - DroneSpec.LoadClimbSlow * share);
                double climb = Math.Max(0, ty - y), d = Math.Sqrt((tx - x) * (tx - x) + (tz - z) * (tz - z));
                // the climb, then the leg: over the ground at cruise speed, through the air faster or slower with the wind
                double tClimb = climb > 0.5 ? climb / climbRate + 1.5 : 0;
                double s = meanWind * Wind.AtHeight(Math.Max(0, ty - Terrain.Ground(tx, tz)));
                double wx = -Compass.ForwardX(windFrom) * s, wz = -Compass.ForwardZ(windFrom) * s;
                double ux = d > 1e-6 ? (tx - x) / d : 0, uz = d > 1e-6 ? (tz - z) / d : 0;
                double ax = ux * Autopilot.Cruise - wx, az = uz * Autopilot.Cruise - wz;
                double air = Math.Sqrt(ax * ax + az * az);
                double tLeg = d / Autopilot.Cruise + 1.5;
                e.Seconds += tClimb + tLeg;
                e.Battery += (tClimb * (DroneModel.DrainFactor(kg, s) + DroneSpec.ClimbDrain * 0.8) + tLeg * DroneModel.DrainFactor(kg, air)) / DroneSpec.HoverSeconds;
                e.Metres += d;
                x = tx; z = tz; y = Math.Max(y, ty);
                double wait = 0;
                if (home) wait = (Autopilot.ReturnAlt - 4) / DroneSpec.MaxDescent + 4 / 1.47 + 1.5;
                else if (route[i].Kind == WaypointKind.Hover) wait = Autopilot.HoverSeconds;
                else if (route[i].Kind == WaypointKind.Drop)
                {
                    wait = route[i].Alt / 3.2 + route[i].Alt / climbRate + 4;
                    if (dropKg != null && drop < dropKg.Count) kg = Math.Max(0, kg - dropKg[drop]);
                    drop++;
                }
                if (!home) y = ty;
                e.Seconds += wait;
                e.Battery += wait * DroneModel.DrainFactor(kg, s) / DroneSpec.HoverSeconds;
            }
            return e;
        }
    }
}
