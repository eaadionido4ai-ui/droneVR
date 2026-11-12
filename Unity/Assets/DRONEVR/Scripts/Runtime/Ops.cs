// The panels of the operations center.
//   The front panel, between the door and the map table, shows one thing at a time: the start card, the
//   mission board, a mission's briefing (where the operator chooses how to fly it: Command Desk, FPV Pilot or
//   Hybrid), the wait for the class, the mission's report, and the summary of the operation.
//   On the west wall: the missions and their best results, and the radio control's buttons.
//   On the north wall, beside the big screen: the link to GAMA and the class.
//   Labels: the pilot's seat.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DroneVR.Vr
{
    public sealed class Ops
    {
        readonly DroneApp app;
        readonly World world;
        readonly Panel front, missions, controls, status;
        readonly TextMeshPro missionsText, controlsText, statusText;
        Mode chosen = Mode.Hybrid;
        string briefing;

        Lang L { get { return app.Lang; } }

        public Ops(DroneApp app)
        {
            this.app = app; world = app.World;
            front = new Panel(world, null, "Front", 1.5f, 1.1f, Palette.Navy);
            front.Place(world.Point("start"), world.Point("spawn") + Vector3.up * 1.5f);
            missions = new Panel(world, null, "Missions", 2.2f, 1.2f, Palette.Paper);
            missions.PlaceOnWall(world.Point("board"), world.Facing("board"));
            missionsText = missions.Text("", 0, 0, 2.08f, 1.1f, 0.42f, Palette.Navy);
            controls = new Panel(world, null, "Controls", 2.2f, 1.2f, Palette.Navy);
            controls.PlaceOnWall(world.Point("brief"), world.Facing("brief"));
            controlsText = controls.Text("", 0, 0, 2.08f, 1.1f, 0.38f, Palette.Cream);
            status = new Panel(world, null, "Status", 1.3f, 0.8f, Palette.Navy);
            status.PlaceOnWall(world.Point("status"), world.Facing("status"));
            status.Text("SIMPLE · GAMA", 0, 0.3f, 1.2f, 0.12f, 0.45f, Palette.Gold, TextAlignmentOptions.Center, true);
            statusText = status.Text("", 0, -0.08f, 1.2f, 0.58f, 0.32f, Palette.Cream, TextAlignmentOptions.Center);
            var seat = new Panel(world, null, "Seat", 0.9f, 0.26f, Palette.Teal);
            seat.PlaceOnWall(world.Point("pilot_label"), world.Facing("pilot_label"));
            seatText = seat.Text("", 0, 0, 0.84f, 0.22f, 0.4f, Palette.White, TextAlignmentOptions.Center, true);
        }
        readonly TextMeshPro seatText;

        public Mode Chosen { get { return chosen; } set { chosen = value; } }
        public bool FrontVisible { get { return front.Visible; } }
        public void HideFront() { front.Visible = false; }

        /// <summary>Puts the front panel somewhere else (the report, beside the map table).</summary>
        void At(string anchor)
        {
            front.Visible = true;
            front.Place(world.Point(anchor), world.Point("spawn") + Vector3.up * 1.5f);
        }

        // ---------- the start card ----------

        public void ShowStart()
        {
            At("start"); front.Clear(); front.SetBackground(Palette.Navy);
            front.Text(Strings.Get("start.title", L), 0, 0.42f, 1.4f, 0.17f, 0.8f, Palette.Gold, TextAlignmentOptions.Center, true);
            front.Text(Strings.Get("start.sub", L), 0, 0.29f, 1.4f, 0.08f, 0.3f, Palette.Cream, TextAlignmentOptions.Center);
            front.Text(Strings.Get("start.body", L), 0, 0.1f, 1.38f, 0.26f, 0.36f, Palette.White, TextAlignmentOptions.Center);
            front.Text(Strings.Get(app.Rig.Desktop ? "start.howto.desktop" : "start.howto", L), 0, -0.13f, 1.38f, 0.16f, 0.3f, Palette.Cream, TextAlignmentOptions.Center);
            front.Button(Strings.Get("start.lang", L), -0.36f, -0.39f, 0.62f, 0.15f, Palette.Teal, () => { app.SetLang(L == Lang.Fil ? Lang.En : Lang.Fil); ShowStart(); });
            front.Button(Strings.Get("start.go", L), 0.36f, -0.39f, 0.62f, 0.15f, Palette.Orange, () => app.OpenBoard());
        }

        // ---------- the mission board ----------

        public void ShowBoard()
        {
            At("start"); front.Clear(); front.SetBackground(Palette.Navy);
            front.Text(Strings.Get("board.title", L), 0, 0.47f, 1.4f, 0.1f, 0.5f, Palette.Gold, TextAlignmentOptions.Center, true);
            front.Text(Strings.Get("board.pick", L), 0, 0.385f, 1.4f, 0.06f, 0.26f, Palette.Cream, TextAlignmentOptions.Center);
            for (int i = 0; i < Mission.Ids.Length; i++)
            {
                string id = Mission.Ids[i];
                float y = 0.25f - i * 0.18f;
                var best = app.Operation.Best(id);
                string line = Strings.Get("mission." + id + ".name", L) + "  ·  " + Strings.Get("mission." + id + ".title", L);
                string sub = best != null ? Strings.Format("board.best", L, Strings.Stars(best.Stars, L), best.Score) : id == "lipad" ? Strings.Get("board.first", L) : Strings.Get("board.notflown", L);
                var b = front.Button(line, -0.08f, y, 1.2f, 0.15f, id == "lipad" ? Palette.Teal : Palette.Green, () => app.OpenBrief(id));
                b.Label.alignment = TextAlignmentOptions.Left; b.Label.fontSizeMax = 0.34f;
                front.Text(sub, 0.6f, y, 0.26f, 0.14f, 0.22f, Palette.Cream, TextAlignmentOptions.Center);
            }
            front.Button(Strings.Get("btn.lang", L), -0.4f, -0.47f, 0.5f, 0.11f, Palette.Grey, () => { app.SetLang(L == Lang.Fil ? Lang.En : Lang.Fil); ShowBoard(); });
            front.Button(Strings.Get("btn.summary", L), 0.4f, -0.47f, 0.5f, 0.11f, Palette.Grey, () => ShowSummary());
        }

        // ---------- a mission's briefing ----------

        public void ShowBrief(string id)
        {
            briefing = id;
            At("start"); front.Clear(); front.SetBackground(Palette.Navy);
            var plan = TownPlan.Mission(id);
            front.Text(Strings.Get("mission." + id + ".name", L) + "  ·  " + Strings.Get("mission." + id + ".title", L), 0, 0.48f, 1.42f, 0.09f, 0.42f, Palette.Gold, TextAlignmentOptions.Center, true);
            front.Text(Strings.Get("mission." + id + ".brief", L), 0, 0.35f, 1.42f, 0.16f, 0.28f, Palette.White);
            front.Text("<b>" + Strings.Get("mission." + id + ".task", L) + "</b>", 0, 0.205f, 1.42f, 0.13f, 0.28f, Palette.Cream);
            string flood = "";
            var lev = plan.FloodLevels;
            if (lev[0] > 0.05 || lev[lev.Length - 1] > 0.05)
                flood = "  " + Strings.Format(lev[lev.Length - 1] > lev[0] + 0.05 ? "brief.flood.rising" : lev[lev.Length - 1] < lev[0] - 0.05 ? "brief.flood.falling" : "brief.flood", L, lev[0].ToString("0.0"));
            front.Text(Strings.Format("brief.weather", L, plan.Wind.ToString("0"), Strings.Direction(plan.WindFrom, L), Wind.Name(plan.Wind, L)) + flood + "  " + Strings.Format("brief.minutes", L, plan.Minutes),
                0, 0.095f, 1.42f, 0.08f, 0.24f, Palette.Sky);
            front.Text(Strings.Get("brief.mode", L), 0, 0.015f, 1.42f, 0.06f, 0.28f, Palette.Gold, TextAlignmentOptions.Center, true);
            var modes = new[] { Mode.Desk, Mode.Pilot, Mode.Hybrid };
            for (int i = 0; i < 3; i++)
            {
                var m = modes[i];
                var b = front.Button(Strings.Get("mode." + m.ToString().ToLowerInvariant(), L), -0.48f + i * 0.48f, -0.075f, 0.44f, 0.1f, Palette.Teal, () => { chosen = m; ShowBrief(id); });
                b.Chosen(m == chosen);
            }
            front.Text(Strings.Get("mode." + chosen.ToString().ToLowerInvariant() + ".about", L), 0, -0.19f, 1.42f, 0.11f, 0.26f, Palette.Cream, TextAlignmentOptions.Center);
            front.Text("<size=85%>" + Strings.Get("mission." + id + ".tip", L) + "</size>", 0, -0.32f, 1.42f, 0.13f, 0.24f, Palette.Sky, TextAlignmentOptions.Center);
            front.Button(Strings.Get("brief.back", L), -0.4f, -0.47f, 0.5f, 0.11f, Palette.Grey, () => app.OpenBoard());
            front.Button(Strings.Get("brief.begin", L), 0.4f, -0.47f, 0.5f, 0.11f, Palette.Orange, () => app.Begin(id, chosen));
        }

        /// <summary>Ready: waiting for GAMA to start the class's mission.</summary>
        public void ShowWaiting(int ready, int players)
        {
            At("start"); front.Clear(); front.SetBackground(Palette.Navy);
            front.Text(Strings.Get("mission." + briefing + ".name", L), 0, 0.3f, 1.4f, 0.15f, 0.7f, Palette.Gold, TextAlignmentOptions.Center, true);
            front.Text(players > ready ? Strings.Format("brief.wait.class", L, ready, players) : Strings.Get("brief.ready", L), 0, 0.02f, 1.4f, 0.3f, 0.42f, Palette.White, TextAlignmentOptions.Center);
            front.Button(Strings.Get("brief.back", L), 0, -0.4f, 0.6f, 0.13f, Palette.Grey, () => app.Leave());
        }

        // ---------- the report and the summary ----------

        public void ShowResults(Flight f, MissionResult r)
        {
            At("results"); front.Clear(); front.SetBackground(Palette.Navy);
            string id = f.Mission.Id;
            front.Text(Strings.Format("results.title", L, Strings.Get("mission." + id + ".name", L)) + "  ·  " + Strings.Get("mode." + f.Mode.ToString().ToLowerInvariant(), L), 0, 0.48f, 1.42f, 0.08f, 0.36f, Palette.Gold, TextAlignmentOptions.Center, true);
            front.Text(Strings.Format("results.score", L, Stars(r.Stars), r.Score), 0, 0.375f, 1.42f, 0.11f, 0.5f, Palette.White, TextAlignmentOptions.Center, true);
            front.Text(Strings.Format("results.parts", L, Mathf.RoundToInt((float)(r.Tasks * 100)), Mathf.RoundToInt((float)(r.Safety * 100)), Mathf.RoundToInt((float)(r.Pace * 100))), 0, 0.29f, 1.42f, 0.06f, 0.24f, Palette.Sky, TextAlignmentOptions.Center);
            var sb = new System.Text.StringBuilder();
            foreach (var line in r.Lines) sb.Append("<color=").Append(Palette.Html(Palette.Of(line.Kind))).Append('>').Append(line.Text(L)).Append("</color>\n");
            front.Text(sb.ToString(), 0, -0.02f, 1.42f, 0.52f, 0.28f, Palette.Cream);
            front.Button(Strings.Get("btn.again", L), -0.48f, -0.46f, 0.44f, 0.11f, Palette.Orange, () => app.OpenBrief(id));
            front.Button(Strings.Get("btn.board", L), 0, -0.46f, 0.44f, 0.11f, Palette.Teal, () => app.OpenBoard());
            front.Button(Strings.Get("btn.summary", L), 0.48f, -0.46f, 0.44f, 0.11f, Palette.Grey, () => ShowSummary());
        }

        /// <summary>Stars drawn with letters the font has: a filled star is *, an empty one is a dot.</summary>
        static string Stars(int n)
        {
            var sb = new System.Text.StringBuilder("<color=" + Palette.Html(Palette.Gold) + ">");
            for (int i = 0; i < 3; i++) sb.Append(i < n ? "*" : "<color=#7a8a94>·</color>");
            return sb.Append("</color>").ToString();
        }

        public void ShowSummary()
        {
            At("start"); front.Clear(); front.SetBackground(Palette.Navy);
            app.Operation.Lang = L;
            front.Text(Strings.Get("sum.title", L), 0, 0.46f, 1.4f, 0.1f, 0.5f, Palette.Gold, TextAlignmentOptions.Center, true);
            var lines = app.Operation.Summary();
            front.Text(string.Join("\n", lines.GetRange(0, lines.Count - 1)), 0, 0.12f, 1.38f, 0.5f, 0.32f, Palette.White);
            front.Text(lines[lines.Count - 1], 0, -0.27f, 1.38f, 0.24f, 0.28f, Palette.Cream, TextAlignmentOptions.Center);
            front.Button(Strings.Get("btn.board", L), 0, -0.46f, 0.6f, 0.11f, Palette.Teal, () => app.OpenBoard());
        }

        // ---------- the wall boards ----------

        public void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<b><size=120%>").Append(Strings.Get("board.title", L)).Append("</size></b>\n");
            foreach (var id in Mission.Ids)
            {
                var best = app.Operation.Best(id);
                sb.Append("<b>").Append(Strings.Get("mission." + id + ".name", L)).Append("</b>  ").Append(Strings.Get("mission." + id + ".title", L)).Append("   ")
                  .Append(best != null ? Stars(best.Stars) + "  " + best.Score : "<color=#7a8a94>" + Strings.Get("board.notflown", L) + "</color>").Append('\n');
            }
            missionsText.text = sb.ToString();
            controlsText.text = "<b><size=120%>" + Strings.Get(app.Rig.Desktop ? "hud.controls.desktop" : "hud.controls", L).Replace("{0}", Strings.Get("tool.photo", L)).Replace(" · ", "\n") + "</size></b>\n"
                + (app.Rig.Desktop ? "" : Strings.Get("hud.controls.hybrid", L).Replace(" · ", "\n") + "\n") + "\n" + Strings.Get("hud.ceiling", L);
            seatText.text = Strings.Get("pilot.seat", L);
        }

        /// <summary>The board beside the big screen: whether GAMA is there, and the class.</summary>
        public void Status(string line, int ready, int players, int othersFlying)
        {
            statusText.text = line + (players > 0 ? "\n" + Strings.Format("class.ready", L, ready, players) : "")
                + (othersFlying > 0 ? "\n" + Strings.Format("class.drones", L, othersFlying) : "");
        }
    }
}
