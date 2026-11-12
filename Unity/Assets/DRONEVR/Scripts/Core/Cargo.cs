// What the drone carries, and how a package falls once it is let go.
//
// The drone has three hooks and lifts 8 kg (DroneSpec). Supplies are loaded on the pad, at the operations
// center's loading dock. In the air a package is released from a hook and falls: it starts with the drone's
// own speed over the ground, gravity pulls it down, and the wind takes hold of it on the way. Dropped from
// too high it breaks; dropped over water it is lost.
using System;
using System.Collections.Generic;

namespace DroneVR
{
    public enum PackageFate
    {
        Falling,
        /// <summary>It landed in the place that asked for it.</summary>
        Delivered,
        /// <summary>It landed outside the place.</summary>
        Missed,
        /// <summary>It fell from too high.</summary>
        Broken,
        /// <summary>It fell in the water.</summary>
        Sunk,
        /// <summary>It landed in a place that did not ask for it.</summary>
        NotNeeded,
        /// <summary>A life ring that reached the person it was for.</summary>
        Reached
    }

    public sealed class Package
    {
        public readonly string Item;
        public double X, Y, Z, Vx, Vy, Vz;
        public PackageFate Fate = PackageFate.Falling;
        /// <summary>How fast it was falling when it landed.</summary>
        public double Impact;
        /// <summary>Which site or survivor it was for, once it has landed (or -1), and how far from the mark it fell.</summary>
        public int Target = -1;
        public double Off;
        public Package(string item) { Item = item; }
        public bool Falling { get { return Fate == PackageFate.Falling; } }
    }

    public static class PackageFall
    {
        public const double Gravity = 9.81;
        /// <summary>How fast the wind takes hold of a falling package (per second), and how much the air slows its fall.</summary>
        public const double WindPull = 0.9, AirDrag = 0.12;
        /// <summary>A package that lands faster than this is broken: a fall of about 4.5 m. A life ring is not.</summary>
        public const double BreakSpeed = 9.2;

        /// <summary>One step of DroneSpec.Dt seconds. Returns true when the package has just landed.</summary>
        public static bool Step(Package p, Town town, double wx, double wz, double level)
        {
            if (!p.Falling) return false;
            const double dt = DroneSpec.Dt;
            p.Vx += (wx - p.Vx) * WindPull * dt;
            p.Vz += (wz - p.Vz) * WindPull * dt;
            p.Vy -= (Gravity + AirDrag * p.Vy) * dt;
            double nx = p.X + p.Vx * dt, ny = p.Y + p.Vy * dt, nz = p.Z + p.Vz * dt;
            double surface = town.SurfaceBelow(nx, p.Y, nz);
            if (ny > Math.Max(surface, level)) { p.X = nx; p.Y = ny; p.Z = nz; return false; }
            p.X = nx; p.Z = nz; p.Impact = -p.Vy;
            bool water = level > surface + 0.05;
            p.Y = water ? level : surface;
            p.Vx = p.Vy = p.Vz = 0;
            p.Fate = water && p.Item != "ring" ? PackageFate.Sunk : p.Impact > BreakSpeed && p.Item != "ring" ? PackageFate.Broken : PackageFate.Missed;
            return true;
        }
    }

    /// <summary>What hangs from the drone's hooks.</summary>
    public sealed class Cargo
    {
        public readonly List<string> Items = new List<string>();

        public double Kg
        {
            get { double kg = 0; foreach (var i in Items) kg += TownPlan.Supply(i).Kg; return kg; }
        }

        public int FreeHooks { get { return DroneSpec.Hooks - Items.Count; } }

        /// <summary>Why this cannot be loaded, as a key of Strings; null if it can.</summary>
        public string WhyNot(string item)
        {
            if (Items.Count >= DroneSpec.Hooks) return "load.hooks";
            if (Kg + TownPlan.Supply(item).Kg > DroneSpec.MaxPayloadKg + 1e-9) return "load.heavy";
            return null;
        }

        public bool Add(string item)
        {
            if (WhyNot(item) != null) return false;
            Items.Add(item);
            return true;
        }

        public bool Remove(string item) { return Items.Remove(item); }
    }
}
