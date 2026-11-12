// When a mission starts. The operator reads the briefing, chooses how to fly, and presses Begin.
//   Alone (GAMA not connected): the mission starts at once.
//   With a class (GAMA connected): the headset tells GAMA it is ready. GAMA starts the mission for the whole
//   class when every headset is ready (StartDelay seconds later), or when the teacher says so, so that the
//   class flies one incident together: the same wind, the same flood, the same people to find, the same
//   boats. The class flies the mission its first ready headset chose. A headset that asks while the class is
//   already well into a mission is told so, and flies its own.
// If GAMA says nothing for GamaPatience seconds, the headset goes on alone.
// The GAMA side is in GAMA/models/DRONE VR.gaml (reflex gather); Tools/check_constants.py checks the numbers.
namespace DroneVR
{
    public sealed class MissionStart
    {
        /// <summary>Seconds between the last headset being ready and the start. GAMA waits as long (start_delay).</summary>
        public const double StartDelay = 3;
        /// <summary>Seconds without a word from GAMA, after Begin, before the headset goes on alone.</summary>
        public const double GamaPatience = 8;

        /// <summary>The mission to fly: the one the operator chose, or the class's once GAMA has started it.</summary>
        public string Mission { get; private set; }
        /// <summary>The operator pressed Begin.</summary>
        public bool Asked { get; private set; }
        /// <summary>True from the moment the mission should begin.</summary>
        public bool Go { get; private set; }
        /// <summary>True if GAMA started it: this headset flies with the class, in GAMA's weather.</summary>
        public bool ByGama { get; private set; }
        /// <summary>Seconds the class's mission had already run when this headset joined it.</summary>
        public double Clock { get; private set; }
        /// <summary>GAMA's count: how many headsets are ready, of how many, and for which mission.</summary>
        public int Ready { get; private set; }
        public int Players { get; private set; }
        public string ClassMission { get; private set; }

        double sinceGama;

        /// <summary>Ready, and GAMA is waiting for classmates who are not.</summary>
        public bool WaitingForClass { get { return Asked && !Go && Players > Ready; } }

        public void Ask(string mission)
        {
            Mission = mission; Asked = true; Go = false; ByGama = false; Clock = 0; sinceGama = 0;
        }

        /// <summary>The operator went back to the board, or the mission is over.</summary>
        public void Reset()
        {
            Asked = false; Go = false; ByGama = false; Clock = 0; Mission = null;
        }

        /// <summary>GAMA's word on the class (it comes every two seconds).</summary>
        public void GamaSays(int ready, int players, string classMission)
        {
            Ready = ready; Players = players; ClassMission = classMission; sinceGama = 0;
        }

        /// <summary>
        /// GAMA starts a mission, clock seconds ago. It is the class's: a headset that asked for another one flies
        /// this one, and so does a headset that had not asked yet (the teacher started it).
        /// </summary>
        public void GamaStarts(string mission, double clock)
        {
            if (Go) return;
            Mission = mission; Asked = true; Go = true; ByGama = true; Clock = clock;
        }

        /// <summary>GAMA is well into another mission with the class: this headset flies its own.</summary>
        public void GamaBusy()
        {
            if (Asked && !Go) { Go = true; ByGama = false; }
        }

        public void Update(double dt, bool gamaConnected)
        {
            if (!Asked || Go) return;
            sinceGama += dt;
            if (!gamaConnected || sinceGama > GamaPatience) { Go = true; ByGama = false; }
        }
    }
}
