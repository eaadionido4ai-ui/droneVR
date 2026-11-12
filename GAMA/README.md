# DRONE VR in GAMA

This folder is a SIMPLE learning package. Add it to the SIMPLE WebPlatform; it reads `settings.json`
(experiment `vr_xp` of `models/DRONE VR.gaml`, 1 to 6 headsets).

| File | What it is |
|---|---|
| `models/DRONE.gaml` | The model: the barangay's ground and the flood over it, each mission's wind (with its gusts) and water level, the people stranded by the flood (agents), and the rescue boats (agents) that go to the groups the drones find, along the flooded streets, and bring them back. It runs on its own too, with its own map and buttons. |
| `models/DRONE VR.gaml` | The link to the Unity app, through the SIMPLE plugin: the `unity_linker` and `unity_player` species and the `vr_xp` experiment. It decides when the class's mission starts, and shares what each drone finds with the whole class. |
| `models/DRONE Town.gaml` | The places: the buildings, the streets, the gates, the hazards, the survivors, the sites, the boats' landings and their ways through the flood. Written by `Tools/world-export/gen-data.mjs` from the plan the 3D models and Unity's data are made from (`Tools/world-export/src/plan.js`). Do not edit it by hand. |
| `results/dronevr_log.csv` | Written while the model runs: what each headset does, by headset id only (no names). |

The numbers (the ground, the wind, the boats, the class's start) are the same as in Unity
(`Unity/Assets/DRONEVR/Scripts/Core`) and in the Python reference (`Tools/flight-reference`).
`Tools/check_constants.py` checks that they agree; change a number in every place.

Places use DRONE VR's coordinates (metres; x east, z south), written in GAML as points `{x, z}`. GAMA draws them
at (x + 240, z + 180), and Unity puts them at (x, y, -z).

## A mission for the class

1. Each operator chooses a mission and how to fly it, and presses **Begin**: the headset tells GAMA it is ready
   (`dr_ready`). The class flies the mission its first ready headset chose. Every two seconds GAMA tells every
   headset how many are ready (`class`).
2. When every headset is ready, GAMA waits 3 seconds (`start_delay`) and starts the mission for all of them
   (`start`). If some are still not ready 30 seconds after the first (`ready_patience`), it starts without them.
   A headset that is ready within the first 20 seconds of the mission (`join_window`) joins it.
3. During the mission GAMA sends the weather to the headsets that fly it (`weather`): one wind and one flood
   for the class. It keeps the class's progress: the best photograph of each hazard, the groups found and the
   life rings that reached them, the needs met at each site, and sends it every second (`targets`). In SAGIP it
   moves the rescue boats (`boats`). Every headset sees the others' drones (`drones`).
4. The mission ends when its time is up (`end`), when no headset is flying it any more, or when the teacher ends
   it. Each headset sends its own result (`dr_result`).

The teacher's buttons, in GAMA's experiment panel: **Start the class's mission now**, **End the class's
mission**, **Raise the wind** and **Calm the wind** (from half to twice the mission's own wind).

Without GAMA, a headset flies on its own, with the same rules, weather and boats (in `DroneVR.Core`). If GAMA
says nothing for 8 seconds after **Begin**, or stops sending the weather for 3 seconds, the headset goes on alone.

## The messages

**From GAMA to Unity** (`send_message`). Each is a map whose `dronevr` entry says what it is:

| `dronevr` | Other entries | Sent | To |
|---|---|---|---|
| `class` | `ready`: how many headsets are ready or flying; `players`: how many headsets; `mission`: the mission the class is getting ready for or flying (`""` if none) | every two seconds | every headset |
| `start` | `mission`: its name (`lipad`, `bantay`, `sagip`, `hatid`); `t`: the milliseconds it has already run | when the class's mission starts, or a late headset joins it | those headsets |
| `busy` | | a headset is ready while the class is well into a mission: it flies its own | that headset |
| `end` | | the mission is over | the headsets flying it |
| `weather` | `t`: milliseconds of the mission; `wind`, `mean`: the wind's speed now and its mean, cm/s at 60 m up; `from`: where it blows from, tenths of a degree; `level`: the flood's level, mm above the river's normal level | at every step | the headsets flying it |
| `targets` | `mission`; `s`: BANTAY, the best photograph of each hazard (%); SAGIP, for each group 1 found + 2 a life ring reached it + 4 aboard a boat; HATID, for each site 1 for its first need met + 2 for its second | every second, and at once after a change | the headsets flying it |
| `boats` | `p`: for each boat x, z (dm), heading (degrees), what it is doing (0 waiting, 1 out, 2 taking people aboard, 3 back, 4 putting them ashore), the group it goes to (or -1) | at every step of SAGIP | the headsets flying it |
| `drones` | `ids`: the headsets whose drones are flying; `p`: for each x, y, z (dm), heading (degrees), battery (%) | four times a second, with two headsets or more | the headsets flying it |

**From Unity to GAMA** (actions of `unity_linker`; every argument is a string, the first is the headset's id):

| Action | When |
|---|---|
| `dr_hello(id, lang)` | the app starts, or GAMA connects (`fil` or `en`) |
| `dr_event(id, kind, detail)` | anything else worth logging: a mission begun, a drone lost |
| `dr_ready(id, mission, mode)` | the operator pressed **Begin** (`desk`, `pilot` or `hybrid`) |
| `dr_leave(id)` | the operator went back to the board without flying |
| `dr_drone(id, x, y, z, yaw, battery, aloft)` | twice a second during a mission: where the drone is (dm), its heading, its battery (%), and whether it is in the air (1 or 0) |
| `dr_gate(id, n)` | LIPAD: the drone flew through ring n |
| `dr_photo(id, hazard, quality)` | BANTAY: a better photograph of a hazard (its number, and %) |
| `dr_found(id, who)` | SAGIP: the drone marked a group (its number) |
| `dr_ring(id, who)` | SAGIP: a life ring reached a group |
| `dr_delivered(id, site, need)` | HATID: a package landed on its mark: that need of that site is met |
| `dr_result(id, mission, stars, score, seconds)` | the mission is over on the headset |

## Not yet done

- The models have not been run in GAMA yet. They were checked by hand against the SIMPLE plugin's source. Open
  `models/DRONE VR.gaml` in GAMA 2025.06 with the SIMPLE plugin and report what does not compile.
- Each headset keeps its own score, and its own timing of the boats; GAMA's boats are the ones it shows.
- The operators are not shown to each other in the operations center (the SIMPLE plugin can show other players;
  DRONE VR shows their drones instead).
