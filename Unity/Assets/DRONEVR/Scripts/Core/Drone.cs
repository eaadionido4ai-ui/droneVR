// The drone: a heavy-lift quadcopter with a camera, three hooks and a battery. This is how it flies.
//
// The pilot (or the autopilot) says how fast to go forward, sideways and up, and how fast to turn; the drone's
// flight controller does the rest, as on a real GPS-stabilized drone: it levels itself, it holds its place
// when the sticks are let go, and it leans into the steady wind by itself. What it cannot do is cancel every
// gust, fly faster through the air than its motors allow, or carry weight for free:
//   - a share of each gust pushes it off its place (GustHold is the share it cancels);
//   - against a wind stronger than MaxAirSpeed it is blown back;
//   - a load makes it slower, and the battery drains faster with a load, with speed and in a climb.
// It breaks if it hits something fast, or lands hard, and it is lost if it comes down in water.
//
// One step is Dt seconds. The same steps are in the Python reference (Tools/flight-reference/flight.py), which
// the tests compare this with, number for number.
using System;

namespace DroneVR
{
    public static class DroneSpec
    {
        /// <summary>Seconds in one step of the flight.</summary>
        public const double Dt = 0.02;
        /// <summary>Metres per second over the ground with the stick pushed all the way, and through the air at most.</summary>
        public const double MaxSpeed = 14, MaxAirSpeed = 18;
        public const double MaxClimb = 5, MaxDescent = 3.5;
        /// <summary>Degrees per second with the stick pushed all the way.</summary>
        public const double YawRate = 100;
        /// <summary>How quickly it takes up a new speed: seconds, over the ground and up or down.</summary>
        public const double TauH = 0.7, TauV = 0.4;
        /// <summary>The share of a gust that the flight controller cancels.</summary>
        public const double GustHold = 0.65;
        /// <summary>Half its width with the propellers, and the height of its body above its feet.</summary>
        public const double Radius = 0.6, Gear = 0.25;
        /// <summary>The highest it may fly above the pad: 120 m, the 400 feet of the CAAP's rules for drones.</summary>
        public const double Ceiling = 120;
        /// <summary>What it can lift, and on how many hooks.</summary>
        public const double MaxPayloadKg = 8;
        public const int Hooks = 3;
        /// <summary>With a full load it flies this much slower, and climbs this much slower.</summary>
        public const double LoadSlow = 0.25, LoadClimbSlow = 0.3;
        /// <summary>A full battery hovers this many seconds with nothing on the hooks.</summary>
        public const double HoverSeconds = 420;
        /// <summary>What uses the battery faster: each kilogram carried, flying at full air speed, climbing at full rate.</summary>
        public const double PayloadDrain = 0.06, SpeedDrain = 0.35, ClimbDrain = 0.25;
        /// <summary>A landing is safe below these speeds, down and sideways; anything is broken by a hit above CrashSpeed.</summary>
        public const double LandSpeed = 2.5, LandSlide = 3, CrashSpeed = 4;
        /// <summary>Below these shares of the battery the pilot is warned, and then the drone lands by itself.</summary>
        public const double LowBattery = 0.30, CriticalBattery = 0.12;
        /// <summary>Seconds on the pad to change the battery.</summary>
        public const double SwapSeconds = 4;
    }

    /// <summary>What the sticks say, each from -1 to 1.</summary>
    public struct DroneInput
    {
        public double Forward, Right, Up, Yaw;
        public DroneInput(double forward, double right, double up, double yaw) { Forward = forward; Right = right; Up = up; Yaw = yaw; }
        public static readonly DroneInput None = new DroneInput(0, 0, 0, 0);
    }

    public enum CrashKind { None, Obstacle, HardLanding, Water }

    public sealed class DroneState
    {
        public double X, Y, Z;
        public double Vx, Vy, Vz;
        /// <summary>Degrees: 0 faces north, 90 east.</summary>
        public double Heading;
        /// <summary>From 1 (full) to 0.</summary>
        public double Battery = 1;
        public double PayloadKg;
        public bool Landed = true;
        public CrashKind Crash = CrashKind.None;
        /// <summary>What it hit, when Crash is Obstacle.</summary>
        public HitKind HitBy = HitKind.None;
        /// <summary>How fast it moves through the air (not over the ground): east and south.</summary>
        public double AirVx, AirVz;
        /// <summary>Metres flown over the ground, and seconds in the air, since the last reset.</summary>
        public double Flown, Aloft;

        public bool Crashed { get { return Crash != CrashKind.None; } }
        public double Speed { get { return Math.Sqrt(Vx * Vx + Vz * Vz); } }
        public double AirSpeed { get { return Math.Sqrt(AirVx * AirVx + AirVz * AirVz); } }
        public P3 Position { get { return new P3(X, Y, Z); } }
        /// <summary>Its height above the ground under it (not above roofs).</summary>
        public double AboveGround { get { return Y - Terrain.Ground(X, Z); } }
        public double LoadShare { get { return Math.Min(1, PayloadKg / DroneSpec.MaxPayloadKg); } }
        public double MaxSpeedNow { get { return DroneSpec.MaxSpeed * (1 - DroneSpec.LoadSlow * LoadShare); } }
        public double MaxClimbNow { get { return DroneSpec.MaxClimb * (1 - DroneSpec.LoadClimbSlow * LoadShare); } }

        /// <summary>A fresh drone standing on the pad, facing east.</summary>
        public void ResetOnPad()
        {
            X = TownPlan.PadX; Z = TownPlan.PadZ; Y = TownPlan.PadY + DroneSpec.Gear;
            Vx = Vy = Vz = 0; AirVx = AirVz = 0; Heading = 90; Battery = 1; PayloadKg = 0;
            Landed = true; Crash = CrashKind.None; HitBy = HitKind.None; Flown = 0; Aloft = 0;
        }

        public DroneState Copy() { return (DroneState)MemberwiseClone(); }
    }

    public static class DroneModel
    {
        /// <summary>
        /// One step of DroneSpec.Dt seconds. (wx, wz) is the wind where the drone is, (sx, sz) its steady part,
        /// level the flood's level.
        /// </summary>
        public static void Step(DroneState d, DroneInput input, Town town, double wx, double wz, double sx, double sz, double level)
        {
            if (d.Crashed) return;
            const double dt = DroneSpec.Dt;
            bool dead = d.Battery <= 0;
            double fwd = Clamp(input.Forward), right = Clamp(input.Right), up = Clamp(input.Up), yaw = Clamp(input.Yaw);
            // an empty battery: the motors only manage a steady way down
            if (dead) { fwd = 0; right = 0; yaw = 0; up = -0.6; }

            d.Heading = Compass.Wrap(d.Heading + yaw * DroneSpec.YawRate * dt);
            if (d.Landed)
            {
                d.Vx = d.Vy = d.Vz = 0; d.AirVx = d.AirVz = 0;
                if (up > 0.1 && !dead) d.Landed = false; else return;
            }

            // the speed over the ground that the sticks ask for
            double len = Math.Sqrt(fwd * fwd + right * right);
            if (len > 1) { fwd /= len; right /= len; }
            double max = d.MaxSpeedNow;
            double cx = (Compass.ForwardX(d.Heading) * fwd + Compass.RightX(d.Heading) * right) * max;
            double cz = (Compass.ForwardZ(d.Heading) * fwd + Compass.RightZ(d.Heading) * right) * max;
            // to get it, the drone must fly this fast through the air, against the steady wind; no faster than it can
            double ax = cx - sx, az = cz - sz;
            double air = Math.Sqrt(ax * ax + az * az);
            if (air > DroneSpec.MaxAirSpeed) { ax *= DroneSpec.MaxAirSpeed / air; az *= DroneSpec.MaxAirSpeed / air; }
            // what it really gets: the steady wind is leaned into, a share of each gust is not
            double gx = ax + sx + (1 - DroneSpec.GustHold) * (wx - sx);
            double gz = az + sz + (1 - DroneSpec.GustHold) * (wz - sz);
            double kh = Math.Min(1, dt / DroneSpec.TauH), kv = Math.Min(1, dt / DroneSpec.TauV);
            d.Vx += (gx - d.Vx) * kh;
            d.Vz += (gz - d.Vz) * kh;
            double cy = up >= 0 ? up * d.MaxClimbNow : up * DroneSpec.MaxDescent;
            d.Vy += (cy - d.Vy) * kv;
            d.AirVx = d.Vx - wx; d.AirVz = d.Vz - wz;

            double nx = d.X + d.Vx * dt, ny = d.Y + d.Vy * dt, nz = d.Z + d.Vz * dt;
            // the edge of the barangay and the ceiling: the drone will not go past them
            if (nx < Terrain.X0 + 2) { nx = Terrain.X0 + 2; d.Vx = Math.Max(0, d.Vx); }
            if (nx > Terrain.X1 - 2) { nx = Terrain.X1 - 2; d.Vx = Math.Min(0, d.Vx); }
            if (nz < Terrain.Z0 + 2) { nz = Terrain.Z0 + 2; d.Vz = Math.Max(0, d.Vz); }
            if (nz > Terrain.Z1 - 2) { nz = Terrain.Z1 - 2; d.Vz = Math.Min(0, d.Vz); }
            double top = TownPlan.PadY + DroneSpec.Ceiling;
            if (ny > top) { ny = top; d.Vy = Math.Min(0, d.Vy); }

            double speed = Math.Sqrt(d.Vx * d.Vx + d.Vy * d.Vy + d.Vz * d.Vz);
            // coming down on something: the ground, a roof, the pad, or the water
            double surface = town.SurfaceBelow(nx, d.Y, nz);
            if (ny <= Math.Max(surface, level) + DroneSpec.Gear)
            {
                d.X = nx; d.Z = nz;
                if (level > surface + 0.05) { d.Y = level; Stop(d); d.Crash = CrashKind.Water; return; }
                d.Y = surface + DroneSpec.Gear;
                bool hard = -d.Vy > DroneSpec.LandSpeed || d.Speed > DroneSpec.LandSlide;
                Stop(d);
                if (hard) d.Crash = CrashKind.HardLanding; else d.Landed = true;
                return;
            }
            // running into something
            var hit = town.Hit(nx, ny, nz, DroneSpec.Radius);
            if (hit != HitKind.None)
            {
                if (speed > DroneSpec.CrashSpeed) { d.Crash = CrashKind.Obstacle; d.HitBy = hit; Stop(d); return; }
                // slowly: it bumps and stays where it was
                Stop(d);
                nx = d.X; ny = d.Y; nz = d.Z;
            }
            double moved = Math.Sqrt((nx - d.X) * (nx - d.X) + (nz - d.Z) * (nz - d.Z));
            d.X = nx; d.Y = ny; d.Z = nz;
            d.Flown += moved; d.Aloft += dt;

            // the battery
            if (!dead)
            {
                double a = d.AirSpeed / DroneSpec.MaxAirSpeed;
                double load = 1 + DroneSpec.PayloadDrain * d.PayloadKg + DroneSpec.SpeedDrain * a * a + DroneSpec.ClimbDrain * Math.Max(0, d.Vy) / DroneSpec.MaxClimb;
                d.Battery = Math.Max(0, d.Battery - dt * load / DroneSpec.HoverSeconds);
            }
        }

        static void Stop(DroneState d) { d.Vx = d.Vy = d.Vz = 0; d.AirVx = d.AirVz = 0; }
        static double Clamp(double v) { return v < -1 ? -1 : v > 1 ? 1 : double.IsNaN(v) ? 0 : v; }

        /// <summary>How much faster than a hover the battery drains, flying level at this air speed with this load.</summary>
        public static double DrainFactor(double payloadKg, double airSpeed)
        {
            double a = Math.Min(1, airSpeed / DroneSpec.MaxAirSpeed);
            return 1 + DroneSpec.PayloadDrain * payloadKg + DroneSpec.SpeedDrain * a * a;
        }
    }
}
