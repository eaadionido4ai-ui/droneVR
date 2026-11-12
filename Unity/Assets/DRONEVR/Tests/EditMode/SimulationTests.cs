// Checks of the ground, the town, the weather, the drone's flight, the autopilot and the falling packages.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DroneVR.Tests
{
    public class TerrainTests
    {
        [Test]
        public void TheGroundIsThePlansGround()
        {
            foreach (var s in TownPlan.GroundSamples)
                Assert.AreEqual(s[2], Terrain.Ground(s[0], s[1]), 1e-9, "at " + s[0] + ", " + s[1]);
        }

        [Test]
        public void TheRiverAndTheCreekAreLowAndTheHallAndTheSchoolStandHigh()
        {
            Assert.Less(Terrain.Ground(0, 120), -2, "the middle of the river");
            Assert.Less(Terrain.Ground(Terrain.CreekX, 0), 0.1, "the creek's bed");
            double highest = TownPlan.Missions.Max(m => m.FloodLevels.Max());
            Assert.Greater(Terrain.Ground(TownPlan.PadX, TownPlan.PadZ), highest + 1, "the pad stays dry in every mission");
            Assert.Greater(Terrain.Ground(150, -122), highest + 1, "the evacuation center stays dry");
            foreach (var s in TownPlan.Staging)
                Assert.Greater(Terrain.Depth(s[0], s[1], TownPlan.Mission("sagip").FloodLevels[0]), 0.35, "a boat floats at its landing when the search begins");
        }

        [Test]
        public void TheFloodCoversTheLowStreetsFirst()
        {
            Assert.AreEqual(0, Terrain.Depth(0, 80, 0.5), 1e-9, "the river road is dry when the river is a little high");
            Assert.Greater(Terrain.Depth(0, 80, 2.2), 1.2, "and deep under water in the flood");
            Assert.AreEqual(0, Terrain.Depth(60, -100, 2.2), 1e-9, "the north of the barangay is still dry");
            Assert.Greater(Terrain.Depth(60, -100, 2.8), 0, "until the flood is at its highest");
            Assert.Greater(Terrain.Depth(0, -100, 2.2), 0.2, "but the creek's valley floods far to the north");
        }

        [Test]
        public void HeadingsTurnTheShortWay()
        {
            Assert.AreEqual(20, Compass.Turn(350, 10), 1e-9);
            Assert.AreEqual(-20, Compass.Turn(10, 350), 1e-9);
            Assert.AreEqual(90, Compass.Towards(0, 0, 10, 0), 1e-9, "east");
            Assert.AreEqual(0, Compass.Towards(0, 0, 0, -10), 1e-9, "north is -z");
            Assert.AreEqual(180, Compass.Towards(0, 0, 0, 10), 1e-9);
            Assert.AreEqual("NW", Compass.Point(315));
            Assert.AreEqual(1, Compass.ForwardX(90), 1e-9);
            Assert.AreEqual(-1, Compass.ForwardZ(0), 1e-9);
        }
    }

    public class TownTests
    {
        [Test]
        public void NoTwoBuildingsStandOnTheSameGround()
        {
            foreach (var sets in TownPlan.Missions.Select(m => m.Sets))
            {
                var bs = new Town(sets).Buildings;
                for (int i = 0; i < bs.Count; i++)
                    for (int j = i + 1; j < bs.Count; j++)
                    {
                        bool apart = Math.Abs(bs[i].X - bs[j].X) >= (bs[i].W + bs[j].W) / 2 || Math.Abs(bs[i].Z - bs[j].Z) >= (bs[i].D + bs[j].D) / 2;
                        Assert.IsTrue(apart, bs[i].Id + " and " + bs[j].Id);
                    }
            }
        }

        [Test]
        public void TheHousesThatFallAreRubbleAfterTheEarthquake()
        {
            var calm = Town.For(TownPlan.Mission("lipad")); var quake = Town.For(TownPlan.Mission("bantay"));
            var fallen = TownPlan.Buildings.Where(b => b.Set == "normal").ToList();
            Assert.AreEqual(3, fallen.Count);
            foreach (var b in fallen)
            {
                Assert.AreEqual(HitKind.Building, calm.Hit(b.X, b.Base + 2.5, b.Z, 0.5), b.Id + " stands in calm weather");
                Assert.AreEqual(HitKind.None, quake.Hit(b.X, b.Base + 2.5, b.Z, 0.5), b.Id + " has fallen");
                Assert.AreEqual(HitKind.Building, quake.Hit(b.X, b.Base + 0.8, b.Z, 0.5), "its rubble lies there");
            }
        }

        [Test]
        public void TheMiddleOfTheBridgeIsGoneAfterTheTyphoon()
        {
            var search = Town.For(TownPlan.Mission("sagip")); var relief = Town.For(TownPlan.Mission("hatid"));
            Assert.AreEqual(3.4, search.SurfaceFromAbove(20, 120), 1e-9, "the deck");
            Assert.Less(relief.SurfaceFromAbove(20, 120), 0, "only the river");
            Assert.AreEqual(3.4, relief.SurfaceFromAbove(20, 105), 1e-9, "the north end still stands");
            Assert.AreEqual(HitKind.Bridge, search.Hit(20, 3.0, 120, 0.6), "flying into the deck from below");
        }

        [Test]
        public void ADroneCanLandOnARoofAndFlyUnderACoveredCourt()
        {
            var town = Town.For(TownPlan.Mission("lipad"));
            var court = TownPlan.Buildings.First(b => b.Id == "court");
            Assert.AreEqual(court.Top, town.SurfaceFromAbove(court.X, court.Z), 1e-9, "its roof, from above");
            Assert.AreEqual(Terrain.Ground(court.X, court.Z), town.SurfaceBelow(court.X, court.Base + 3, court.Z), 1e-9, "its floor, from under the roof");
            Assert.AreEqual(HitKind.None, town.Hit(court.X, court.Base + 3, court.Z, 0.6), "there is room under the roof");
            Assert.AreEqual(HitKind.Roof, town.Hit(court.X, court.Base + court.H + 0.5, court.Z, 0.6));
            var hall = TownPlan.Buildings.First(b => b.Id == "hall");
            Assert.AreEqual(HitKind.Building, town.Hit(hall.X, hall.Base + 3, hall.Z, 0.6));
            Assert.AreEqual(hall.Top, town.SurfaceFromAbove(hall.X, hall.Z), 1e-9);
            Assert.AreEqual(TownPlan.PadY, town.SurfaceFromAbove(TownPlan.PadX, TownPlan.PadZ), 1e-9, "the pad");
        }

        [Test]
        public void TowersTreesAndPolesAreInTheWay()
        {
            var town = Town.For(TownPlan.Mission("lipad"));
            var cell = TownPlan.Towers.First(t => t.Id == "celltower");
            Assert.AreEqual(HitKind.Tower, town.Hit(cell.X + 1, Terrain.Ground(cell.X, cell.Z) + 30, cell.Z, 0.6));
            Assert.AreEqual(HitKind.None, town.Hit(cell.X + 1, Terrain.Ground(cell.X, cell.Z) + 45, cell.Z, 0.6), "over its top");
            var palm = TownPlan.Trees.First(t => t.Palm);
            double g = Terrain.Ground(palm.X, palm.Z);
            Assert.AreEqual(HitKind.Tree, town.Hit(palm.X, g + palm.H - 1, palm.Z, 0.6), "its crown");
            Assert.AreEqual(HitKind.None, town.Hit(palm.X + 1.5, g + 2, palm.Z, 0.6), "beside its trunk, under the crown");
            var pole = TownPlan.Poles[0];
            Assert.AreEqual(HitKind.Pole, town.Hit(pole.X, Terrain.Ground(pole.X, pole.Z) + 5, pole.Z, 0.6));
        }

        [Test]
        public void EverythingAMissionLooksForIsWhereADroneCanReachIt()
        {
            var school = Town.For(TownPlan.Mission("lipad"));
            foreach (var g in TownPlan.Gates)
                Assert.AreEqual(HitKind.None, school.Hit(g.X, g.Center.Y, g.Z, g.R), "a ring with something in it at " + g.X + ", " + g.Z);
            var quake = Town.For(TownPlan.Mission("bantay"));
            foreach (var h in TownPlan.Hazards)
            {
                // a place to photograph it from: 18 m away on some side, 12 m higher
                bool some = false;
                for (int a = 0; a < 360 && !some; a += 30)
                {
                    double x = h.X + 18 * Math.Cos(a * Compass.Deg), z = h.Z + 18 * Math.Sin(a * Compass.Deg);
                    some = quake.Hit(x, h.Point.Y + 12, z, 1.5) == HitKind.None && h.Point.Y + 12 > quake.SurfaceFromAbove(x, z) + 3;
                }
                Assert.IsTrue(some, "no place to photograph " + h.Id + " from");
                Assert.Less(Math.Sqrt((h.ReportX - h.X) * (h.ReportX - h.X) + (h.ReportZ - h.Z) * (h.ReportZ - h.Z)), 25, "the report of " + h.Id + " is near it");
            }
            var flood = Town.For(TownPlan.Mission("sagip"));
            double top = TownPlan.Mission("sagip").FloodLevels.Max();
            foreach (var s in TownPlan.Survivors)
            {
                Assert.Greater(s.Point.Y, top - 0.9, s.Id + " would be under water");
                Assert.AreEqual(HitKind.None, flood.Hit(s.X, s.Point.Y + 12, s.Z, 0.6), "something hangs over " + s.Id + " where the drone would look from");
            }
            var wind = Town.For(TownPlan.Mission("hatid"));
            foreach (var s in TownPlan.Sites)
            {
                Assert.AreEqual(s.Point.Y, wind.SurfaceFromAbove(s.X, s.Z), 0.3, "the place to drop into at " + s.Id);
                Assert.Greater(s.Point.Y, TownPlan.Mission("hatid").FloodLevels.Max() + 0.04, s.Id + " is dry");
                foreach (var need in s.Needs) Assert.IsNotNull(TownPlan.Supply(need));
            }
        }
    }

    public class WeatherTests
    {
        [Test]
        public void TheWindGustsAroundItsMean()
        {
            double lo = double.MaxValue, hi = 0, sum = 0; int n = 0;
            for (double t = 0; t < 473; t += 0.1) { double s = Wind.Speed(9, 0.5, t); lo = Math.Min(lo, s); hi = Math.Max(hi, s); sum += s; n++; }
            Assert.AreEqual(9, sum / n, 0.15, "the mean");
            Assert.Greater(hi, 12.5); Assert.Less(hi, 13.6);
            Assert.Less(lo, 5.5); Assert.Greater(lo, 4.4);
            Assert.AreEqual(9 * (1 + 0.5 * 0.4 * Math.Sin(1.3)), Wind.Speed(9, 0.5, 0), 1e-9);
        }

        [Test]
        public void TheWindIsWeakerNearTheGround()
        {
            Assert.AreEqual(0.6, Wind.AtHeight(0), 1e-9);
            Assert.AreEqual(0.8, Wind.AtHeight(30), 1e-9);
            Assert.AreEqual(1, Wind.AtHeight(60), 1e-9);
            Assert.AreEqual(1, Wind.AtHeight(110), 1e-9);
        }

        [Test]
        public void AWindFromTheWestBlowsEast()
        {
            var w = new Weather(new MissionPlan("x", new string[0], 10, 0, 270, new double[] { 0 }, new double[] { 0 }, 5));
            double wx, wz;
            w.SteadyAt(0, 60, out wx, out wz);
            Assert.AreEqual(10, wx, 1e-9); Assert.AreEqual(0, wz, 1e-9);
            w.SteadyAt(0, 0, out wx, out wz);
            Assert.AreEqual(6, wx, 1e-9, "weaker at the ground");
        }

        [Test]
        public void TheFloodFollowsTheMissionsHours()
        {
            var search = TownPlan.Mission("sagip");
            Assert.AreEqual(2.2, Weather.LevelOf(search, 0), 1e-9);
            Assert.AreEqual(2.5, Weather.LevelOf(search, 180), 1e-9, "half way up");
            Assert.AreEqual(2.8, Weather.LevelOf(search, 360), 1e-9);
            Assert.AreEqual(2.8, Weather.LevelOf(search, 3600), 1e-9, "it stays there");
            var relief = TownPlan.Mission("hatid");
            Assert.Less(Weather.LevelOf(relief, 400), Weather.LevelOf(relief, 100), "the water goes down after the typhoon");
            Assert.AreEqual(0, Weather.LevelOf(TownPlan.Mission("bantay"), 100), 1e-9);
        }

        [Test]
        public void GamasWeatherIsUsedWhileItComes()
        {
            var w = new Weather(TownPlan.Mission("hatid"));
            double own = w.SpeedAt(5);
            w.SetFromGama(14, 12, 180, 3.3);
            Assert.AreEqual(14, w.SpeedAt(5), 1e-9); Assert.AreEqual(12, w.MeanSpeed, 1e-9);
            Assert.AreEqual(180, w.FromAt(5), 1e-9); Assert.AreEqual(3.3, w.Level(5), 1e-9);
            w.ForgetGama();
            Assert.AreEqual(own, w.SpeedAt(5), 1e-9);
            w.WindScale = 1.5;
            Assert.AreEqual(own * 1.5, w.SpeedAt(5), 1e-9, "the teacher's hand on the wind");
        }

        [Test]
        public void EveryMissionsWindCanBeFlownAgainst()
        {
            foreach (var m in TownPlan.Missions)
                Assert.Less(m.Wind * (1 + m.Gust), DroneSpec.MaxAirSpeed - 3, m.Id);
        }
    }

    public class DroneTests
    {
        static readonly Town Open = Town.Bare();

        static DroneState InTheAir(double x, double y, double z)
        {
            var d = new DroneState(); d.ResetOnPad();
            d.X = x; d.Y = y; d.Z = z; d.Landed = false;
            return d;
        }

        static void Fly(DroneState d, double seconds, DroneInput sticks, Town town = null, double wx = 0, double wz = 0, double sx = 0, double sz = 0, double level = 0)
        {
            int n = (int)Math.Round(seconds / DroneSpec.Dt);
            for (int i = 0; i < n; i++) DroneModel.Step(d, sticks, town ?? Open, wx, wz, sx, sz, level);
        }

        [Test]
        public void TheDroneFliesAsTheReferenceModelStepForStep()
        {
            for (int r = 0; r < FlightGolden.Names.Length; r++)
            {
                var w = FlightGolden.Weather[r];
                var plan = new MissionPlan("x", new string[0], w[0], w[1], w[2], new double[] { 0 }, new double[] { w[4] }, 10);
                var weather = new Weather(plan);
                var d = new DroneState(); d.ResetOnPad(); d.PayloadKg = w[3];
                int step = 0, row = 0;
                foreach (var s in FlightGolden.Sticks[r])
                {
                    for (int k = 0; k < (int)s[0]; k++)
                    {
                        double t = step * DroneSpec.Dt, wx, wz, sx, sz;
                        weather.WindAt(t, d.AboveGround, out wx, out wz);
                        weather.SteadyAt(t, d.AboveGround, out sx, out sz);
                        DroneModel.Step(d, new DroneInput(s[1], s[2], s[3], s[4]), Open, wx, wz, sx, sz, w[4]);
                        step++;
                        if (row < FlightGolden.Rows[r].Length && step == (int)FlightGolden.Rows[r][row][0])
                        {
                            var g = FlightGolden.Rows[r][row++];
                            string at = FlightGolden.Names[r] + ", step " + step;
                            Assert.AreEqual(g[1], d.X, 1e-7, at + ": x"); Assert.AreEqual(g[2], d.Y, 1e-7, at + ": y"); Assert.AreEqual(g[3], d.Z, 1e-7, at + ": z");
                            Assert.AreEqual(g[4], d.Vx, 1e-7, at + ": vx"); Assert.AreEqual(g[5], d.Vy, 1e-7, at + ": vy"); Assert.AreEqual(g[6], d.Vz, 1e-7, at + ": vz");
                            Assert.AreEqual(g[7], d.Heading, 1e-7, at + ": heading"); Assert.AreEqual(g[8], d.Battery, 1e-9, at + ": battery");
                            Assert.AreEqual(g[9] == 1, d.Landed, at + ": landed"); Assert.AreEqual((int)g[10], (int)d.Crash, at + ": crash");
                        }
                    }
                }
                Assert.AreEqual(FlightGolden.Rows[r].Length, row, FlightGolden.Names[r] + ": every row was checked");
            }
        }

        [Test]
        public void ItTakesOffClimbsAndHoversWhereItIsLeft()
        {
            var d = new DroneState(); d.ResetOnPad();
            Fly(d, 2, DroneInput.None);
            Assert.IsTrue(d.Landed, "it stays on the pad until told to go up");
            Assert.AreEqual(1, d.Battery, 1e-9, "and uses no battery there");
            Fly(d, 4, new DroneInput(0, 0, 1, 0));
            Assert.IsFalse(d.Landed);
            Assert.AreEqual(TownPlan.PadY + DroneSpec.Gear + 5 * 4, d.Y, 2.5, "about 5 m a second");
            double x = d.X, z = d.Z;
            Fly(d, 10, DroneInput.None);
            Assert.AreEqual(x, d.X, 1e-6); Assert.AreEqual(z, d.Z, 1e-6);
            Assert.AreEqual(0, d.Vy, 0.01, "it has stopped climbing");
        }

        [Test]
        public void TheSticksMoveItTheWayItFaces()
        {
            var d = InTheAir(0, 60, 0); d.Heading = 0;
            Fly(d, 5, new DroneInput(1, 0, 0, 0));
            Assert.Less(d.Z, -40, "forward is north when it faces north"); Assert.AreEqual(0, d.X, 1e-6);
            Assert.AreEqual(DroneSpec.MaxSpeed, d.Speed, 0.05, "top speed");
            d = InTheAir(0, 60, 0); d.Heading = 0;
            Fly(d, 5, new DroneInput(0, 1, 0, 0));
            Assert.Greater(d.X, 40, "right is east");
            d = InTheAir(0, 60, 0); d.Heading = 0;
            Fly(d, 0.9, new DroneInput(0, 0, 0, 1));
            Assert.AreEqual(90, d.Heading, 1e-6, "a quarter turn to the right in 0.9 s");
            Fly(d, 5, new DroneInput(1, 1, 0, 0));
            Assert.AreEqual(DroneSpec.MaxSpeed, d.Speed, 0.05, "no faster on a diagonal");
        }

        [Test]
        public void ALoadMakesItSlowerAndEmptiesTheBatterySooner()
        {
            var light = InTheAir(-200, 60, 0); var heavy = InTheAir(-200, 60, 0); heavy.PayloadKg = 8;
            Fly(light, 20, new DroneInput(1, 0, 0.0, 0)); Fly(heavy, 20, new DroneInput(1, 0, 0.0, 0));
            Assert.AreEqual(DroneSpec.MaxSpeed * 0.75, heavy.Speed, 0.05);
            Assert.Less(heavy.X, light.X - 40);
            Assert.Less(heavy.Battery, light.Battery - 0.015);
            light = InTheAir(0, 20, 0); heavy = InTheAir(0, 20, 0); heavy.PayloadKg = 8;
            Fly(light, 4, new DroneInput(0, 0, 1, 0)); Fly(heavy, 4, new DroneInput(0, 0, 1, 0));
            Assert.Less(heavy.Y, light.Y - 4, "it climbs slower");
        }

        [Test]
        public void AFullBatteryHoversSevenMinutesAndThenTheDroneComesDown()
        {
            var d = InTheAir(-100, 80, -150);
            Fly(d, 418, DroneInput.None);
            Assert.Greater(d.Battery, 0, "still flying");
            Assert.AreEqual(80, d.Y, 1e-6);
            Fly(d, 4, DroneInput.None);
            Assert.AreEqual(0, d.Battery, 1e-12);
            Fly(d, 60, new DroneInput(1, 0, 1, 0));
            Assert.IsTrue(d.Landed, "it comes down by itself, whatever the sticks say");
            Assert.IsFalse(d.Crashed, "gently enough");
            Fly(d, 3, new DroneInput(0, 0, 1, 0));
            Assert.IsTrue(d.Landed, "and stays down");
        }

        [Test]
        public void ItLeansIntoASteadyWindButGustsPushIt()
        {
            var d = InTheAir(0, 60, 0);
            Fly(d, 20, DroneInput.None, null, 8, 0, 8, 0);
            Assert.AreEqual(0, d.X, 1e-6, "a steady wind does not move it");
            Assert.AreEqual(8, d.AirSpeed, 1e-6, "though it flies through the air to stay there");
            double calm = InTheAirBatteryAfterHover(0), windy = InTheAirBatteryAfterHover(8);
            Assert.Less(windy, calm, "which costs battery");
            d = InTheAir(0, 60, 0);
            Fly(d, 5, DroneInput.None, null, 12, 0, 8, 0);
            Assert.AreEqual((1 - DroneSpec.GustHold) * 4 * 5, d.X, 1.5, "a gust of 4 m/s more pushes it at about a third of that");
        }

        static double InTheAirBatteryAfterHover(double wind)
        {
            var d = InTheAir(0, 60, 0);
            Fly(d, 30, DroneInput.None, null, wind, 0, wind, 0);
            return d.Battery;
        }

        [Test]
        public void AWindStrongerThanItsMotorsBlowsItBack()
        {
            var d = InTheAir(0, 60, 0); d.Heading = 270;
            Fly(d, 10, new DroneInput(1, 0, 0, 0), null, 22, 0, 22, 0);
            Assert.Greater(d.X, 20, "flying west as hard as it can, it is carried east");
            Assert.AreEqual(DroneSpec.MaxAirSpeed, d.AirSpeed, 0.05);
        }

        [Test]
        public void ItLandsGentlyBreaksIfItComesDownHardAndIsLostInWater()
        {
            var d = InTheAir(-100, Terrain.Ground(-100, -150) + 6, -150);
            Fly(d, 6, new DroneInput(0, 0, -0.5, 0));
            Assert.IsTrue(d.Landed); Assert.IsFalse(d.Crashed);
            Assert.AreEqual(Terrain.Ground(-100, -150) + DroneSpec.Gear, d.Y, 1e-9);

            d = InTheAir(-100, Terrain.Ground(-100, -150) + 20, -150);
            Fly(d, 10, new DroneInput(0, 0, -1, 0));
            Assert.AreEqual(CrashKind.HardLanding, d.Crash, "3.5 m/s down is too fast");

            d = InTheAir(-100, Terrain.Ground(-100, -150) + 3, -150);
            Fly(d, 3, new DroneInput(1, 0, -0.5, 0));
            Assert.AreEqual(CrashKind.HardLanding, d.Crash, "so is landing while flying fast forward");

            d = InTheAir(0, 6, 60);
            Fly(d, 6, new DroneInput(0, 0, -0.5, 0), null, 0, 0, 0, 0, 2.2);
            Assert.AreEqual(CrashKind.Water, d.Crash);
            Assert.AreEqual(2.2, d.Y, 1e-9, "it floats where it fell");
        }

        [Test]
        public void ItBreaksAgainstAWallAtSpeedAndOnlyBumpsItSlowly()
        {
            var town = Town.For(TownPlan.Mission("lipad"));
            var hall = TownPlan.Buildings.First(b => b.Id == "hall");
            var d = InTheAir(hall.X + 30, hall.Base + 3, hall.Z); d.Heading = 270;
            Fly(d, 6, new DroneInput(1, 0, 0, 0), town);
            Assert.AreEqual(CrashKind.Obstacle, d.Crash); Assert.AreEqual(HitKind.Building, d.HitBy);

            d = InTheAir(hall.X + 12, hall.Base + 3, hall.Z); d.Heading = 270;
            Fly(d, 6, new DroneInput(0.15, 0, 0, 0), town);
            Assert.IsFalse(d.Crashed, "at 2 m/s it only bumps");
            Assert.AreEqual(hall.X + hall.W / 2 + DroneSpec.Radius, d.X, 0.2, "and stays at the wall");

            d = InTheAir(hall.X + 30, hall.Top + 2, hall.Z); d.Heading = 270;
            Fly(d, 2.5, new DroneInput(1, 0, 0, 0), town);
            Fly(d, 3, DroneInput.None, town);
            Assert.IsFalse(d.Crashed, "over the roof there is nothing to hit");
            Fly(d, 4, new DroneInput(0, 0, -0.5, 0), town);
            Assert.IsTrue(d.Landed, "and it can land on it");
            Assert.AreEqual(hall.Top + DroneSpec.Gear, d.Y, 1e-9);
        }

        [Test]
        public void ItStaysUnderTheCeilingAndInsideTheBarangay()
        {
            var d = InTheAir(0, 100, 0);
            Fly(d, 30, new DroneInput(0, 0, 1, 0));
            Assert.AreEqual(TownPlan.PadY + DroneSpec.Ceiling, d.Y, 1e-9, "120 m above the pad, and no higher");
            d.Heading = 90;
            Fly(d, 30, new DroneInput(1, 0, 0, 0));
            Assert.AreEqual(Terrain.X1 - 2, d.X, 1e-9, "it stops at the edge");
            Assert.IsFalse(d.Crashed);
        }
    }

    public class AutopilotTests
    {
        static Flight NewFlight(string mission, Mode mode = Mode.Desk) { return new Flight(mission, mode); }

        public static void Run(Flight f, double seconds, Func<bool> until = null)
        {
            int n = (int)Math.Round(seconds / DroneSpec.Dt);
            for (int i = 0; i < n && !f.Mission.Over; i++) { f.Step(DroneInput.None); if (until != null && until()) return; }
        }

        [Test]
        public void ItFliesARouteAndComesHomeToThePad()
        {
            var f = NewFlight("lipad");
            f.WaypointAlt = 30;
            Assert.IsNotNull(f.AddWaypoint(-60, -150)); Assert.IsNotNull(f.AddWaypoint(-20, -90)); Assert.IsNotNull(f.AddWaypoint(-110, -60));
            Assert.IsTrue(f.Launch());
            double near1 = 1e9, near2 = 1e9, near3 = 1e9, low = 1e9;
            Run(f, 200, () =>
            {
                var d = f.Drone;
                near1 = Math.Min(near1, Math.Sqrt((d.X + 60) * (d.X + 60) + (d.Z + 150) * (d.Z + 150)));
                near2 = Math.Min(near2, Math.Sqrt((d.X + 20) * (d.X + 20) + (d.Z + 90) * (d.Z + 90)));
                near3 = Math.Min(near3, Math.Sqrt((d.X + 110) * (d.X + 110) + (d.Z + 60) * (d.Z + 60)));
                if (d.Flown > 30 && !TownPlan.OnPad(d.X, d.Z) && Math.Sqrt((d.X - TownPlan.PadX) * (d.X - TownPlan.PadX) + (d.Z - TownPlan.PadZ) * (d.Z - TownPlan.PadZ)) > 20) low = Math.Min(low, d.AboveGround);
                return d.Landed && d.Flown > 100;
            });
            Run(f, 1);
            Assert.Less(near1, Autopilot.Reach + 0.5); Assert.Less(near2, Autopilot.Reach + 0.5); Assert.Less(near3, Autopilot.Reach + 0.5);
            Assert.Greater(low, 20, "it keeps its height between waypoints");
            Assert.IsTrue(f.Drone.Landed); Assert.IsFalse(f.Drone.Crashed);
            Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z), "it lands on the pad, at " + f.Drone.Position);
            Assert.AreEqual(0, f.Auto.Route.Count, "the route is flown: the table is free for the next one");
            Assert.IsFalse(f.Auto.Active);
        }

        [Test]
        public void TheEstimateOnTheTableIsCloseToTheFlight()
        {
            foreach (var id in new[] { "lipad", "hatid" })
            {
                var f = NewFlight(id);
                if (id == "hatid") { Assert.IsTrue(f.Mission.Load("water", f.Drone)); Assert.IsTrue(f.Mission.Load("food", f.Drone)); }
                f.WaypointAlt = 30;
                f.AddWaypoint(-20, -150); f.AddWaypoint(80, -60); f.AddWaypoint(-60, 20);
                var e = f.Estimate();
                f.Launch();
                double t0 = f.Time;
                Run(f, 400, () => f.Drone.Landed && f.Drone.Flown > 100);
                double used = 1 - f.Drone.Battery, seconds = f.Time - t0;
                TestContext.WriteLine(id + ": estimate " + e.Metres.ToString("0") + " m, " + e.Seconds.ToString("0") + " s, " + (e.Battery * 100).ToString("0.0") + "%; flown " + f.Drone.Flown.ToString("0") + " m, " + seconds.ToString("0") + " s, " + (used * 100).ToString("0.0") + "%");
                Assert.IsTrue(f.Drone.Landed && TownPlan.OnPad(f.Drone.X, f.Drone.Z), id + ": home");
                Assert.AreEqual(used, e.Battery, used * 0.2, id + ": the battery, within a fifth");
                Assert.AreEqual(seconds, e.Seconds, seconds * 0.2, id + ": the time, within a fifth");
                Assert.AreEqual(f.Drone.Flown, e.Metres, f.Drone.Flown * 0.1, id + ": the distance");
            }
        }

        [Test]
        public void ItTurnsForHomeWhenTheBatteryWillNotLast()
        {
            var f = NewFlight("hatid");
            f.Mission.Load("water", f.Drone); f.Mission.Load("water", f.Drone);
            f.WaypointAlt = 45;
            // back and forth across the barangay, far more than one battery
            for (int k = 0; k < 6; k++) { f.AddWaypoint(200, -150 + 50 * k); f.AddWaypoint(-200, -125 + 50 * k); }
            Assert.Greater(f.Estimate().Battery, 1, "the table says so");
            f.Launch();
            bool turned = false;
            Run(f, 600, () => { turned |= f.Auto.LowBatteryReturn; return f.Drone.Landed && f.Drone.Flown > 100; });
            Assert.IsTrue(turned, "it gave up the route");
            Assert.IsFalse(f.Drone.Crashed);
            Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z), "and came home, at " + f.Drone.Position);
            Assert.Greater(f.Drone.Battery, 0.02, "with a little left");
            Assert.Less(f.Drone.Battery, 0.2, "but not much: it did not turn too early");
        }

        [Test]
        public void ItHoversWhenTheRouteIsClearedAndGoesOnWhenThereIsSomewhereToGo()
        {
            var f = NewFlight("lipad");
            f.AddWaypoint(100, -150); f.AddWaypoint(100, 0);
            f.Launch();
            Run(f, 12);
            f.ClearRoute();
            Assert.IsTrue(f.Auto.Waiting);
            double x = f.Drone.X, z = f.Drone.Z;
            Run(f, 10);
            Assert.IsFalse(f.Drone.Landed);
            Assert.Less(Math.Abs(f.Drone.X - x) + Math.Abs(f.Drone.Z - z), 25, "it stopped and waits");
            Assert.Less(f.Drone.Speed, 1);
            f.AddWaypoint(-100, -100);
            double near = 1e9;
            Run(f, 90, () => { near = Math.Min(near, Math.Sqrt((f.Drone.X + 100) * (f.Drone.X + 100) + (f.Drone.Z + 100) * (f.Drone.Z + 100))); return f.Drone.Landed; });
            Assert.Less(near, Autopilot.Reach + 0.5, "then the new waypoint");
            Assert.IsTrue(f.Drone.Landed && TownPlan.OnPad(f.Drone.X, f.Drone.Z), "then home");
        }

        [Test]
        public void ARouteCanBeUndoneOnlyAheadOfTheDrone()
        {
            var f = NewFlight("lipad");
            f.AddWaypoint(-100, -150); f.AddWaypoint(-50, -150); f.AddWaypoint(0, -150);
            Assert.IsTrue(f.UndoWaypoint());
            Assert.AreEqual(2, f.Auto.Route.Count);
            f.Launch();
            Run(f, 60, () => f.Auto.Next == 2);
            Assert.IsFalse(f.UndoWaypoint(), "both are behind it now");
            Assert.AreEqual(Flight.MaxWaypoints, Enumerable.Range(0, 40).Count(k => new Func<bool>(() => Fresh.AddWaypoint(k * 3 - 60, 0) != null)()), "a route has a longest length");
        }
        static readonly Flight Fresh = new Flight("lipad", Mode.Desk);

        [Test]
        public void ItComesDownAndDropsOnTheMarkInStillAir()
        {
            var plan = TownPlan.Mission("hatid");
            var f = NewFlight("hatid");
            f.Mission.Weather.WindScale = 0;
            f.Mission.Load("food", f.Drone); f.Mission.Load("water", f.Drone);
            var site = TownPlan.Sites.First(s => s.Id == "evac");
            var w = f.AddWaypoint(site.X + 4, site.Z - 5);
            Assert.AreEqual(WaypointKind.Drop, w.Kind, "over a site, a waypoint is a drop");
            Assert.AreEqual(site.X, w.X, 1e-9, "on the mark");
            f.AddWaypoint(site.X, site.Z);
            f.Launch();
            double lowest = 1e9;
            Run(f, 300, () =>
            {
                bool over = Math.Abs(f.Drone.X - site.X) < 8 && Math.Abs(f.Drone.Z - site.Z) < 8;
                if (over) lowest = Math.Min(lowest, f.Drone.Y - site.Point.Y);
                return f.Drone.Landed && f.Drone.Flown > 100;
            });
            var relief = (ReliefFlights)f.Mission;
            Assert.AreEqual(2, relief.Count, "both packages delivered");
            Assert.AreEqual(Autopilot.DropHeight, lowest, 0.6, "from three and a half metres up");
            foreach (var p in f.Mission.Packages) { Assert.AreEqual(PackageFate.Delivered, p.Fate); Assert.Less(p.Off, 1.5, "within a metre and a half of the mark in still air"); }
            Assert.IsTrue(TownPlan.OnPad(f.Drone.X, f.Drone.Z));
        }
    }

    public class CargoTests
    {
        [Test]
        public void TheDroneLiftsEightKilogramsOnThreeHooks()
        {
            var c = new Cargo();
            Assert.IsTrue(c.Add("water")); Assert.IsTrue(c.Add("food"));
            Assert.AreEqual(7, c.Kg, 1e-9);
            Assert.AreEqual("load.heavy", c.WhyNot("firstaid"), "2 kg more is too much");
            Assert.IsTrue(c.Add("radio"));
            Assert.AreEqual("load.hooks", c.WhyNot("medicine"), "and there are only three hooks");
            Assert.IsTrue(c.Remove("water")); Assert.IsFalse(c.Remove("water"));
            Assert.AreEqual(4, c.Kg, 1e-9);
        }

        static Package Drop(double height, double wind, Town town, double level = 0, string item = "food", double x = -100, double z = -150)
        {
            var p = new Package(item) { X = x, Y = Terrain.Ground(x, z) + height, Z = z };
            for (int i = 0; i < 2000 && p.Falling; i++) PackageFall.Step(p, town, wind, 0, level);
            return p;
        }

        [Test]
        public void APackageBreaksIfItFallsFromTooHigh()
        {
            var open = Town.Bare();
            Assert.AreEqual(PackageFate.Missed, Drop(3.5, 0, open).Fate, "from the autopilot's height it lands whole (and then the mission says where)");
            Assert.AreEqual(PackageFate.Missed, Drop(4.2, 0, open).Fate);
            Assert.AreEqual(PackageFate.Broken, Drop(5.0, 0, open).Fate);
            Assert.AreEqual(PackageFate.Broken, Drop(30, 0, open).Fate);
            Assert.AreEqual(PackageFate.Missed, Drop(30, 0, open, 0, "ring").Fate, "a life ring does not break");
            Assert.AreEqual(Math.Sqrt(2 * 9.81 * 3.5), Drop(3.5, 0, open).Impact, 0.4, "it lands at about the speed of a free fall");
        }

        [Test]
        public void TheWindCarriesAFallingPackage()
        {
            var open = Town.Bare();
            double still = Drop(3.5, 0, open).X, breeze = Drop(3.5, 8, open).X, high = Drop(30, 8, open, 0, "ring").X;
            Assert.AreEqual(-100, still, 1e-9);
            Assert.Greater(breeze, still + 0.8, "about a metre in the relief flights' wind");
            Assert.Less(breeze, still + 2.5);
            Assert.Greater(high, breeze + 5, "much further from high up");
        }

        [Test]
        public void APackageDroppedInTheFloodIsLostButALifeRingFloats()
        {
            var open = Town.Bare();
            Assert.AreEqual(PackageFate.Sunk, Drop(5, 0, open, 2.2, "water", 0, 60).Fate);
            var ring = Drop(5, 0, open, 2.2, "ring", 0, 60);
            Assert.AreEqual(PackageFate.Missed, ring.Fate);
            Assert.AreEqual(2.2, ring.Y, 1e-9, "on the water");
        }
    }
}
