// The weather a mission flies in: the wind, with its gusts, and the level of the flood. Each mission has its
// own (TownPlan.Missions): calm for the flight school, a breeze and dust after the earthquake, rain and
// rising water for the search, strong gusty wind for the relief flights.
//
// GAMA runs the same weather (GAMA/models/DRONE.gaml) and sends it to the headsets, so a class flies in one
// weather and the teacher can raise the wind. Without GAMA, Unity works it out here. The numbers are the
// same in both, and in the Python reference; Tools/check_constants.py checks them.
using System;

namespace DroneVR
{
    public static class Wind
    {
        /// <summary>Gusts are two waves on top of the mean wind: their periods in seconds, and their shares.</summary>
        public const double GustPeriodA = 11, GustPeriodB = 4.3, GustShareA = 0.6, GustShareB = 0.4, GustPhaseB = 1.3;
        /// <summary>The wind's direction swings slowly from side to side.</summary>
        public const double SwingPeriod = 23, SwingDegrees = 15;
        /// <summary>Near the ground the wind is weaker: this share of what it is at FullHeight metres and above.</summary>
        public const double LowShare = 0.6, FullHeight = 60;

        /// <summary>The wind's speed at FullHeight, t seconds into the mission, for this mean speed and gustiness.</summary>
        public static double Speed(double mean, double gust, double t)
        {
            double wave = GustShareA * Math.Sin(2 * Math.PI * t / GustPeriodA) + GustShareB * Math.Sin(2 * Math.PI * t / GustPeriodB + GustPhaseB);
            return Math.Max(0, mean * (1 + gust * wave));
        }

        /// <summary>Where the wind blows from (degrees), t seconds into the mission.</summary>
        public static double From(double from, double t) { return from + SwingDegrees * Math.Sin(2 * Math.PI * t / SwingPeriod); }

        /// <summary>The share of the wind felt this high above the ground.</summary>
        public static double AtHeight(double aboveGround)
        {
            double k = aboveGround / FullHeight;
            return LowShare + (1 - LowShare) * (k < 0 ? 0 : k > 1 ? 1 : k);
        }

        /// <summary>The Beaufort name of a wind, for the pilot's display.</summary>
        public static string Name(double speed, Lang lang)
        {
            string key = speed < 1.6 ? "wind.calm" : speed < 5.5 ? "wind.light" : speed < 8 ? "wind.moderate" : speed < 10.8 ? "wind.fresh" : speed < 13.9 ? "wind.strong" : "wind.gale";
            return Strings.Get(key, lang);
        }
    }

    /// <summary>The weather of one mission, from moment to moment.</summary>
    public sealed class Weather
    {
        public readonly MissionPlan Plan;
        /// <summary>The teacher's hand on the wind: 1 is the mission's own wind (GAMA: "Raise the wind").</summary>
        public double WindScale = 1;

        // what GAMA last said, when GAMA runs the weather
        bool fromGama;
        double gamaSpeed, gamaFrom, gamaMean, gamaLevel;

        public Weather(MissionPlan plan) { Plan = plan; }

        /// <summary>GAMA's weather: the wind's speed now and its mean (m/s at FullHeight), where it blows from, and the flood's level.</summary>
        public void SetFromGama(double speed, double mean, double from, double level)
        {
            fromGama = true; gamaSpeed = speed; gamaMean = mean; gamaFrom = from; gamaLevel = level;
        }

        /// <summary>GAMA stopped answering: back to our own weather.</summary>
        public void ForgetGama() { fromGama = false; }
        public bool FromGama { get { return fromGama; } }

        public double MeanSpeed { get { return fromGama ? gamaMean : Plan.Wind * WindScale; } }
        public double SpeedAt(double t) { return fromGama ? gamaSpeed : Wind.Speed(Plan.Wind * WindScale, Plan.Gust, t); }
        public double FromAt(double t) { return fromGama ? gamaFrom : Wind.From(Plan.WindFrom, t); }

        /// <summary>The wind at a height above the ground, t seconds into the mission: how fast the air moves east (x) and south (z).</summary>
        public void WindAt(double t, double aboveGround, out double wx, out double wz)
        {
            double s = SpeedAt(t) * Wind.AtHeight(aboveGround), from = FromAt(t);
            wx = -Compass.ForwardX(from) * s; wz = -Compass.ForwardZ(from) * s;
        }

        /// <summary>The steady part of the wind, which a drone's flight controller leans into by itself.</summary>
        public void SteadyAt(double t, double aboveGround, out double wx, out double wz)
        {
            double s = MeanSpeed * Wind.AtHeight(aboveGround), from = fromGama ? gamaFrom : Plan.WindFrom;
            wx = -Compass.ForwardX(from) * s; wz = -Compass.ForwardZ(from) * s;
        }

        /// <summary>The flood's level t seconds into the mission, in metres above the river's normal level.</summary>
        public double Level(double t) { return fromGama ? gamaLevel : LevelOf(Plan, t); }

        public static double LevelOf(MissionPlan plan, double t)
        {
            double m = t / 60.0;
            var min = plan.FloodMinutes; var lev = plan.FloodLevels;
            if (m <= min[0]) return lev[0];
            for (int k = 1; k < min.Length; k++)
                if (m <= min[k]) return lev[k - 1] + (lev[k] - lev[k - 1]) * (m - min[k - 1]) / (min[k] - min[k - 1]);
            return lev[lev.Length - 1];
        }
    }
}
