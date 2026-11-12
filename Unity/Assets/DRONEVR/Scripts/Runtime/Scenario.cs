// What happens around the operator that is not up to them, and what the rest of the class does. GAMA runs the
// incident when it is connected (the SIMPLE way: the simulation lives in GAMA, the headset shows it): it starts
// the class's mission, sends the weather, tells every headset what the others' drones have found, moves the
// rescue boats and shows the other drones. When GAMA is not there, or stops answering, the headset goes on with
// its own: the same rules and weather, in DroneVR.Core.
//
// This class also tells GAMA what this headset's drone does: where it is, and each ring, photograph, group
// found, life ring and delivery, by comparing the mission with what it has already told.
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DroneVR.Vr
{
    public struct BoatPose
    {
        public double X, Z, Heading;
        /// <summary>0 waiting, 1 on its way out, 2 taking people aboard, 3 on its way back, 4 putting them ashore.</summary>
        public int Phase;
        public int Job;
    }

    public struct OtherDrone
    {
        public string Id;
        public double X, Y, Z, Heading;
        public int Battery;
    }

    public sealed class Scenario
    {
        /// <summary>If GAMA sends no weather (or boats, or drones) for this long, the headset goes on with its own.</summary>
        const float GamaPatience = 3f;
        /// <summary>Seconds between two reports of where the drone is.</summary>
        const float ReportEvery = 0.5f;

        public readonly MissionStart Start = new MissionStart();
        public static bool Gama { get { return DroneSimulationManager.Linked; } }

        /// <summary>GAMA ended the class's mission (the time is up, or the teacher said so).</summary>
        public event System.Action EndCalled;

        public readonly List<BoatPose> Boats = new List<BoatPose>();
        public readonly List<OtherDrone> Others = new List<OtherDrone>();
        /// <summary>The boats GAMA moves are shown while they come; otherwise the headset's own.</summary>
        public bool BoatsFromGama { get { return withClass && Boats.Count > 0 && Time.time - lastBoats < GamaPatience; } }
        public bool WithClass { get { return withClass; } }

        Flight flight;
        bool withClass;
        float lastWeather, lastBoats, lastDrones, reportTimer;
        // what has been told to GAMA already
        int toldGates;
        double[] toldQuality;
        bool[] toldFound, toldRing;
        bool[][] toldGot;

        public Scenario() { DroneSimulationManager.OnDroneMessage += OnMessage; }
        public void Dispose() { DroneSimulationManager.OnDroneMessage -= OnMessage; }

        static void Ask(string action, params string[] keyValues) { DroneSimulationManager.Ask(action, keyValues); }
        static string N(double v) { return ((int)System.Math.Round(v)).ToString(); }

        public void Hello(Lang lang) { Ask("dr_hello", "lang", lang == Lang.Fil ? "fil" : "en"); }
        public void Event(string kind, string detail) { Ask("dr_event", "kind", kind, "detail", detail ?? ""); }

        /// <summary>The operator pressed Begin: ready to fly (with the class, if GAMA is there).</summary>
        public void Ready(string mission, Mode mode)
        {
            Start.Ask(mission);
            Ask("dr_ready", "mission", mission, "mode", mode.ToString().ToLowerInvariant());
        }

        /// <summary>The operator went back to the board without flying.</summary>
        public void Leave() { Start.Reset(); Ask("dr_leave"); }

        /// <summary>A mission begins on this headset; byGama: it is the class's mission.</summary>
        public void Begin(Flight f, bool byGama)
        {
            flight = f; withClass = byGama && Gama;
            Boats.Clear(); Others.Clear();
            toldGates = 0;
            toldQuality = new double[TownPlan.Hazards.Length];
            toldFound = new bool[TownPlan.Survivors.Length]; toldRing = new bool[TownPlan.Survivors.Length];
            toldGot = new bool[TownPlan.Sites.Length][];
            for (int i = 0; i < toldGot.Length; i++) toldGot[i] = new bool[TownPlan.Sites[i].Needs.Length];
            reportTimer = 0;
        }

        /// <summary>The mission is over on this headset.</summary>
        public void Finish(MissionResult r)
        {
            if (flight != null && r != null)
                Ask("dr_result", "mission", flight.Mission.Id, "stars", r.Stars.ToString(), "score", r.Score.ToString(), "seconds", N(flight.Time));
            flight = null; withClass = false;
            Boats.Clear(); Others.Clear();
            Start.Reset();
        }

        public void Tick(float dt)
        {
            Start.Update(dt, Gama);
            if (flight == null) return;
            var w = flight.Mission.Weather;
            if (w.FromGama && Time.time - lastWeather > GamaPatience) { w.ForgetGama(); Debug.Log("DRONE VR: GAMA stopped sending the weather; using our own."); }
            if (Others.Count > 0 && Time.time - lastDrones > GamaPatience) Others.Clear();
            if (!Gama) return;
            reportTimer -= dt;
            if (reportTimer <= 0)
            {
                reportTimer = ReportEvery;
                var d = flight.Drone;
                Ask("dr_drone", "x", N(d.X * 10), "y", N(d.Y * 10), "z", N(d.Z * 10), "yaw", N(d.Heading), "battery", N(d.Battery * 100), "aloft", d.Landed || d.Crashed ? "0" : "1");
            }
            TellProgress();
        }

        // ---------- what this headset tells GAMA ----------

        void TellProgress()
        {
            var m = flight.Mission;
            var fs = m as FlightSchool;
            if (fs != null) while (toldGates < fs.Passed) { toldGates++; Ask("dr_gate", "n", toldGates.ToString()); }
            var hp = m as HazardPatrol;
            if (hp != null)
                for (int i = 0; i < hp.Quality.Length; i++)
                    if (hp.Quality[i] > toldQuality[i] + 0.004) { toldQuality[i] = hp.Quality[i]; Ask("dr_photo", "hazard", i.ToString(), "quality", N(hp.Quality[i] * 100)); }
            var sar = m as SearchRescue;
            if (sar != null)
                for (int i = 0; i < toldFound.Length; i++)
                {
                    if (sar.Found(i) && !toldFound[i]) { toldFound[i] = true; Ask("dr_found", "who", i.ToString()); }
                    if (sar.Ringed(i) && !toldRing[i]) { toldRing[i] = true; Ask("dr_ring", "who", i.ToString()); }
                }
            var rf = m as ReliefFlights;
            if (rf != null)
                for (int i = 0; i < rf.Got.Length; i++)
                    for (int k = 0; k < rf.Got[i].Length; k++)
                        if (rf.Got[i][k] && !toldGot[i][k]) { toldGot[i][k] = true; Ask("dr_delivered", "site", i.ToString(), "need", k.ToString()); }
        }

        // ---------- what GAMA sends ----------

        void OnMessage(string kind, JObject o)
        {
            switch (kind)
            {
                case "class":
                    Start.GamaSays((int)o["ready"], (int)o["players"], (string)o["mission"]);
                    break;
                case "start":
                    Start.GamaStarts((string)o["mission"], (double)o["t"] / 1000.0);
                    break;
                case "busy":
                    Start.GamaBusy();
                    break;
                case "end":
                    if (flight != null && withClass && EndCalled != null) EndCalled();
                    break;
                case "weather":
                    if (flight == null || !withClass) return;
                    flight.Mission.Weather.SetFromGama((double)o["wind"] / 100.0, (double)o["mean"] / 100.0, (double)o["from"] / 10.0, (double)o["level"] / 1000.0);
                    lastWeather = Time.time;
                    break;
                case "targets":
                    if (flight == null || !withClass || (string)o["mission"] != flight.Mission.Id) return;
                    var s = o["s"] as JArray;
                    if (s != null) ApplyTargets(s);
                    break;
                case "boats":
                    var p = o["p"] as JArray;
                    if (p == null || !withClass) return;
                    Boats.Clear();
                    for (int k = 0; k + 4 < p.Count; k += 5)
                        Boats.Add(new BoatPose { X = (int)p[k] / 10.0, Z = (int)p[k + 1] / 10.0, Heading = (int)p[k + 2], Phase = (int)p[k + 3], Job = (int)p[k + 4] });
                    lastBoats = Time.time;
                    break;
                case "drones":
                    var ids = o["ids"] as JArray; var q = o["p"] as JArray;
                    if (ids == null || q == null) return;
                    Others.Clear();
                    string me = DroneSimulationManager.MyId;
                    for (int k = 0; k < ids.Count && 5 * k + 4 < q.Count; k++)
                    {
                        var id = (string)ids[k];
                        if (id == me) continue;
                        Others.Add(new OtherDrone { Id = id, X = (int)q[5 * k] / 10.0, Y = (int)q[5 * k + 1] / 10.0, Z = (int)q[5 * k + 2] / 10.0, Heading = (int)q[5 * k + 3], Battery = (int)q[5 * k + 4] });
                    }
                    lastDrones = Time.time;
                    break;
            }
        }

        // What the class has done counts for this headset too: a hazard photographed, a group found, a need met.
        void ApplyTargets(JArray s)
        {
            var m = flight.Mission;
            var hp = m as HazardPatrol;
            if (hp != null)
                for (int i = 0; i < hp.Quality.Length && i < s.Count; i++)
                {
                    double q = (int)s[i] / 100.0;
                    if (q > hp.Quality[i] + 0.004) { hp.DoneByClass(i, q); toldQuality[i] = System.Math.Max(toldQuality[i], q); }
                }
            var sar = m as SearchRescue;
            if (sar != null)
                for (int i = 0; i < toldFound.Length && i < s.Count; i++)
                {
                    int b = (int)s[i];
                    bool found = (b & 1) != 0, ring = (b & 2) != 0;
                    if (found && !sar.Found(i)) { sar.DoneByClass(i, ring ? 2 : 1); toldFound[i] = true; toldRing[i] |= ring; }
                    else if (found && ring && !sar.Ringed(i)) { sar.DoneByClass(i, 2); toldRing[i] = true; }
                }
            var rf = m as ReliefFlights;
            if (rf != null)
                for (int i = 0; i < rf.Got.Length && i < s.Count; i++)
                {
                    int bits = (int)s[i];
                    for (int k = 0; k < rf.Got[i].Length; k++)
                        if (((bits >> k) & 1) != 0 && !rf.Got[i][k]) { rf.DoneByClass(i, k); toldGot[i][k] = true; }
                }
        }
    }
}
