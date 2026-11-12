// The ground of the riverside barangay, as a formula: the height of the ground at any point, in metres above
// the river's normal level. The same formula is in the plan the models are built from
// (Tools/world-export/src/plan.js), in the GAMA model (GAMA/models/DRONE.gaml) and in the Python reference
// (Tools/flight-reference/flight.py). Tools/check_constants.py checks that the numbers agree; change them in
// all four places.
//
// Places use DRONE VR's own coordinates: metres, x to the east, z to the south, y up. The barangay is 480 m
// from west to east and 360 m from north to south. In Unity a point (x, y, z) is at (x, y, -z); in GAMA it is
// at (x + 240, z + 180).
using System;

namespace DroneVR
{
    /// <summary>A point, or a direction: x east, y up, z south.</summary>
    public struct P3
    {
        public double X, Y, Z;
        public P3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static P3 operator +(P3 a, P3 b) { return new P3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static P3 operator -(P3 a, P3 b) { return new P3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static P3 operator *(P3 a, double k) { return new P3(a.X * k, a.Y * k, a.Z * k); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        /// <summary>The length on the map, leaving the height out.</summary>
        public double Flat { get { return Math.Sqrt(X * X + Z * Z); } }
        public static double Dot(P3 a, P3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public override string ToString() { return "(" + X.ToString("0.0") + ", " + Y.ToString("0.0") + ", " + Z.ToString("0.0") + ")"; }
    }

    public static class Terrain
    {
        public const double X0 = -240, X1 = 240, Z0 = -180, Z1 = 180;

        /// <summary>The river runs from west to east between these two z; its banks slope over RiverBank metres.</summary>
        public const double RiverNorth = 100, RiverSouth = 140, RiverBank = 4, RiverDepth = 3.1;
        /// <summary>North of the river the land rises gently away from it.</summary>
        public const double NorthBank = 0.6, NorthSlope = 0.010;
        /// <summary>The creek's valley is lower, and rises away from the creek.</summary>
        public const double ValleyBank = 0.3, ValleySlope = 0.004, ValleySide = 0.022;
        /// <summary>The creek runs south into the river.</summary>
        public const double CreekX = -40, CreekHalf = 4, CreekDepth = 1.5;
        /// <summary>The far bank, where the sitio is.</summary>
        public const double SouthBank = 0.8, SouthSlope = 0.03;
        /// <summary>The school's hill, flat on top.</summary>
        public const double HillX = 150, HillZ = -110, HillR = 90, HillPeak = 6, HillTop = 4;
        /// <summary>The rise the barangay hall stands on.</summary>
        public const double RiseX = -170, RiseZ = -125, RiseR = 70, RisePeak = 3, RiseTop = 1.5;

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        static double Hypot(double a, double b) { return Math.Sqrt(a * a + b * b); }

        /// <summary>The height of the ground at a point, in metres above the river's normal level.</summary>
        public static double Ground(double x, double z)
        {
            if (z >= RiverSouth) return SouthBank + (z - RiverSouth) * SouthSlope;
            double side = Math.Max(0, Math.Abs(x - CreekX) - CreekHalf) * ValleySide;
            double cut = CreekDepth * Clamp01(CreekHalf - Math.Abs(x - CreekX));
            if (z > RiverNorth)
            {
                double bank = Math.Min(NorthBank, ValleyBank + side) - cut;
                double k = (z - RiverNorth) / (RiverSouth - RiverNorth);
                double dip = Clamp01(Math.Min(z - RiverNorth, RiverSouth - z) / RiverBank);
                return bank + (SouthBank - bank) * k - RiverDepth * dip;
            }
            double up = RiverNorth - z;
            double g = Math.Min(NorthBank + up * NorthSlope, ValleyBank + up * ValleySlope + side);
            g += Math.Min(HillTop, HillPeak * Math.Max(0, 1 - Hypot(x - HillX, z - HillZ) / HillR));
            g += Math.Min(RiseTop, RisePeak * Math.Max(0, 1 - Hypot(x - RiseX, z - RiseZ) / RiseR));
            return g - cut;
        }

        public static bool Inside(double x, double z) { return x >= X0 && x <= X1 && z >= Z0 && z <= Z1; }

        /// <summary>How deep the water is at a point when the flood stands at this level (0 where the ground is dry).</summary>
        public static double Depth(double x, double z, double level) { return Math.Max(0, level - Ground(x, z)); }
    }

    /// <summary>Angles on the map. A heading is in degrees: 0 faces north (-z), 90 faces east (+x).</summary>
    public static class Compass
    {
        public const double Deg = Math.PI / 180;
        public static double Wrap(double degrees) { degrees %= 360; return degrees < 0 ? degrees + 360 : degrees; }
        /// <summary>The smallest turn from one heading to another, from -180 to 180 (positive to the right).</summary>
        public static double Turn(double from, double to) { double d = Wrap(to - from); return d > 180 ? d - 360 : d; }
        /// <summary>The heading that goes from one point towards another.</summary>
        public static double Towards(double x0, double z0, double x1, double z1) { return Wrap(Math.Atan2(x1 - x0, -(z1 - z0)) / Deg); }
        public static double ForwardX(double heading) { return Math.Sin(heading * Deg); }
        public static double ForwardZ(double heading) { return -Math.Cos(heading * Deg); }
        public static double RightX(double heading) { return Math.Cos(heading * Deg); }
        public static double RightZ(double heading) { return Math.Sin(heading * Deg); }
        static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        /// <summary>N, NE, E... for a heading.</summary>
        public static string Point(double heading) { return Points[(int)Math.Floor(Wrap(heading + 22.5) / 45) % 8]; }
    }
}
