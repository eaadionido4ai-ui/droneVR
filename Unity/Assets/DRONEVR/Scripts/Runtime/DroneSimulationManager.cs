// DRONE VR's link to GAMA, built on the SIMPLE template's SimulationManager (Assets/Scripts/Gama). The
// template connects to the SIMPLE WebPlatform, joins the GAMA experiment, places the operator where GAMA says,
// and keeps telling GAMA where the operator is. This class adds what DRONE VR needs on top:
//   - the messages GAMA sends (the class, the start of a mission, the weather, the targets, the boats, the
//     other drones), passed to Scenario;
//   - the actions DRONE VR asks GAMA to do (dr_ready, dr_found, dr_delivered, ...);
//   - the position of DRONE VR's world inside GAMA's world (see the constructor).
// The model on the GAMA side is GAMA/models/DRONE VR.gaml.
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DroneVR.Vr
{
    public class DroneSimulationManager : SimulationManager
    {
        /// <summary>
        /// GAMA's world starts at its north-west corner and counts y southwards; DRONE VR's coordinates have their
        /// origin in the middle of the barangay. GAMA's (x, y) is DRONE VR's (x + 240, z + 180): the same numbers
        /// are in the GAML model (offset_x and offset_y).
        /// </summary>
        public const float OffsetX = 240f, OffsetY = 180f;

        public DroneSimulationManager()
        {
            // the template turns GAMA's (x, y) into Unity's (x * coef + offsetX, -y * coef + offsetY)
            GamaCRSOffsetX = -OffsetX;
            GamaCRSOffsetY = OffsetY;
        }

        /// <summary>Called for every DRONE VR message from GAMA: its "dronevr" kind and the whole message.</summary>
        public static event Action<string, JObject> OnDroneMessage;

        public static bool Linked
        {
            get
            {
                return ConnectionManager.Instance != null && ConnectionManager.Instance.IsConnectionState(ConnectionState.AUTHENTICATED)
                    && Instance != null && Instance.IsGameState(GameState.GAME);
            }
        }

        /// <summary>This headset's id, as GAMA knows it.</summary>
        public static string MyId { get { return ConnectionManager.Instance != null ? ConnectionManager.Instance.GetConnectionId() : ""; } }

        /// <summary>Asks the unity_linker agent in GAMA to do an action (only when connected).</summary>
        public static void Ask(string action, Dictionary<string, string> args)
        {
            if (!Linked) return;
            if (!args.ContainsKey("id")) args["id"] = ConnectionManager.Instance.GetConnectionId();
            ConnectionManager.Instance.SendExecutableAsk(action, args);
        }

        public static void Ask(string action, params string[] keyValues)
        {
            var args = new Dictionary<string, string>();
            for (int i = 0; i + 1 < keyValues.Length; i += 2) args[keyValues[i]] = keyValues[i + 1];
            Ask(action, args);
        }

        protected override void ManageOtherMessages(string content)
        {
            JObject o;
            try { o = JObject.Parse(content); }
            catch (Exception) { return; }
            var kind = (string)o["dronevr"];
            if (kind == null) return;
            if (OnDroneMessage != null) OnDroneMessage(kind, o);
        }
    }
}
