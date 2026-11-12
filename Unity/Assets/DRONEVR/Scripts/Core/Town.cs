// What stands in the barangay, as far as a drone is concerned: the things it can hit and the surfaces it can
// land on. The lists themselves (TownPlan) are written from the plan the models are built from
// (Tools/world-export/src/plan.js), so what the drone hits is what the pilot sees.
//
// A Town is the barangay as one mission shows it: some pieces belong to a scene set (the houses that fall in
// the earthquake, the middle of the bridge), and they only count while that set is shown.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public enum RoofKind
    {
        /// <summary>Two slopes meeting at a ridge.</summary>
        Gable,
        Flat,
        /// <summary>A roof on posts, open underneath: a covered court. A drone can fly under it.</summary>
        Open
    }

    public sealed class Building
    {
        public readonly string Id, Kind, Set;
        /// <summary>Its center, its width along x and its depth along z.</summary>
        public readonly double X, Z, W, D;
        /// <summary>The height of its walls, how much higher its roof's top is, and the height of its floor.</summary>
        public readonly double H, RoofH, Base;
        public readonly RoofKind Roof;
        public Building(string id, string kind, double x, double z, double w, double d, double h, RoofKind roof, double roofH, double @base, string set)
        { Id = id; Kind = kind; X = x; Z = z; W = w; D = d; H = h; Roof = roof; RoofH = roofH; Base = @base; Set = set; }
        /// <summary>The height of its highest point: the ridge, or the flat roof.</summary>
        public double Top { get { return Base + H + RoofH; } }
        public bool Covers(double x, double z, double margin) { return Math.Abs(x - X) <= W / 2 + margin && Math.Abs(z - Z) <= D / 2 + margin; }
    }

    /// <summary>A flat piece in the air, such as the deck of the bridge.</summary>
    public sealed class Slab
    {
        public readonly string Id, Set;
        public readonly double X0, X1, Z0, Z1, Y0, Y1;
        public Slab(string id, double x0, double x1, double z0, double z1, double y0, double y1, string set)
        { Id = id; X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; Y0 = y0; Y1 = y1; Set = set; }
    }

    /// <summary>Something tall and round: the water tower, the mobile phone tower.</summary>
    public sealed class Tower
    {
        public readonly string Id;
        public readonly double X, Z, R, H;
        public Tower(string id, double x, double z, double r, double h) { Id = id; X = x; Z = z; R = r; H = h; }
    }

    public sealed class Tree
    {
        public readonly double X, Z, H, R;
        public readonly bool Palm;
        public Tree(double x, double z, double h, double r, bool palm) { X = x; Z = z; H = h; R = r; Palm = palm; }
    }

    public sealed class Pole
    {
        public readonly double X, Z, H;
        public Pole(double x, double z, double h) { X = x; Z = z; H = h; }
    }

    /// <summary>A ring of the flight school: its center is Y metres above the ground.</summary>
    public sealed class Gate
    {
        public readonly double X, Z, Y, R;
        public Gate(double x, double z, double y, double r) { X = x; Z = z; Y = y; R = r; }
        public P3 Center { get { return new P3(X, Terrain.Ground(X, Z) + Y, Z); } }
    }

    /// <summary>A hazard to photograph after the earthquake.</summary>
    public sealed class HazardSpot
    {
        public readonly string Id;
        /// <summary>Where it is; Y is the height of the thing to photograph above the ground.</summary>
        public readonly double X, Z, Y;
        /// <summary>How close a drone may safely come, in metres.</summary>
        public readonly double Near;
        /// <summary>Where the residents said to look: near the hazard, not on it.</summary>
        public readonly double ReportX, ReportZ;
        public HazardSpot(string id, double x, double z, double y, double near, double reportX, double reportZ)
        { Id = id; X = x; Z = z; Y = y; Near = near; ReportX = reportX; ReportZ = reportZ; }
        public P3 Point { get { return new P3(X, Terrain.Ground(X, Z) + Y, Z); } }
    }

    /// <summary>People waiting for rescue in the flood.</summary>
    public sealed class SurvivorSpot
    {
        public readonly string Id, Where;
        /// <summary>Where they are; Y is how high they are above the ground.</summary>
        public readonly double X, Z, Y;
        public readonly int People;
        /// <summary>They cannot be seen from above (under a roof, under leaves): the thermal camera finds them, from close.</summary>
        public readonly bool Hidden;
        /// <summary>How many minutes they can safely wait.</summary>
        public readonly double LimitMinutes;
        /// <summary>They are in fast water: a boat can only pull them in once a life ring has been dropped to them.</summary>
        public readonly bool Ring;
        public SurvivorSpot(string id, string where, double x, double z, double y, int people, bool hidden, double limitMinutes, bool ring)
        { Id = id; Where = where; X = x; Z = z; Y = y; People = people; Hidden = hidden; LimitMinutes = limitMinutes; Ring = ring; }
        public P3 Point { get { return new P3(X, Terrain.Ground(X, Z) + Y, Z); } }
    }

    /// <summary>A place that asked for supplies.</summary>
    public sealed class DropSite
    {
        public readonly string Id;
        /// <summary>The center of the place to drop into, its height above the ground (a roof), and its radius.</summary>
        public readonly double X, Z, Y, R;
        public readonly string[] Needs;
        public DropSite(string id, double x, double z, double y, double r, string[] needs) { Id = id; X = x; Z = z; Y = y; R = r; Needs = needs; }
        public P3 Point { get { return new P3(X, Terrain.Ground(X, Z) + Y, Z); } }
    }

    public sealed class SupplyKind
    {
        public readonly string Id;
        public readonly double Kg;
        public readonly int Color;
        public SupplyKind(string id, double kg, int color) { Id = id; Kg = kg; Color = color; }
    }

    /// <summary>The weather and the scene of one mission.</summary>
    public sealed class MissionPlan
    {
        public readonly string Id;
        public readonly string[] Sets;
        /// <summary>The mean wind in m/s at 60 m up, how much it swings in gusts, and where it blows from (degrees).</summary>
        public readonly double Wind, Gust, WindFrom;
        /// <summary>The flood's level over the minutes of the mission, in metres above the river's normal level.</summary>
        public readonly double[] FloodMinutes, FloodLevels;
        public readonly double Minutes;
        public MissionPlan(string id, string[] sets, double wind, double gust, double windFrom, double[] floodMinutes, double[] floodLevels, double minutes)
        { Id = id; Sets = sets; Wind = wind; Gust = gust; WindFrom = windFrom; FloodMinutes = floodMinutes; FloodLevels = floodLevels; Minutes = minutes; }
    }

    public static partial class TownPlan
    {
        public static MissionPlan Mission(string id)
        {
            foreach (var m in Missions) if (m.Id == id) return m;
            throw new ArgumentException("no mission " + id);
        }

        public static SupplyKind Supply(string id)
        {
            foreach (var s in Supplies) if (s.Id == id) return s;
            throw new ArgumentException("no supply " + id);
        }

        /// <summary>The pad the drones take off from: its top is this high.</summary>
        public static double PadY { get { return Terrain.Ground(PadX, PadZ) + 0.2; } }
        public static P3 Pad { get { return new P3(PadX, PadY, PadZ); } }
        public static bool OnPad(double x, double z) { return Math.Abs(x - PadX) <= PadSize / 2 && Math.Abs(z - PadZ) <= PadSize / 2; }
    }

    public enum HitKind { None, Building, Roof, Tower, Tree, Pole, Bridge }

    /// <summary>The barangay as one mission shows it.</summary>
    public sealed class Town
    {
        readonly List<Building> buildings = new List<Building>();
        readonly List<Slab> slabs = new List<Slab>();

        public Town(params string[] sets)
        {
            var on = new HashSet<string>(sets ?? new string[0]);
            foreach (var b in TownPlan.Buildings) if (b.Set == null || on.Contains(b.Set)) buildings.Add(b);
            foreach (var s in TownPlan.Slabs) if (s.Set == null || on.Contains(s.Set)) slabs.Add(s);
        }

        public static Town For(MissionPlan plan) { return new Town(plan.Sets); }

        /// <summary>Nothing but the ground and the pad: the open country the reference model flies over.</summary>
        public static Town Bare() { var t = new Town(); t.buildings.Clear(); t.slabs.Clear(); t.bare = true; return t; }
        bool bare;

        public IReadOnlyList<Building> Buildings { get { return buildings; } }

        /// <summary>
        /// The highest thing to stand on at (x, z) that is not above y: the ground, the pad, a roof, the bridge.
        /// A little above y still counts (half a metre), so that a drone skimming a roof finds it.
        /// </summary>
        public double SurfaceBelow(double x, double y, double z)
        {
            double s = Terrain.Ground(x, z);
            if (TownPlan.OnPad(x, z)) s = Math.Max(s, TownPlan.PadY);
            foreach (var b in buildings)
            {
                if (!b.Covers(x, z, 0)) continue;
                if (b.Top <= y + 0.5 && b.Top > s) s = b.Top;
            }
            foreach (var d in slabs)
            {
                if (x < d.X0 || x > d.X1 || z < d.Z0 || z > d.Z1) continue;
                if (d.Y1 <= y + 0.5 && d.Y1 > s) s = d.Y1;
            }
            return s;
        }

        /// <summary>The highest surface at (x, z), seen from the sky.</summary>
        public double SurfaceFromAbove(double x, double z) { return SurfaceBelow(x, 1e9, z); }

        /// <summary>What a ball of this radius at this point is touching, if anything. Tops of roofs are for SurfaceBelow.</summary>
        public HitKind Hit(double x, double y, double z, double radius)
        {
            if (bare) return HitKind.None;
            foreach (var b in buildings)
            {
                if (!b.Covers(x, z, radius)) continue;
                double low = b.Roof == RoofKind.Open ? b.Base + b.H : b.Base - 3;
                if (y > low - radius && y < b.Top - 0.3) return b.Roof == RoofKind.Open ? HitKind.Roof : HitKind.Building;
            }
            foreach (var d in slabs)
            {
                if (x < d.X0 - radius || x > d.X1 + radius || z < d.Z0 - radius || z > d.Z1 + radius) continue;
                if (y > d.Y0 - radius && y < d.Y1 - 0.3) return HitKind.Bridge;
            }
            foreach (var t in TownPlan.Towers)
            {
                double dx = x - t.X, dz = z - t.Z, r = t.R + radius;
                if (dx * dx + dz * dz < r * r && y < Terrain.Ground(t.X, t.Z) + t.H + radius) return HitKind.Tower;
            }
            foreach (var t in TownPlan.Trees)
            {
                double dx = x - t.X, dz = z - t.Z, r = t.R * 0.8 + radius;
                if (dx * dx + dz * dz >= r * r) continue;
                double g = Terrain.Ground(t.X, t.Z);
                if (y > g + t.H - t.R - 0.5 && y < g + t.H + 1) return HitKind.Tree;
            }
            foreach (var p in TownPlan.Poles)
            {
                double dx = x - p.X, dz = z - p.Z, r = 0.4 + radius;
                if (dx * dx + dz * dz < r * r && y < Terrain.Ground(p.X, p.Z) + p.H + radius) return HitKind.Pole;
            }
            return HitKind.None;
        }
    }
}
