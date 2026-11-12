# DRONE VR

**Drones for disaster risk reduction and management, in virtual reality**

DRONE VR puts the player in the emergency operations center (EOC) of a riverside barangay, as its drone
operator. After a flight school, they fly three missions, one for each use of drones in a disaster:
**BANTAY**, watching for hazards after an earthquake (environmental safety); **SAGIP**, finding people stranded
by a flood (search and rescue); and **HATID**, carrying relief to places cut off by a typhoon (logistics).

It is built like [TANGHALAN VR](https://github.com/eaadionido4ai-ui/tanghalanVR), with the tools of the SIMPLE
VR contest:

- **Unity**: the project is the SIMPLE Unity Template VR (`project-SIMPLE/simple.toolchain`, branch `Unity-6`) for
  Meta Quest headsets, with DRONE VR added in `Unity/Assets/DRONEVR`.
- **GAMA**: the incident is an agent-based model in `GAMA/models`: the weather and the flood, the people waiting
  for rescue, and the rescue boats that go to those the drones find. It runs the incident for a whole class at
  once, and talks to the headsets through the SIMPLE GAMA plugin and the SIMPLE WebPlatform.
- **Blender**: every model (the barangay, the operations center, the drone, the supplies, the people, the boat)
  passes through Blender. The `.blend` files are in `Blender/`, and the scripts that make them are in
  `Tools/blender`.

The barangay, its houses, streets and hazards, and what each mission looks for, all come from one plan
(`Tools/world-export/src/plan.js`). The models, Unity's data and GAMA's data are all made from it, so they agree.

> 🧪 Version 0.1.0, for review. The rules and the simulations pass their tests, and the Unity scripts compile
> against Unity 6000.3.8f1. It has **not yet been played in the Unity editor or on a headset**, and the GAMA model
> has not yet been run in GAMA (see [What has been checked](#what-has-been-checked)).

## The operations center

The operator stands in the BDRRMC operations center, a room under the barangay hall. Everything is there:

- **The map table**, in the middle: a model of the whole barangay, one metre for every 200, in the scene of the
  mission (rubble after the earthquake, water at the flood's level). On it: the drone, its route, and what the
  mission is about. Its two consoles show the drone, the route and its cost, what the mission still needs, and
  the radio's messages.
- **The big screen**, on the north wall: what the drone's camera sees, with the pilot's display over it.
- **The loading dock and the shelf of supplies**, on the east wall, with a miniature of the drone. What the
  operator hangs on the miniature, the ground crew hangs on the drone at the pad.
- **The pilot's seat**, in the south-east corner, for flying with the FPV goggles.
- **The mission board** at the front, and on the walls the missions' best results and the radio control's buttons.

## The missions

| Mission | Use of drones | The incident | Weather | What to do |
|---|---|---|---|---|
| **LIPAD** | flight school | a calm morning | 1.5 m/s | take off, fly through six rings in order, land on the pad |
| **BANTAY** | environmental safety | just after a strong earthquake | 3 m/s, dust | find the seven hazards near the places the residents reported (a fire, a fallen power line, a crack in the bridge, a landslide, a fuel spill, a leaning water tower, debris damming the creek), and photograph each one from a safe distance |
| **SAGIP** | search and rescue | the river over its banks, still rising (2.2 m to 2.8 m) | 5 m/s, rain | find the eight groups of people (18 in all) and mark them: a rescue boat goes to each one marked. Some are hidden under roofs and trees: the thermal camera finds them, from close. One man is in fast water: a boat can reach him only after the drone drops him a life ring |
| **HATID** | logistics | after a typhoon: the bridge is out, streets are blocked | 9 m/s, gusty | carry what four places asked for (the evacuation center, the sitio across the river, the health center, a family on a roof) and drop it on the mark: 8 kg at most on three hooks, from below 4 m, aiming against the wind |

The drone flies as a GPS-stabilized drone does: let go of the sticks and it holds its place, leaning into the
steady wind by itself. It cannot cancel every gust, fly through the air faster than 18 m/s, or carry weight for
free: a load makes it slower and drains the battery faster. A battery hovers seven minutes. It breaks on a hard
hit or a hard landing, and is lost in water; a spare is on the pad five seconds later, with empty hooks. On the
pad the battery is changed in four seconds. It never flies higher than 120 m above the pad, the 400 feet of the
CAAP's rules for drones.

A mission's report gives up to three stars from a score out of 100: the task itself (70), flying safely (18:
drones lost, flying too close to a hazard, coming home) and time (12). The operations summary shows each
mission's best result, and says when the three uses of drones have all been flown.

## Three ways to fly

In each mission's briefing, the operator chooses how to fly it:

- **Command Desk** (god's-eye, at the map table). Point at the map and pull the trigger to put a waypoint; the
  console sets its height. Over a site that asked for supplies the drone will come down and drop; on a hazard
  patrol it stops to look; near a ring of the flight school it goes through the middle. The console says what
  the route will cost in metres, time and battery before the drone leaves. Hang the supplies on the drone at the
  dock, press **Launch**, and watch the drone on the table and on the big screen. The autopilot comes home by
  itself when the battery would not last.
- **FPV Pilot** (first person). The operator sits in the pilot's seat and puts on the FPV goggles: the view rides
  on the drone, with the pilot's display in front of the eyes. The two controllers are the drone's radio control.
  The edges of the view darken when the drone moves fast or turns, for comfort. **A** takes the goggles off, to
  fly from the big screen and the map table.
- **Hybrid**. The route is planned on the table and flown by the autopilot, and the operator **takes the sticks**
  when it matters (the console's button, or **X**), with the goggles on, then **hands them back**: the autopilot
  goes on with the route from where the drone is.

What the autopilot does not do is what makes Hybrid worth it: it does not see obstacles (a route through the
mobile phone tower hits it), it frames hazards less sharply than a pilot, it drops from straight above the mark
(and the wind carries the package off a small roof now and then), and it finds hidden people only when the route
passes low over them. A well-planned route earns two or three stars on its own; the sharpest photographs, the
hidden people and a sure drop on a small roof in the wind need a pilot.

## Running it

You need **Unity 6000.3.8f1** (exactly this version, as the SIMPLE template requires) with Android Build Support,
OpenJDK and the Android SDK & NDK tools. See the SIMPLE guide:
[doc.project-simple.eu/unity/installation](https://doc.project-simple.eu/unity/installation).

1. In Unity Hub, **Add project from disk** and choose the `Unity` folder of this repository. The first import takes a while.
2. Open `Assets/DRONEVR/Scenes/Main Scene` and press **Play**.

**With no headset**, the scene plays with the keyboard and the mouse (DRONE VR switches to this by itself):

| | With a Meta Quest | With the keyboard and mouse |
|---|---|---|
| Walk in the operations center | left stick | W A S D |
| Jump to a spot | right stick forward, aim, let go | |
| Turn | right stick left and right | right mouse button and drag, or Q and E |
| Point and choose; put a waypoint on the map | aim the controller, **trigger** | aim with the mouse, **left click** |
| Hang a crate on the drone at the dock | **grip** it, carry it to the drone, let go; or point at it and pull the **trigger** | click the crate |
| **Flying** (holding the radio control) | | |
| Up and down, turn | left stick | R F, Q E |
| Forward and back, sideways | right stick | W S, A D |
| The drone's camera: photograph, mark people, drop | right **trigger** | left click |
| Let a package go (the life ring) | right **grip** | G |
| The goggles on or off | **A** | Tab |
| The thermal camera | **B** | T |
| Hand the drone back to the autopilot (Hybrid) | **X** | H |
| Call the drone home | **Y** | Y |
| Gently, for drops and photographs | push the sticks a little | hold Shift |

**On a Meta Quest**:

1. In **File > Build Profiles**, choose **Android** and click **Switch Platform**.
2. With the headset in developer mode and plugged in, click **Build and Run**.
3. The app opens the SIMPLE Startup Menu. Start it, or set the computer's IP address first in the IP Menu.

**With GAMA** (a SIMPLE session):

1. Install GAMA 2025.06 with the SIMPLE plugin, and the SIMPLE WebPlatform
   ([doc.project-simple.eu](https://doc.project-simple.eu)).
2. Add this repository's `GAMA` folder as a learning package: it has the `settings.json` the WebPlatform reads.
3. Launch **DRONE VR** from the WebPlatform, then start the app in Unity or on the headsets.

The board beside the big screen says when GAMA is connected. **A class flies one incident together.** Up to 6
headsets can join one session:

- Each operator chooses a mission and presses **Begin**. When every headset is ready, GAMA starts the mission for
  all of them: the class flies the mission its first ready operator chose, each in the way they chose.
- They fly in one weather and one flood, sent by GAMA. A hazard photographed, a group found, a need met by one
  drone counts for the whole class, and the rescue boats are GAMA's. Every operator sees the others' drones, on
  the map table and in the sky.
- The teacher can start or end the class's mission, and **raise** or **calm** the wind, from GAMA's experiment.
- GAMA writes what the operators do to `GAMA/results/dronevr_log.csv` (by headset id only, no names).

Each operator still has their own score. **Without GAMA**, or as soon as GAMA stops answering, the app runs the
same rules and weather itself (in `DroneVR.Core`), so a class without a laptop can still fly. See
[GAMA/README.md](GAMA/README.md) for the messages between GAMA and Unity.

## How it is put together

```
Unity/                          the SIMPLE Unity Template VR (Unity 6000.3.8f1), with:
  Assets/DRONEVR/
    Scenes/Main Scene.unity     the template's FPS player scene, with DRONE VR's Game Manager and app
    Scripts/Core/               the rules and the simulations, with no Unity in them (also built and tested outside Unity)
      Terrain, Town             the ground (a formula) and what stands on it; what a drone hits and lands on
      Weather                   each mission's wind, with its gusts, and the flood's level
      Drone                     how the drone flies, carries, drains its battery, lands and breaks
      Autopilot                 flies a route of waypoints, drops, comes home; what a route will cost
      Cargo                     the hooks, and how a package falls in the wind
      Mission, Rescue           the four missions, their rules and scores; the rescue boats
      Flight, MissionStart      one mission flown three ways; when a class's mission starts
      Strings                   every text in English and Filipino
      Generated/TownPlan.g.cs   the barangay's buildings, trees, targets and the boats' ways, written from the plan
    Scripts/Runtime/            the Unity side: DroneApp (the operation), World (loading the models), Rig (the operator
                                and the radio control), Ui (panels, pointing), Ops (the panels), Desk (the map table),
                                Dock (the supplies), DroneView (the drone, its camera, the goggles), MissionScene (the
                                barangay during a mission), Sfx, Scenario (GAMA or local), DroneSimulationManager
                                (the template's SimulationManager)
    Tests/EditMode/             the tests, for Unity's Test Runner and for `dotnet test Tests`
    Resources/DRONEVR/          the models (FBX), textures and materials
  Assets/Scripts, Scenes, ...   the SIMPLE template, unchanged
GAMA/                           the SIMPLE learning package: settings.json, models/DRONE.gaml (the model),
                                models/DRONE VR.gaml (the link to Unity), models/DRONE Town.gaml (the places, generated)
Blender/                        the Blender files of the barangay, the operations center and the props
Tools/
  world-export/                 the plan of the barangay (src/plan.js); builds the models in three.js and exports them;
                                gen-data.mjs writes the plan for Unity and GAMA
  blender/                      turns the exports into .blend and FBX files, with one palette of colors; renders previews
  build_assets.sh               runs the whole asset pipeline
  unity_meta.py, make_materials.py, make_scene.py    write Unity's .meta files, materials, main scene and build settings
  flight-reference/             the ground, the wind and the drone's flight in Python: the reference the C# is checked against
  check_constants.py            checks that Unity, GAMA, the reference and the plan use the same numbers, and every text exists
  unity-compile-check/          compiles all the Unity scripts against Unity's own libraries, without the editor
Tests/                          runs the tests with .NET:  dotnet test Tests
```

**The asset pipeline.** `Tools/build_assets.sh` rebuilds everything from the plan:

1. It writes the plan's data for Unity and GAMA.
2. It builds the barangay (with a set of pieces for each mission's scene), the operations center and the props
   in three.js, and exports them as glTF (`node export.mjs --preview` also takes pictures of them).
3. Blender paints them from one shared palette, and saves `.blend` and FBX files.
4. It writes the materials and Unity's `.meta` files.

The models carry named boxes and points (see `Tools/world-export/src/tag.js`, the same names as TANGHALAN VR):
walls, the floor to teleport to, the map table, where the panels, the supplies and the pilot's seat go, the
drone's propellers, hooks and camera.

**Where things are.** Everything shares the plan's coordinates: metres, x to the east, z to the south, y up, with
the middle of the barangay at (0, 0) and the river's normal level at y = 0. In Unity a point (x, y, z) is at
(x, y, -z), and in GAMA it is at (x + 240, z + 180).

## What has been checked

| Check | How | Result |
|---|---|---|
| The rules and simulations | `dotnet test Tests`: the ground, the town, the weather, the flight, the autopilot, the falling packages, the boats, each mission flown from the map table, by a pilot (a small program at the sticks) and in Hybrid, the scores, the class's start, the texts | 79 tests pass |
| The drone flies as the reference does | C# against the Python reference (`Tools/flight-reference`), step by step, in six set flights (a calm circuit, a full load in the typhoon's wind, a wind too strong to fly against, a landing too hard, an empty battery, a flood) | pass |
| Unity, GAMA, the reference and the plan agree | `Tools/check_constants.py`: 64 numbers and lists, and the 129 texts the scripts ask for | pass |
| The Unity scripts compile | `Tools/unity-compile-check`: the template and DRONE VR, against Unity 6000.3.8f1's libraries and the packages' sources | pass |
| The models look right | pictures from three.js and renders from the Blender files | checked by eye |
| **Playing in the Unity editor and on a Quest** | | **not done yet** |
| **The GAMA model in GAMA** | GAMA could not be run where this was built; the models were checked against the SIMPLE plugin's source | **not done yet** |

So the first things to do are: open the project in Unity 6000.3.8f1, play the main scene, then open
`GAMA/models/DRONE VR.gaml` in GAMA 2025.06 with the SIMPLE plugin. Please report what does not work.

## Still to do

- Play-test in Unity and on Meta Quest headsets, then fix what comes up: the size and place of panels and text,
  the feel of the sticks, and above all the comfort of the FPV goggles (the vignette, how the view turns with the
  drone). A comfort setting that turns the view in steps may be needed.
- Have a Filipino teacher, and someone who works in DRRM, read the texts: the briefings, the hazards' reports and
  what to do about them, the radio's messages.
- A radio control model in the operator's hands, and recorded sounds.
- More incidents for the same three uses: a landslide after heavy rain, a fire at night with the thermal camera,
  ashfall from a volcano.
- A teacher's view in GAMA of each operator's route and photographs, for the debriefing.

## Credits

DRONE VR was conceptualized by the author of TANGHALAN VR, and built with AI assistance (Claude), on the
architecture of TANGHALAN VR.

Built with the SIMPLE toolchain of project SIMPLE (IRD, UMMISCO; the ACROSS program; co-funded by the European
Union): the SIMPLE Unity Template VR and the GAMA plugin (`project-SIMPLE/simple.toolchain`), GAMA
(gama-platform.org), and Blender (blender.org). The template includes third-party packages and assets with their
own terms: DOTween, websocket-sharp, NativeWebSocket, the Supercyan Character Pack Free Sample, polyextrude, and
Unity's XR samples. The models are built with three.js (threejs.org).

## License

No license has been chosen for DRONE VR's own content and code, so all rights are reserved by the author for now,
as for TANGHALAN VR. The SIMPLE template, the GAMA plugin (LGPL-2.1) and the third-party packages keep their own
licenses.
