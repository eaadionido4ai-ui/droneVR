// Checks of the four missions, flown the three ways: from the map table (the autopilot), by a pilot (here a
// small program that works the sticks), and both. Also the rescue boats, the score, and the texts.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DroneVR.Tests
{
    /// <summary>Stands in for the person: flies the drone with the sticks, as a careful pilot would.</summary>
    public static class Pilot
    {
        public static void Run(Flight f, double seconds, Func<bool> until = null)
        {
            int n = (int)Math.Round(seconds / DroneSpec.Dt);
            for (int i = 0; i < n && !f.Mission.Over; i++) { f.Step(DroneInput.None); if (until != null && until()) return; }
        }

        /// <summary>Flies to a point and stops there. Returns false if it did not get there in time.</summary>
        public static bool GoTo(Flight f, double x, double y, double z, double within = 1.5, double seconds = 120, double speed = 12)
        {
            int n = (int)Math.Round(seconds / DroneSpec.Dt);
            for (int i = 0; i < n && !f.Mission.Over && !f.Drone.Crashed; i++)
            {
                var d = f.Drone;
                double flat = Math.Sqrt((x - d.X) * (x - d.X) + (z - d.Z) * (z - d.Z));
                if (flat < within && Math.Abs(y - d.Y) < within && d.Speed < 1.5) return true;
                // climb before going, as the autopilot does
                bool low = d.Y < y - 3 && flat > 8;
                var sticks = Autopilot.Fly(d, low ? d.X : x, low ? d.Z : z, y, speed, flat > 6);
                f.Step(sticks);
            }
            return false;
        }

        /// <summary>Flies high to above a point, then comes straight down to a height over it.</summary>
        public static bool ComeDownOver(Flight f, double x, double y, double z, double within = 1.0)
        {
            double high = Math.Max(y + 20, TownPlan.PadY + 30);
            return GoTo(f, x, high, z, 1.5, 150) && GoTo(f, x, y, z, within, 60, 3);
        }

        public static bool LandOnPad(Flight f)
        {
            if (!GoTo(f, TownPlan.PadX, TownPlan.PadY + 12, TownPlan.PadZ, 1.2)) return false;
            for (int i = 0; i < 2000 && !f.Drone.Landed && !f.Drone.Crashed && !f.Mission.Over; i++)
            {
                var sticks = Autopilot.Fly(f.Drone, TownPlan.PadX, TownPlan.PadZ, f.Drone.Y, 3, false);
                sticks.Up = -0.5;
                f.Step(sticks);
            }
            return f.Drone.Landed && TownPlan.OnPad(f.Drone.X, f.Drone.Z);
        }

        /// <summary>Holds the drone still for a while.</summary>
        public static void Hold(Flight f, double x, double y, double z, double seconds)
        {
            int n = (int)Math.Round(seconds / DroneSpec.Dt);
            for (int i = 0; i < n && !f.Mission.Over; i++) f.Step(Autopilot.Fly(f.Drone, x, z, y, 3, false));
        }

        public static void WaitOnPad(Flight f) { Run(f, DroneSpec.SwapSeconds + 0.5); }
    }

    public class FlightSchoolTests
    {
        [Test]
        public void FromTheMapTableTheAutopilotFliesTheRings()
        {
            var f = new Flight("lipad", Mode.Desk);
            foreach (var g in TownPlan.Gates)
            {
                var w = f.AddWaypoint(g.X + 5, g.Z - 6);
                Assert.AreEqual(g.X, w.X, 1e-9, "a waypoint put near a ring goes through its middle"); Assert.AreEqual(g.Y, w.Alt, 1e-9);
            }
            Assert.Less(f.Estimate().Battery, 0.5);
            Assert.IsTrue(f.Launch());
            Pilot.Run(f, 300);
            var school = (FlightSchool)f.Mission;
            Assert.AreEqual(TownPlan.Gates.Length, school.Passed);
            Assert.IsTrue(school.LandedHome);
            Assert.IsNotNull(f.Mission.FinishedAt, "the mission is over when the drone is back on the pad");
            var r = f.Result();
            TestContext.WriteLine("LIPAD from the desk: " + r.Score + " points, finished at " + f.Mission.FinishedAt);
            Assert.AreEqual(3, r.Stars);
            Assert.AreEqual(1, r.Tasks, 1e-9); Assert.AreEqual(1, r.Safety, 1e-9);
        }

        [Test]
        public void APilotFliesTheRingsInOrder()
        {
            var f = new Flight("lipad", Mode.Pilot);
            Assert.IsTrue(f.Manual, "an FPV pilot has the sticks from the start");
            var school = (FlightSchool)f.Mission;
            // the second ring first: it does not count
            var second = TownPlan.Gates[1].Center;
            Assert.IsTrue(Pilot.GoTo(f, second.X, second.Y, second.Z));
            Assert.AreEqual(0, school.Passed, "the rings count in order");
            foreach (var g in TownPlan.Gates) Assert.IsTrue(Pilot.GoTo(f, g.Center.X, g.Center.Y, g.Center.Z), "ring at " + g.X + ", " + g.Z);
            Assert.AreEqual(6, school.Passed);
            Assert.IsNull(f.Mission.FinishedAt, "not over until the drone has landed on the pad");
            Assert.IsTrue(Pilot.LandOnPad(f));
            Pilot.Run(f, 0.1);
            Assert.IsNotNull(f.Mission.FinishedAt);
            Assert.GreaterOrEqual(f.Result().Stars, 2);
        }

        [Test]
        public void LandingAwayFromThePadLosesMarks()
        {
            var f = new Flight("lipad", Mode.Pilot);
            foreach (var g in TownPlan.Gates) Pilot.GoTo(f, g.Center.X, g.Center.Y, g.Center.Z);
            Pilot.GoTo(f, -120, Terrain.Ground(-120, -150) + 6, -150);
            for (int i = 0; i < 600 && !f.Drone.Landed; i++) f.Step(new DroneInput(0, 0, -0.5, 0));
            Assert.IsTrue(f.Drone.Landed);
            f.Mission.End();
            var r = f.Result();
            Assert.Less(r.Safety, 0.75, "the drone is not home");
            Assert.Less(r.Tasks, 1);
            Assert.Less(r.Stars, 3);
        }
    }

    public class HazardPatrolTests
    {
        static DroneState At(double x, double y, double z)
        {
            var d = new DroneState(); d.ResetOnPad(); d.X = x; d.Y = y; d.Z = z; d.Landed = false;
            return d;
        }

        [Test]
        public void AGoodPhotographIsTakenStillFromTheRightDistanceWithTheHazardInTheMiddle()
        {
            var h = TownPlan.Hazards.First(q => q.Id == "wire");
            var p = h.Point;
            Assert.AreEqual(1, HazardPatrol.QualityOf(h, At(p.X + 16, p.Y + 12, p.Z), 0), 1e-9, "20 m away, still, centered");
            Assert.AreEqual(0.75, HazardPatrol.QualityOf(h, At(p.X + 16, p.Y + 12, p.Z), HazardPatrol.Cone / 2), 1e-9, "off the middle");
            Assert.AreEqual(0, HazardPatrol.QualityOf(h, At(p.X + 16, p.Y + 12, p.Z), HazardPatrol.Cone + 1), 1e-9, "out of the picture");
            Assert.Less(HazardPatrol.QualityOf(h, At(p.X + 38, p.Y + 12, p.Z), 0), 0.7, "from far");
            Assert.AreEqual(0, HazardPatrol.QualityOf(h, At(p.X + 50, p.Y + 12, p.Z), 0), 1e-9, "from too far");
            Assert.AreEqual(0.5, HazardPatrol.QualityOf(h, At(p.X + 5, p.Y + 4, p.Z), 0), 1e-9, "from dangerously near");
            var moving = At(p.X + 16, p.Y + 12, p.Z); moving.Vx = 10;
            Assert.AreEqual(0.4, HazardPatrol.QualityOf(h, moving, 0), 1e-9, "blurred by speed");
        }

        [Test]
        public void APilotPhotographsWhatIsInTheMiddleOfThePicture()
        {
            var f = new Flight("bantay", Mode.Pilot);
            var patrol = (HazardPatrol)f.Mission;
            var h = TownPlan.Hazards.First(q => q.Id == "slide");
            Assert.IsTrue(Pilot.GoTo(f, h.X - 16, h.Point.Y + 12, h.Z));
            Pilot.Hold(f, h.X - 16, h.Point.Y + 12, h.Z, 2);
            Assert.IsFalse(f.Primary(new Aim(f.Drone.Position, new P3(0, 0, 1))), "looking away: nothing in the picture");
            Assert.AreEqual(0, patrol.Count);
            Assert.IsTrue(f.Primary(Aim.At(f.Drone, h.Point)));
            int i = Array.IndexOf(TownPlan.Hazards, h);
            Assert.Greater(patrol.Quality[i], 0.9, "a clear photograph");
            Assert.AreEqual(1, patrol.Count); Assert.AreEqual(2, patrol.Photos);
            Assert.AreEqual(2, patrol.StateOf(i));
            Assert.AreEqual(0, patrol.Strikes);
        }

        [Test]
        public void FlyingTooCloseToAHazardIsAStrike()
        {
            var f = new Flight("bantay", Mode.Pilot);
            var h = TownPlan.Hazards.First(q => q.Id == "spill");
            Assert.IsTrue(Pilot.ComeDownOver(f, h.X + 3, h.Point.Y + 5, h.Z));
            Assert.AreEqual(1, f.Mission.Strikes);
            Pilot.Hold(f, h.X + 3, h.Point.Y + 5, h.Z, 3);
            Assert.AreEqual(1, f.Mission.Strikes, "once for each hazard");
            Assert.IsTrue(f.Mission.Events.Any(e => e.Key == "bantay.tooclose"));
            Assert.Less(f.Result().Safety, 1);
        }

        /// <summary>Flies routes of hover waypoints from the table, in as many flights as the battery asks for.</summary>
        static Flight Patrol(Func<HazardSpot, double[]> where, double alt)
        {
            var f = new Flight("bantay", Mode.Desk);
            f.WaypointAlt = alt;
            var left = TownPlan.Hazards.ToList();
            double x = TownPlan.PadX, z = TownPlan.PadZ;
            while (left.Count > 0 && !f.Mission.Over)
            {
                // the nearest next, while the battery estimate allows
                while (left.Count > 0)
                {
                    var h = left.OrderBy(q => Math.Sqrt((q.X - x) * (q.X - x) + (q.Z - z) * (q.Z - z))).First();
                    var p = where(h);
                    var w = f.AddWaypoint(p[0], p[1]);
                    Assert.AreEqual(WaypointKind.Hover, w.Kind, "on a patrol the drone stops at each waypoint to look");
                    if (f.Estimate().Battery > 0.8 && f.Auto.Route.Count > 1) { f.UndoWaypoint(); break; }
                    left.Remove(h); x = p[0]; z = p[1];
                }
                Assert.IsTrue(f.Launch());
                Pilot.Run(f, 400, () => f.Drone.Landed && f.Drone.Flown > 50 && !f.Auto.Active);
                Assert.IsFalse(f.Drone.Crashed, "the patrol hit something: " + f.Drone.HitBy + " at " + f.Drone.Position);
                Pilot.WaitOnPad(f);
                x = TownPlan.PadX; z = TownPlan.PadZ;
            }
            return f;
        }

        [Test]
        public void FromTheMapTableTheAutopilotPhotographsWhatItPassesNear()
        {
            // waypoints beside each hazard, 18 m to the side that is clear, 15 m up
            var quake = Town.For(TownPlan.Mission("bantay"));
            var f = Patrol(h =>
            {
                for (int a = 0; a < 360; a += 30)
                {
                    double x = h.X + 18 * Math.Cos(a * Compass.Deg), z = h.Z + 18 * Math.Sin(a * Compass.Deg);
                    if (quake.Hit(x, Terrain.Ground(x, z) + 15, z, 2) == HitKind.None && Terrain.Ground(x, z) + 15 > quake.SurfaceFromAbove(x, z) + 4) return new[] { x, z };
                }
                return new[] { h.X + 18, h.Z };
            }, 15);
            var patrol = (HazardPatrol)f.Mission;
            TestContext.WriteLine("beside the hazards: " + string.Join(", ", patrol.Quality.Select((q, i) => TownPlan.Hazards[i].Id + " " + q.ToString("0.00"))) + "; time " + f.Time.ToString("0") + " s, strikes " + f.Mission.Strikes);
            Assert.AreEqual(TownPlan.Hazards.Length, patrol.Count, "every hazard is photographed");
            Assert.IsNotNull(f.Mission.FinishedAt);
            foreach (var q in patrol.Quality) Assert.LessOrEqual(q, HazardPatrol.AutoFraming + 1e-9, "the autopilot's camera never frames as well as a pilot");
            var r = f.Result();
            TestContext.WriteLine("  " + r.Score + " points, " + r.Stars + " stars");
            Assert.GreaterOrEqual(r.Stars, 2, "good enough to act on");
            Assert.Less(r.Score, 95, "but the clearest pictures are a pilot's");
        }

        [Test]
        public void ARouteOverTheReportsAloneMissesSome()
        {
            // the operator does not know where the hazards are, only where the residents said to look
            var f = Patrol(h => new[] { h.ReportX, h.ReportZ }, 30);
            var patrol = (HazardPatrol)f.Mission;
            f.Mission.End();
            var r = f.Result();
            TestContext.WriteLine("over the reports at 30 m: " + string.Join(", ", patrol.Quality.Select((q, i) => TownPlan.Hazards[i].Id + " " + q.ToString("0.00"))) + "; " + r.Score + " points, " + r.Stars + " stars");
            Assert.Greater(patrol.Quality.Count(q => q > 0), 3, "most are seen");
            Assert.Less(patrol.Quality.Average(), 0.62, "but not well: someone has to look closer");
            Assert.Less(r.Stars, 3);
        }

        [Test]
        public void AClassmatesPhotographCountsForTheClass()
        {
            var patrol = new HazardPatrol();
            patrol.DoneByClass(2, 0.9);
            Assert.AreEqual(1, patrol.Count); Assert.AreEqual(0.9, patrol.Quality[2], 1e-9);
            patrol.DoneByClass(2, 0.6);
            Assert.AreEqual(0.9, patrol.Quality[2], 1e-9, "the better picture is kept");
        }
    }

    public class RescueFleetTests
    {
        [Test]
        public void TheBoatsWaysStayInWaterDeepEnoughAndClearOfHouses()
        {
            double level = TownPlan.Mission("sagip").FloodLevels[0];
            var town = Town.For(TownPlan.Mission("sagip"));
            for (int s = 0; s < TownPlan.Staging.Length; s++)
                for (int v = 0; v < TownPlan.Survivors.Length; v++)
                {
                    var r = TownPlan.BoatRoutes[s][v];
                    string who = "from landing " + s + " to " + TownPlan.Survivors[v].Id;
                    Assert.Less(Math.Sqrt(Math.Pow(r[0] - TownPlan.Staging[s][0], 2) + Math.Pow(r[1] - TownPlan.Staging[s][1], 2)), 8, who + ": starts at the landing");
                    Assert.Less(Math.Sqrt(Math.Pow(r[r.Length - 2] - TownPlan.Survivors[v].X, 2) + Math.Pow(r[r.Length - 1] - TownPlan.Survivors[v].Z, 2)), 16, who + ": ends beside the people");
                    for (int k = 2; k + 1 < r.Length; k += 2)
                    {
                        double len = Math.Sqrt(Math.Pow(r[k] - r[k - 2], 2) + Math.Pow(r[k + 1] - r[k - 1], 2));
                        for (double a = 0; a <= len; a += 1.0)
                        {
                            double x = r[k - 2] + (r[k] - r[k - 2]) * a / len, z = r[k - 1] + (r[k + 1] - r[k - 1]) * a / len;
                            Assert.Greater(Terrain.Depth(x, z, level), 0.2, who + ": aground at " + x.ToString("0") + ", " + z.ToString("0"));
                            foreach (var b in town.Buildings) if (b.Roof != RoofKind.Open) Assert.IsFalse(b.Covers(x, z, 0.3), who + ": through " + b.Id);
                        }
                    }
                }
        }

        [Test]
        public void TheBoatThatCanBeThereSoonestIsSent()
        {
            var fleet = new RescueFleet();
            int jeep = Array.FindIndex(TownPlan.Survivors, s => s.Id == "jeep"), roof1 = Array.FindIndex(TownPlan.Survivors, s => s.Id == "roof1"), grove = Array.FindIndex(TownPlan.Survivors, s => s.Id == "grove");
            double eta;
            int b = fleet.Send(jeep, out eta);
            Assert.AreEqual(1, fleet.Boats[b].Home, "the jeepney is beside the east landing");
            Assert.AreEqual(RescueFleet.Way(1, jeep) / RescueFleet.Speed, eta, 1e-9);
            int b2 = fleet.Send(roof1, out eta);
            Assert.AreEqual(0, fleet.Boats[b2].Home, "the roof by the river is nearer the west landing");
            // the east boat is busy with the jeepney: is it still the soonest for the grove beside it?
            int b3 = fleet.Send(grove, out eta);
            double viaEast = fleet.Boats[b].FreeAt, fromWest = RescueFleet.Way(0, grove) / RescueFleet.Speed;
            TestContext.WriteLine("grove: boat " + b3 + ", there at " + eta.ToString("0") + " s; a west boat would take " + fromWest.ToString("0") + " s");
            Assert.LessOrEqual(eta, fromWest + 1e-9, "whichever boat gets there first");
        }

        [Test]
        public void TheBoatsBringEveryoneBack()
        {
            var fleet = new RescueFleet();
            double eta;
            var etas = new double[TownPlan.Survivors.Length];
            for (int i = 0; i < TownPlan.Survivors.Length; i++) { fleet.Send(i, out eta); etas[i] = eta; }
            Assert.IsTrue(fleet.Busy);
            fleet.Finish();
            Assert.IsFalse(fleet.Busy);
            for (int i = 0; i < TownPlan.Survivors.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(fleet.AshoreAt[i]), TownPlan.Survivors[i].Id + " is ashore");
                Assert.AreEqual(etas[i] + RescueFleet.LoadSeconds, fleet.AboardAt[i], 1.5, TownPlan.Survivors[i].Id + ": aboard when the boat said it would be there");
                Assert.Greater(fleet.AshoreAt[i], fleet.AboardAt[i]);
            }
            foreach (var b in fleet.Boats)
            {
                Assert.AreEqual(TownPlan.Staging[b.Home][0], b.X, 6, "each boat ends at its own landing");
                Assert.AreEqual(BoatPhase.Waiting, b.Phase);
            }
            TestContext.WriteLine("all eight groups found at once: the last is aboard at " + fleet.AboardAt.Max().ToString("0") + " s, ashore at " + fleet.AshoreAt.Max().ToString("0") + " s");
        }
    }

    public class SearchRescueTests
    {
        static int Index(string id) { return Array.FindIndex(TownPlan.Survivors, s => s.Id == id); }

        static DroneState Over(string id, double above, double aside = 0)
        {
            var s = TownPlan.Survivors[Index(id)];
            var d = new DroneState(); d.ResetOnPad(); d.X = s.X + aside; d.Y = s.Point.Y + above; d.Z = s.Z; d.Landed = false;
            return d;
        }

        [Test]
        public void PeopleInTheOpenAreSeenFromFarAndHiddenOnesOnlyByTheThermalCameraFromNear()
        {
            var sar = new SearchRescue();
            Assert.IsTrue(sar.CanSee(Index("roof1"), Over("roof1", 40, 40), false));
            Assert.IsFalse(sar.CanSee(Index("roof1"), Over("roof1", 40, 80), false), "too far");
            Assert.IsFalse(sar.CanSee(Index("grove"), Over("grove", 15), false), "under the leaves");
            Assert.IsTrue(sar.CanSee(Index("grove"), Over("grove", 15), true), "warm bodies show through");
            Assert.IsFalse(sar.CanSee(Index("grove"), Over("grove", 40), true), "but not from high up");
        }

        [Test]
        public void MarkingAGroupSendsABoatToIt()
        {
            var sar = new SearchRescue();
            var d = Over("market", 25, 10);
            Assert.IsFalse(sar.Primary(d, new Aim(d.Position, new P3(1, 0, 0)), false), "looking the other way");
            Assert.AreEqual("sagip.nothing", sar.Events.Last().Key);
            Assert.IsTrue(sar.Primary(d, Aim.At(d, TownPlan.Survivors[Index("market")].Point), false));
            Assert.IsTrue(sar.Found(Index("market")));
            Assert.AreEqual(1, sar.Count); Assert.AreEqual(1, sar.StateOf(Index("market")));
            Assert.IsTrue(sar.Fleet.Busy);
            Assert.IsFalse(sar.Primary(d, Aim.At(d, TownPlan.Survivors[Index("market")].Point), false), "already marked");
        }

        [Test]
        public void SomethingWarmOutOfSightAsksForTheThermalCamera()
        {
            var sar = new SearchRescue();
            var d = Over("gym", 14, 14);
            var aim = Aim.At(d, TownPlan.Survivors[Index("gym")].Point);
            Assert.IsFalse(sar.Primary(d, aim, false));
            Assert.AreEqual("sagip.trythermal", sar.Events.Last().Key);
            Assert.IsTrue(sar.Primary(d, aim, true));
        }

        [Test]
        public void EveryGroupCanBeSavedIfItIsFoundInGoodTime()
        {
            for (int i = 0; i < TownPlan.Survivors.Length; i++)
            {
                var f = new Flight("sagip", Mode.Pilot);
                var sar = (SearchRescue)f.Mission;
                Pilot.Run(f, 90);
                sar.DoneByClass(i, TownPlan.Survivors[i].Ring ? 2 : 1);
                Pilot.Run(f, 500, () => sar.Aboard(i));
                Assert.IsTrue(sar.Aboard(i), TownPlan.Survivors[i].Id + ": no boat came");
                TestContext.WriteLine(TownPlan.Survivors[i].Id + ": found at 1:30, aboard at " + Strings.Clock(sar.Fleet.AboardAt[i]) + ", limit " + TownPlan.Survivors[i].LimitMinutes + " min");
                Assert.IsTrue(sar.InTime(i), TownPlan.Survivors[i].Id + ": found at 1:30 and still too late");
            }
        }

        [Test]
        public void FoundTooLateIsWorthLess()
        {
            int roof = Index("roof1");
            var early = new Flight("sagip", Mode.Pilot); var late = new Flight("sagip", Mode.Pilot);
            Pilot.Run(early, 60); ((SearchRescue)early.Mission).DoneByClass(roof, 1);
            Pilot.Run(late, TownPlan.Survivors[roof].LimitMinutes * 60 + 5);
            Assert.IsTrue(late.Mission.Events.Any(e => e.Key == "sagip.late"), "nobody came in their six minutes");
            ((SearchRescue)late.Mission).DoneByClass(roof, 1);
            early.Mission.End(); late.Mission.End();
            Assert.IsTrue(((SearchRescue)early.Mission).InTime(roof)); Assert.IsFalse(((SearchRescue)late.Mission).InTime(roof));
            Assert.Greater(early.Result().Tasks, late.Result().Tasks);
            Assert.Greater(late.Result().Tasks, 0, "but found late is better than never found");
        }

        [Test]
        public void TheManInFastWaterNeedsALifeRingBeforeABoatCanComeForHim()
        {
            int tree = Index("tree");
            var s = TownPlan.Survivors[tree];
            var f = new Flight("sagip", Mode.Pilot);
            var sar = (SearchRescue)f.Mission;
            Assert.IsTrue(f.Mission.Load("ring", f.Drone));
            Assert.IsTrue(Pilot.GoTo(f, s.X + 20, s.Point.Y + 30, s.Z - 20, 1.5, 150));
            Assert.IsTrue(f.Primary(Aim.At(f.Drone, s.Point)));
            Assert.IsTrue(sar.Found(tree));
            Assert.AreEqual("sagip.needsring", f.Mission.Events.Last(e => e.Kind == Note.Warn).Key);
            Assert.IsFalse(sar.Fleet.Busy, "no boat is sent yet"); Assert.IsFalse(sar.InTime(tree));
            // down beside the tree, clear of its crown, and let the ring go from just above the water
            double water = f.Mission.Weather.Level(f.Time);
            Assert.IsTrue(Pilot.ComeDownOver(f, s.X, water + 4, s.Z - 4));
            Assert.IsNotNull(f.Release());
            Pilot.Hold(f, s.X, water + 4, s.Z - 4, 3);
            TestContext.WriteLine("ring by hand: " + f.Mission.Packages[0].Fate + ", " + f.Mission.Packages[0].Off.ToString("0.0") + " m off, at " + Strings.Clock(f.Time));
            Assert.AreEqual(PackageFate.Reached, f.Mission.Packages[0].Fate);
            Assert.IsTrue(sar.Fleet.Busy, "now a boat goes");
            Assert.IsTrue(sar.InTime(tree), "and it will be there in time");
            Pilot.GoTo(f, s.X, water + 30, s.Z - 4);
            Pilot.Run(f, 120, () => sar.Aboard(tree));
            Assert.IsTrue(sar.Aboard(tree)); Assert.AreEqual(2, sar.StateOf(tree));

            // found, but no ring: nobody can reach him
            var g = new Flight("sagip", Mode.Pilot);
            ((SearchRescue)g.Mission).DoneByClass(tree, 1);
            g.Mission.End();
            var r = g.Result();
            Assert.IsFalse(((SearchRescue)g.Mission).InTime(tree));
            Assert.IsTrue(r.Lines.Any(l => l.Key == "end.sagip.noring"));
            // a ring thrown far from him is no use
            g = new Flight("sagip", Mode.Pilot);
            g.Mission.Load("ring", g.Drone);
            Pilot.GoTo(g, -100, TownPlan.PadY + 20, -140);
            g.Release();
            Pilot.Run(g, 5);
            Assert.AreEqual(PackageFate.Missed, g.Mission.Packages[0].Fate);
            Assert.AreEqual("sagip.ring.missed", g.Mission.Events.Last().Key);
        }

        /// <summary>A search pattern drawn on the table: lanes from west to east over the flooded streets.</summary>
        static Flight Sweep(double alt, double[] lanes, out int flights)
        {
            var f = new Flight("sagip", Mode.Desk);
            f.WaypointAlt = alt;
            flights = 0;
            var points = new List<double[]>();
            bool east = true;
            foreach (double z in lanes) { points.Add(new[] { east ? -130.0 : 200.0, z }); points.Add(new[] { east ? 200.0 : -130.0, z }); east = !east; }
            int k = 0;
            while (k < points.Count && !f.Mission.Over)
            {
                while (k < points.Count)
                {
                    f.AddWaypoint(points[k][0], points[k][1]);
                    if (f.Estimate().Battery > 0.8 && f.Auto.Route.Count > 1) { f.UndoWaypoint(); break; }
                    k++;
                }
                f.Launch(); flights++;
                Pilot.Run(f, 500, () => f.Drone.Landed && f.Drone.Flown > 50 && !f.Auto.Active);
                Assert.IsFalse(f.Drone.Crashed, "the sweep hit something: " + f.Drone.HitBy + " at " + f.Drone.Position);
                Pilot.WaitOnPad(f);
            }
            return f;
        }

        [Test]
        public void AHighSweepFromTheMapTableFindsThoseInTheOpenButNotTheHidden()
        {
            int flights;
            var f = Sweep(45, new[] { -20.0, 40, 95, 165 }, out flights);
            var sar = (SearchRescue)f.Mission;
            TestContext.WriteLine("sweep at 45 m: " + string.Join(", ", TownPlan.Survivors.Select((s, i) => s.Id + (sar.Found(i) ? " " + Strings.Clock(sar.FoundAt[i]) : " -"))) + "; " + flights + " flights, " + Strings.Clock(f.Time));
            foreach (var id in new[] { "roof1", "market", "roof2", "tree", "jeep", "sitio" }) Assert.IsTrue(sar.Found(Index(id)), id + " is in the open");
            Assert.IsTrue(sar.NeedsRing(Index("tree")), "the man in the tree is found, but no boat can come for him yet");
            Assert.IsFalse(sar.Found(Index("grove")), "under the leaves"); Assert.IsFalse(sar.Found(Index("gym")), "under the roof");
            f.Mission.End();
            var r = f.Result();
            TestContext.WriteLine("  " + r.Score + " points, " + r.Stars + " stars: " + string.Join(" | ", r.Text(Lang.En)));
            Assert.Less(r.Stars, 3, "people were left behind");
        }

        [Test]
        public void ALowPassWithTheThermalCameraFindsTheHidden()
        {
            var f = new Flight("sagip", Mode.Desk);
            var sar = (SearchRescue)f.Mission;
            f.WaypointAlt = 20;
            var grove = TownPlan.Survivors[Index("grove")]; var gym = TownPlan.Survivors[Index("gym")];
            f.AddWaypoint(grove.X - 30, grove.Z - 8); f.AddWaypoint(grove.X + 8, grove.Z - 8); f.AddWaypoint(gym.X + 16, gym.Z - 30); f.AddWaypoint(gym.X + 16, gym.Z + 10);
            f.Launch();
            Pilot.Run(f, 300, () => f.Drone.Landed && f.Drone.Flown > 50 && !f.Auto.Active);
            Assert.IsFalse(f.Drone.Crashed, "hit " + f.Drone.HitBy + " at " + f.Drone.Position);
            Assert.IsTrue(sar.Found(Index("grove")), "the family under the trees");
            Assert.IsTrue(sar.Found(Index("gym")), "the people in the covered court");
        }

        [Test]
        public void TheAutopilotDropsALifeRingBesideTheManInTheTree()
        {
            var f = new Flight("sagip", Mode.Desk);
            var sar = (SearchRescue)f.Mission;
            int tree = Index("tree");
            var s = TownPlan.Survivors[tree];
            Assert.AreEqual(WaypointKind.Fly, f.AddWaypoint(s.X + 2, s.Z + 2).Kind, "until he is found, a waypoint there is only a waypoint");
            f.ClearRoute();
            sar.DoneByClass(tree, 1);
            Assert.IsTrue(f.Mission.Load("ring", f.Drone));
            var w = f.AddWaypoint(s.X + 2, s.Z + 2);
            Assert.AreEqual(WaypointKind.Drop, w.Kind);
            Assert.AreEqual(s.Z - Flight.RingAside, w.Z, 1e-9, "beside him, clear of the tree");
            f.Launch();
            Pilot.Run(f, 200, () => f.Drone.Landed && f.Drone.Flown > 50 && !f.Auto.Active);
            Assert.IsFalse(f.Drone.Crashed, "hit " + f.Drone.HitBy + " at " + f.Drone.Position);
            TestContext.WriteLine("autopilot ring: " + f.Mission.Packages[0].Fate + ", " + f.Mission.Packages[0].Off.ToString("0.0") + " m off, at " + Strings.Clock(sar.RingAt[tree]));
            Assert.AreEqual(PackageFate.Reached, f.Mission.Packages[0].Fate);
            Assert.IsTrue(sar.InTime(tree), "the boat is sent as soon as the ring is there");
        }
    }

    public class ReliefFlightsTests
    {
        static DropSite Site(string id) { return TownPlan.Sites.First(s => s.Id == id); }

        /// <summary>A package let go by hand from a still drone at a point above a site, in the mission's wind.</summary>
        static Package DropAt(Flight f, DropSite site, double dx, double dz, double above, string item)
        {
            Assert.IsTrue(f.Mission.Load(item, f.Drone), "loading " + item);
            Assert.IsTrue(Pilot.ComeDownOver(f, site.X + dx, site.Point.Y + above, site.Z + dz), "coming down over " + site.Id);
            Pilot.Hold(f, site.X + dx, site.Point.Y + above, site.Z + dz, 1.5);
            var p = f.Release();
            Assert.IsNotNull(p);
            Pilot.Hold(f, site.X + dx, site.Point.Y + above, site.Z + dz, 3);
            Pilot.GoTo(f, site.X + dx, Math.Max(site.Point.Y + 25, TownPlan.PadY + 30), site.Z + dz);
            return p;
        }

        [Test]
        public void OverASiteTheDroneLetsGoWhatThatSiteAskedFor()
        {
            var f = new Flight("hatid", Mode.Pilot);
            f.Mission.Weather.WindScale = 0;
            Assert.IsTrue(f.Mission.Load("water", f.Drone)); Assert.IsTrue(f.Mission.Load("radio", f.Drone)); Assert.IsTrue(f.Mission.Load("firstaid", f.Drone));
            var health = Site("health");
            Assert.IsTrue(Pilot.GoTo(f, health.X, health.Point.Y + 25, health.Z, 1.5, 150));
            Assert.IsTrue(Pilot.GoTo(f, health.X, health.Point.Y + 3, health.Z, 1, 60, 3));
            Assert.AreEqual("firstaid", f.Release().Item, "the health center asked for a first aid kit and a radio, not water");
            Assert.AreEqual("radio", f.Release().Item);
            Assert.AreEqual("water", f.Release().Item, "what is left goes last");
            Assert.IsNull(f.Release(), "nothing left on the hooks");
            Pilot.Hold(f, health.X, health.Point.Y + 3, health.Z, 3);
            var relief = (ReliefFlights)f.Mission;
            Assert.AreEqual(2, relief.Count);
            Assert.AreEqual(2, relief.StateOf(Array.IndexOf(TownPlan.Sites, health)), "the health center has everything");
            Assert.AreEqual(PackageFate.NotNeeded, f.Mission.Packages[2].Fate, "the water was not asked for here");
            Assert.AreEqual(1, relief.Lost);
        }

        [Test]
        public void APackageMustLandWholeOnTheMark()
        {
            var f = new Flight("hatid", Mode.Pilot);
            f.Mission.Weather.WindScale = 0;
            var evac = Site("evac");
            Assert.AreEqual(PackageFate.Missed, DropAt(f, evac, evac.R + 4, 0, 3, "food").Fate, "beside the mark");
            Assert.IsTrue(Pilot.LandOnPad(f));
            Assert.AreEqual(PackageFate.Broken, DropAt(f, evac, 0, 0, 12, "food").Fate, "from 12 m up");
            Assert.IsTrue(Pilot.LandOnPad(f));
            var good = DropAt(f, evac, 1, 0, 3, "food");
            Assert.AreEqual(PackageFate.Delivered, good.Fate);
            Assert.AreEqual(1, good.Off, 0.6);
            var relief = (ReliefFlights)f.Mission;
            Assert.AreEqual(1, relief.Count); Assert.AreEqual(2, relief.Lost);
            Assert.AreEqual(new[] { "water" }, relief.StillNeeds(Array.IndexOf(TownPlan.Sites, evac)).ToArray());
        }

        [Test]
        public void OnTheRoofMeansOnTheRoof()
        {
            var f = new Flight("hatid", Mode.Pilot);
            f.Mission.Weather.WindScale = 0;
            var roof = Site("roof");
            // just past the edge of the roof: it falls into the flood beside the house
            var p = DropAt(f, roof, 6.5, 0, 3, "food");
            Assert.AreEqual(PackageFate.Sunk, p.Fate);
            Assert.IsTrue(Pilot.LandOnPad(f));
            Assert.AreEqual(PackageFate.Delivered, DropAt(f, roof, 0, 0, 3, "food").Fate);
        }

        [Test]
        public void InTheTyphoonsWindAPilotWhoAimsUpwindLandsNearerTheMark()
        {
            var roof = Site("roof");
            var straight = new Flight("hatid", Mode.Pilot);
            var a = DropAt(straight, roof, 0, 0, 3.5, "food");
            // the wind blows from the west-south-west: upwind is to the west and a little south
            double wx, wz;
            straight.Mission.Weather.SteadyAt(0, 6, out wx, out wz);
            double len = Math.Sqrt(wx * wx + wz * wz);
            var aimed = new Flight("hatid", Mode.Pilot);
            var b = DropAt(aimed, roof, -wx / len * 2.0, -wz / len * 2.0, 3.5, "food");
            double ao = Math.Sqrt((a.X - roof.X) * (a.X - roof.X) + (a.Z - roof.Z) * (a.Z - roof.Z));
            TestContext.WriteLine("on the roof in the wind: straight above " + a.Fate + " " + ao.ToString("0.0") + " m off; aimed 2 m upwind " + b.Fate + " " + b.Off.ToString("0.0") + " m off");
            Assert.AreEqual(PackageFate.Delivered, b.Fate);
            Assert.Less(b.Off, 1.5);
            Assert.Greater(ao, b.Off + 0.5, "straight above the mark, the wind carries it away from the middle");
        }

        [Test]
        public void InTheTyphoonsWindADropOnTheRoofIsSaferInAPilotsHands()
        {
            // the same drop made at different moments of the gusty wind: by the autopilot, which drops from
            // straight above the mark, and by a pilot, who aims upwind by what the wind will carry the package
            var roof = Site("roof");
            int autoOk = 0, pilotOk = 0, n = 0; double autoOff = 0, pilotOff = 0;
            var line = new List<string>();
            for (double wait = 0; wait < 33; wait += 3, n++)
            {
                var auto = new Flight("hatid", Mode.Desk);
                Pilot.Run(auto, wait);
                auto.Mission.Load("food", auto.Drone);
                auto.WaypointAlt = 30;
                auto.AddWaypoint(roof.X, roof.Z);
                auto.Launch();
                Pilot.Run(auto, 300, () => auto.Drone.Landed && auto.Drone.Flown > 50 && !auto.Auto.Active);
                var a = auto.Mission.Packages[0];
                double ao = Math.Sqrt((a.X - roof.X) * (a.X - roof.X) + (a.Z - roof.Z) * (a.Z - roof.Z));
                if (a.Fate == PackageFate.Delivered) autoOk++;
                autoOff += ao;

                var hand = new Flight("hatid", Mode.Pilot);
                Pilot.Run(hand, wait);
                double wx, wz; hand.Mission.Weather.SteadyAt(0, 6, out wx, out wz);
                double len = Math.Sqrt(wx * wx + wz * wz);
                var b = DropAt(hand, roof, -wx / len * 2.0, -wz / len * 2.0, 3.5, "food");
                double bo = Math.Sqrt((b.X - roof.X) * (b.X - roof.X) + (b.Z - roof.Z) * (b.Z - roof.Z));
                if (b.Fate == PackageFate.Delivered) pilotOk++;
                pilotOff += bo;
                line.Add(ao.ToString("0.0") + (a.Fate == PackageFate.Delivered ? "" : "x") + "/" + bo.ToString("0.0") + (b.Fate == PackageFate.Delivered ? "" : "x"));
            }
            TestContext.WriteLine("roof drops, autopilot/pilot, metres off (x: not delivered): " + string.Join(" ", line));
            TestContext.WriteLine("  autopilot " + autoOk + " of " + n + ", mean " + (autoOff / n).ToString("0.0") + " m; pilot " + pilotOk + " of " + n + ", mean " + (pilotOff / n).ToString("0.0") + " m");
            Assert.AreEqual(n, pilotOk, "the pilot's drops all land on the mark");
            Assert.Less(autoOk, n, "some of the autopilot's do not: the wind carries them off the roof's mark");
            Assert.Greater(autoOk, n / 2, "though most do");
            Assert.Less(pilotOff / n, autoOff / n - 0.4, "and the pilot's land nearer its middle");
        }

        /// <summary>A whole day of relief flights planned on the map table, flown by the autopilot.</summary>
        [Test]
        public void FromTheMapTableTheAutopilotDeliversToTheOpenPlaces()
        {
            var f = new Flight("hatid", Mode.Desk);
            f.WaypointAlt = 30;
            var relief = (ReliefFlights)f.Mission;
            var flights = new[]
            {
                new[] { "evac:food", "evac:water" },
                new[] { "sitio:water", "sitio:medicine" },
                new[] { "health:firstaid", "health:radio", "roof:food" },
            };
            foreach (var flight in flights)
            {
                foreach (var stop in flight)
                {
                    var site = Site(stop.Split(':')[0]);
                    Assert.IsTrue(f.Mission.Load(stop.Split(':')[1], f.Drone), "loading for " + stop);
                    Assert.AreEqual(WaypointKind.Drop, f.AddWaypoint(site.X, site.Z).Kind);
                }
                var e = f.Estimate();
                Assert.Less(e.Battery, 0.85, "one battery is enough for " + string.Join(", ", flight));
                double before = f.Drone.Battery;
                Assert.IsTrue(f.Launch());
                Pilot.Run(f, 400, () => f.Drone.Landed && f.Drone.Flown > 50 && !f.Auto.Active);
                TestContext.WriteLine(string.Join(", ", flight) + ": estimate " + (e.Battery * 100).ToString("0") + "%, used " + ((before - f.Drone.Battery) * 100).ToString("0") + "%, back at " + Strings.Clock(f.Time));
                Assert.IsFalse(f.Drone.Crashed, "hit " + f.Drone.HitBy + " at " + f.Drone.Position);
                Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z));
                Pilot.WaitOnPad(f);
            }
            foreach (var p in f.Mission.Packages) TestContext.WriteLine("  " + p.Item + ": " + p.Fate + (p.Target >= 0 ? " at " + TownPlan.Sites[p.Target].Id + ", " + p.Off.ToString("0.0") + " m off" : ""));
            var r = f.Result();
            TestContext.WriteLine("  " + relief.Count + " of " + relief.Total + " needs met; " + r.Score + " points, " + r.Stars + " stars; " + Strings.Clock(f.Time));
            int evac = Array.IndexOf(TownPlan.Sites, Site("evac")), sitio = Array.IndexOf(TownPlan.Sites, Site("sitio")), health = Array.IndexOf(TownPlan.Sites, Site("health"));
            Assert.AreEqual(2, relief.StateOf(evac), "the evacuation center"); Assert.AreEqual(2, relief.StateOf(health), "the health center");
            Assert.GreaterOrEqual(relief.Count, 5);
            Assert.GreaterOrEqual(r.Stars, 2);
        }
    }

    public class FlightTests
    {
        [Test]
        public void EachWayOfFlyingAllowsWhatItShould()
        {
            var desk = new Flight("lipad", Mode.Desk);
            Assert.IsTrue(desk.CanPlan); Assert.IsFalse(desk.CanFly);
            Assert.IsFalse(desk.TakeControl(), "at the Command Desk the autopilot flies");
            Assert.IsFalse(desk.Manual);
            desk.Step(new DroneInput(0, 0, 1, 0));
            Assert.IsTrue(desk.Drone.Landed, "the sticks do nothing");

            var pilot = new Flight("lipad", Mode.Pilot);
            Assert.IsNull(pilot.AddWaypoint(0, 0), "an FPV pilot has no route"); Assert.IsFalse(pilot.Launch()); Assert.IsFalse(pilot.Return());
            Assert.IsFalse(pilot.HandBack());
            Assert.IsTrue(pilot.Manual);

            var both = new Flight("lipad", Mode.Hybrid);
            Assert.IsFalse(both.Manual);
            Assert.IsNotNull(both.AddWaypoint(0, -150));
            Assert.IsTrue(both.TakeControl()); Assert.IsTrue(both.HandBack());
        }

        [Test]
        public void InHybridThePilotTakesTheSticksAndHandsThemBack()
        {
            var f = new Flight("hatid", Mode.Hybrid);
            var roof = TownPlan.Sites.First(s => s.Id == "roof"); var evac = TownPlan.Sites.First(s => s.Id == "evac");
            f.Mission.Load("food", f.Drone); f.Mission.Load("food", f.Drone);
            f.WaypointAlt = 30;
            f.AddWaypoint(roof.X, roof.Z); f.AddWaypoint(evac.X, evac.Z);
            f.Launch();
            // the autopilot flies there; when it starts down for the drop, the pilot takes over
            Pilot.Run(f, 200, () => f.Auto.Phase == PilotPhase.Descend);
            Assert.AreEqual(PilotPhase.Descend, f.Auto.Phase);
            Assert.IsTrue(f.TakeControl());
            double wx, wz; f.Mission.Weather.SteadyAt(0, 6, out wx, out wz);
            double len = Math.Sqrt(wx * wx + wz * wz), ax = roof.X - wx / len * 2.0, az = roof.Z - wz / len * 2.0;
            Assert.IsTrue(Pilot.GoTo(f, ax, roof.Point.Y + 3.5, az, 1.0, 60, 3));
            Pilot.Hold(f, ax, roof.Point.Y + 3.5, az, 1.5);
            Assert.IsNotNull(f.Release());
            Pilot.Hold(f, ax, roof.Point.Y + 3.5, az, 3);
            Assert.AreEqual(PackageFate.Delivered, f.Mission.Packages[0].Fate, "the pilot's drop, " + f.Mission.Packages[0].Off.ToString("0.0") + " m off");
            Assert.AreEqual(1, f.Auto.Next, "the waypoint of that drop is done: the autopilot will not go back to it");
            Assert.IsTrue(f.HandBack());
            Pilot.Run(f, 300, () => f.Drone.Landed && !f.Auto.Active);
            Assert.AreEqual(2, f.Mission.Packages.Count, "the autopilot made the second drop");
            Assert.AreEqual(PackageFate.Delivered, f.Mission.Packages[1].Fate);
            Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z), "and brought the drone home");
        }

        [Test]
        public void ALostDroneIsReplacedByASpareWithEmptyHooks()
        {
            var f = new Flight("hatid", Mode.Pilot);
            f.Mission.Load("water", f.Drone);
            var hall = TownPlan.Buildings.First(b => b.Id == "hall");
            for (int i = 0; i < 200; i++) f.Step(new DroneInput(0, 0, 0.3, 0));
            for (int i = 0; i < 100; i++) f.Step(new DroneInput(0, 0, 0, 1));   // turn to face west, towards the hall
            for (int i = 0; i < 600 && !f.Drone.Crashed; i++) f.Step(new DroneInput(1, 0, 0, 0));
            Assert.AreEqual(CrashKind.Obstacle, f.Drone.Crash, "at " + f.Drone.Position + " heading " + f.Drone.Heading);
            f.Step(DroneInput.None);
            Assert.AreEqual(1, f.Mission.Crashes);
            Assert.AreEqual(0, f.Mission.Cargo.Items.Count, "what it carried is lost with it");
            Assert.AreEqual("crash.building", f.Mission.Events.First(e => e.Kind == Note.Bad).Key);
            Pilot.Run(f, Mission.SpareSeconds + 0.1);
            Assert.IsFalse(f.Drone.Crashed); Assert.IsTrue(f.Drone.Landed);
            Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z), "a spare stands on the pad");
            Assert.AreEqual(1, f.Drone.Battery, 1e-9);
            Assert.AreEqual(1, f.Mission.Crashes, "one drone lost, counted once");
            f.Mission.End();
            Assert.AreEqual(1 - 0.34, f.Result().Safety, 1e-9);
        }

        [Test]
        public void TheBatteryIsChangedOnThePad()
        {
            var f = new Flight("lipad", Mode.Pilot);
            Assert.IsTrue(Pilot.GoTo(f, -100, TownPlan.PadY + 30, -150));
            Assert.IsTrue(Pilot.LandOnPad(f));
            Assert.Less(f.Drone.Battery, 0.97);
            Pilot.Run(f, DroneSpec.SwapSeconds - 1);
            Assert.Less(f.Drone.Battery, 0.97, "it takes a few seconds");
            Pilot.Run(f, 1.2);
            Assert.AreEqual(1, f.Drone.Battery, 1e-9);
            Assert.IsTrue(f.Mission.Events.Any(e => e.Key == "battery.swapped"));
        }

        [Test]
        public void SuppliesAreLoadedOnlyOnThePad()
        {
            var f = new Flight("hatid", Mode.Pilot);
            Assert.IsTrue(f.Mission.Load("water", f.Drone));
            Assert.AreEqual(4, f.Drone.PayloadKg, 1e-9);
            for (int i = 0; i < 100; i++) f.Step(new DroneInput(0, 0, 1, 0));
            Assert.IsFalse(f.Mission.Load("radio", f.Drone), "not in the air");
            Assert.IsFalse(f.Mission.Unload("water", f.Drone));
            Assert.AreEqual("load.pad", f.Mission.Events.Last().Key);
        }

        [Test]
        public void ALowBatteryIsToldAndAnAlmostEmptyOneBringsTheDroneDown()
        {
            var f = new Flight("lipad", Mode.Pilot);
            for (int i = 0; i < 300; i++) f.Step(new DroneInput(0, 0, 1, 0));
            f.Drone.Battery = DroneSpec.LowBattery + 0.002;
            Pilot.Run(f, 2);
            Assert.IsTrue(f.Mission.Events.Any(e => e.Key == "battery.low"));
            f.Drone.Battery = DroneSpec.CriticalBattery + 0.001;
            double y = f.Drone.Y;
            for (int i = 0; i < 400; i++) f.Step(new DroneInput(0, 0, 1, 0));
            Assert.IsTrue(f.Mission.Events.Any(e => e.Key == "battery.critical"));
            Assert.Less(f.Drone.Y, y - 3, "it comes down though the pilot pushes up");
        }

        [Test]
        public void TheMissionEndsWhenItsTimeIsUp()
        {
            var f = new Flight("lipad", Mode.Pilot);
            int n = (int)(f.Mission.Seconds / DroneSpec.Dt) + 5;
            for (int i = 0; i < n; i++) f.Step(DroneInput.None);
            Assert.IsTrue(f.Mission.Over); Assert.IsTrue(f.Mission.Ended); Assert.IsNull(f.Mission.FinishedAt);
            var r = f.Result();
            Assert.AreEqual(0, r.Pace, 1e-9); Assert.AreEqual(0, r.Tasks, 1e-9);
            Assert.AreEqual(1, r.Safety, 1e-9, "the drone never left the pad");
            Assert.AreEqual(0, r.Stars);
            Assert.IsTrue(r.Text(Lang.En).Any(l => l.Contains("ended before")));
        }

        [Test]
        public void TimePassesInWholeSteps()
        {
            var f = new Flight("lipad", Mode.Pilot);
            Assert.AreEqual(0, f.Advance(0.015, DroneInput.None));
            Assert.AreEqual(1, f.Advance(0.015, DroneInput.None));
            Assert.AreEqual(5, f.Advance(0.1, DroneInput.None));
            Assert.AreEqual(6 * DroneSpec.Dt, f.Time, 1e-9);
            Assert.AreEqual(25, f.Advance(5, DroneInput.None), "after a long hitch it does not try to catch up");
        }
    }

    public class OperationTests
    {
        [Test]
        public void TheBoardKeepsTheBestResultOfEachMission()
        {
            var op = new Operation();
            Assert.AreEqual(-1, op.Stars("sagip")); Assert.IsFalse(op.AllFlown);
            op.Record("sagip", Mode.Desk, new MissionResult { Stars = 1, Score = 40 });
            op.Record("sagip", Mode.Hybrid, new MissionResult { Stars = 3, Score = 90 });
            op.Record("sagip", Mode.Pilot, new MissionResult { Stars = 2, Score = 70 });
            Assert.AreEqual(3, op.Stars("sagip")); Assert.AreEqual(3, op.Flown);
            op.Record("bantay", Mode.Pilot, new MissionResult { Stars = 2, Score = 61 });
            Assert.IsFalse(op.AllFlown);
            op.Record("hatid", Mode.Desk, new MissionResult { Stars = 0, Score = 10 });
            Assert.IsTrue(op.AllFlown, "the three uses of drones have been flown; the flight school is not one of them");
            Assert.AreEqual(5, op.TotalStars);
            foreach (var lang in new[] { Lang.En, Lang.Fil })
            {
                op.Lang = lang;
                var lines = op.Summary();
                Assert.AreEqual(6, lines.Count);
                StringAssert.Contains("LIPAD", lines[0]); StringAssert.Contains("SAGIP", lines[2]); StringAssert.Contains("90", lines[2]); StringAssert.Contains("Hybrid", lines[2]);
            }
        }
    }

    public class StringsTests
    {
        [Test]
        public void EveryTextIsInBothLanguagesWithTheSameBlanks()
        {
            foreach (var key in Strings.Keys)
            {
                var both = Strings.Both(key);
                Assert.AreEqual(2, both.Length, key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(both[0]), key + ": English"); Assert.IsFalse(string.IsNullOrWhiteSpace(both[1]), key + ": Filipino");
                for (int k = 0; k < 5; k++)
                    Assert.AreEqual(both[0].Contains("{" + k + "}"), both[1].Contains("{" + k + "}"), key + ": blank " + k);
            }
        }

        [Test]
        public void EverythingAMissionNamesHasAName()
        {
            foreach (var id in Mission.Ids)
                foreach (var part in new[] { "name", "title", "brief", "task", "tip" }) Assert.IsTrue(Strings.Has("mission." + id + "." + part), id + "." + part);
            foreach (var h in TownPlan.Hazards) { Assert.IsTrue(Strings.Has("hazard." + h.Id), h.Id); Assert.IsTrue(Strings.Has("hazard." + h.Id + ".act"), h.Id + ".act"); }
            foreach (var s in TownPlan.Survivors) Assert.IsTrue(Strings.Has("survivor." + s.Id), s.Id);
            foreach (var s in TownPlan.Sites) Assert.IsTrue(Strings.Has("site." + s.Id), s.Id);
            foreach (var s in TownPlan.Supplies) Assert.IsTrue(Strings.Has("supply." + s.Id), s.Id);
            foreach (HitKind k in Enum.GetValues(typeof(HitKind))) if (k != HitKind.None) Assert.IsTrue(Strings.Has("crash." + k.ToString().ToLowerInvariant()), k.ToString());
            foreach (Mode m in Enum.GetValues(typeof(Mode))) { Assert.IsTrue(Strings.Has("mode." + m.ToString().ToLowerInvariant())); Assert.IsTrue(Strings.Has("mode." + m.ToString().ToLowerInvariant() + ".about")); }
            foreach (var p in new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }) Assert.IsTrue(Strings.Has("dir." + p));
            foreach (var id in Mission.Ids) Assert.IsTrue(Strings.Has("console.tasks." + id));
        }

        [Test]
        public void WhatTheRadioSaysCanBeSaid()
        {
            // fly a little of every mission and say everything that was told, in both languages
            foreach (var id in Mission.Ids)
            {
                var f = new Flight(id, Mode.Pilot);
                foreach (var s in f.Mission.Offers) f.Mission.Load(s, f.Drone);
                Pilot.GoTo(f, -100, TownPlan.PadY + 20, -140);
                f.Primary(new Aim(f.Drone.Position, new P3(0, -1, 0)));
                f.Release();
                Pilot.Run(f, 3);
                f.Mission.End();
                foreach (var e in f.Mission.Events.Concat(f.Result().Lines))
                {
                    Assert.IsTrue(Strings.Has(e.Key), id + ": no text for " + e.Key);
                    foreach (var lang in new[] { Lang.En, Lang.Fil }) StringAssert.DoesNotContain("{", e.Text(lang), e.Key);
                }
            }
            Assert.AreEqual("3:07", Strings.Clock(187)); Assert.AreEqual("0:00", Strings.Clock(-4));
            Assert.AreEqual("The fallen power line", Strings.Capital(Strings.Get("hazard.wire", Lang.En)));
            Assert.AreEqual("hilagang-kanluran", Strings.Direction(315, Lang.Fil));
        }
    }
}
