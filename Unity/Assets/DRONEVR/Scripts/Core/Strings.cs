// Everything DRONE VR says, in English and in Filipino.
//
// The Filipino was written for DRONE VR: please have a Filipino teacher, and someone who works in disaster
// risk reduction and management, read it over before learners use it.
using System.Collections.Generic;

namespace DroneVR
{
    public enum Lang { En, Fil }

    public static class Strings
    {
        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            // ---- the start ----
            { "start.title", new[] { "DRONE VR", "DRONE VR" } },
            { "start.sub", new[] { "Drones for disaster risk reduction and management", "Mga drone para sa disaster risk reduction and management" } },
            { "start.body", new[] { "You are the drone operator of the barangay's emergency operations center. Learn to fly, then fly three missions: watch for hazards, search for people, and deliver relief.", "Ikaw ang drone operator ng emergency operations center ng barangay. Matutong lumipad, saka lumipad sa tatlong misyon: magbantay sa panganib, maghanap ng tao, at maghatid ng tulong." } },
            { "start.lang", new[] { "Language: English", "Wika: Filipino" } },
            { "start.go", new[] { "Start", "Simulan" } },
            { "start.gama.on", new[] { "Connected to GAMA: the weather, the flood and the rescue boats run in GAMA.", "Nakakonekta sa GAMA: sa GAMA tumatakbo ang panahon, ang baha at ang mga bangkang pansagip." } },
            { "start.gama.off", new[] { "Not connected to GAMA: DRONE VR runs the simulations itself.", "Hindi nakakonekta sa GAMA: ang DRONE VR mismo ang nagpapatakbo ng mga simulation." } },
            { "start.gama.wait", new[] { "Looking for GAMA...", "Hinahanap ang GAMA..." } },
            { "start.howto", new[] { "Point at something and press the trigger. Push the stick forward to jump to a spot, or tilt it to walk.", "Ituro ang isang bagay at pindutin ang trigger. Itulak ang stick pasulong para tumalon sa isang puwesto, o ikiling para maglakad." } },
            { "start.howto.desktop", new[] { "No headset: walk with W A S D, look with the right mouse button, and click to choose.", "Walang headset: maglakad gamit ang W A S D, tumingin gamit ang kanang button ng mouse, at mag-click para pumili." } },

            // ---- the missions ----
            { "mission.lipad.name", new[] { "LIPAD", "LIPAD" } },
            { "mission.lipad.title", new[] { "Flight school", "Paaralan ng paglipad" } },
            { "mission.lipad.brief", new[] { "A calm morning at the barangay hall. Before any real mission, learn how the drone flies.", "Payapang umaga sa barangay hall. Bago ang totoong misyon, alamin kung paano lumilipad ang drone." } },
            { "mission.lipad.task", new[] { "Take off, fly through the six rings in order, then land on the pad.", "Lumipad, dumaan sa anim na singsing ayon sa pagkakasunod, saka lumapag sa pad." } },
            { "mission.lipad.tip", new[] { "Left stick: up, down and turn. Right stick: forward, back and sideways. Let go of the sticks and the drone hovers in place.", "Kaliwang stick: taas, baba at ikot. Kanang stick: abante, atras at patagilid. Bitawan ang mga stick at mananatili sa puwesto ang drone." } },
            { "mission.bantay.name", new[] { "BANTAY", "BANTAY" } },
            { "mission.bantay.title", new[] { "Hazard patrol (environmental safety)", "Pagbabantay sa panganib (kaligtasan ng kapaligiran)" } },
            { "mission.bantay.brief", new[] { "A strong earthquake has just struck. Residents report smoke, a fallen wire, cracks. Nobody should walk into danger to check: send the drone.", "Kalilindol lang nang malakas. May ulat ang mga residente ng usok, bumagsak na kawad, at mga bitak. Walang dapat lumapit sa panganib para tumingin: ipadala ang drone." } },
            { "mission.bantay.task", new[] { "Find the seven hazards near the places reported, and photograph each one from a safe distance.", "Hanapin ang pitong panganib malapit sa mga lugar na iniulat, at kuhanan ng litrato ang bawat isa mula sa ligtas na layo." } },
            { "mission.bantay.tip", new[] { "Hover still, 12 to 28 m away, with the hazard in the middle of the picture. Do not go too close: heat, sparks and falling debris bring drones down.", "Huminto sa ere, 12 hanggang 28 m ang layo, na nasa gitna ng larawan ang panganib. Huwag masyadong lumapit: napapabagsak ng init, kislap at nahuhulog na guho ang drone." } },
            { "mission.sagip.name", new[] { "SAGIP", "SAGIP" } },
            { "mission.sagip.title", new[] { "Search and rescue", "Paghahanap at pagsagip" } },
            { "mission.sagip.brief", new[] { "The typhoon's rain has pushed the river over its banks, and the water is still rising. People are stranded on roofs. The rescue boats are ready, but they do not know where to go.", "Dahil sa ulan ng bagyo, umapaw ang ilog at patuloy pang tumataas ang tubig. May mga taong na-stranded sa mga bubong. Handa ang mga bangkang pansagip, pero hindi nila alam kung saan pupunta." } },
            { "mission.sagip.task", new[] { "Find the eight groups of people and mark each one: a boat goes to every group you mark. One man is in fast water: carry a life ring for him.", "Hanapin ang walong grupo ng tao at markahan ang bawat isa: may bangkang pupunta sa bawat grupong mamarkahan mo. May isang lalaking nasa malakas na agos: magdala ng salbabida para sa kanya." } },
            { "mission.sagip.tip", new[] { "Fly high to see far. Some people are hidden under roofs and trees: come low and switch on the thermal camera to find them.", "Lumipad nang mataas para malayo ang tanaw. May mga taong nakatago sa ilalim ng bubong at puno: bumaba at buksan ang thermal camera para makita sila." } },
            { "mission.hatid.name", new[] { "HATID", "HATID" } },
            { "mission.hatid.title", new[] { "Relief delivery (logistics)", "Paghahatid ng tulong (logistics)" } },
            { "mission.hatid.brief", new[] { "The typhoon has passed, but the wind is still strong, streets are blocked and the bridge is out. Four places have asked for supplies.", "Lumipas na ang bagyo, pero malakas pa ang hangin, barado ang mga kalye at putol ang tulay. Apat na lugar ang humingi ng suplay." } },
            { "mission.hatid.task", new[] { "Load what each place asked for, at most 8 kg on 3 hooks, and drop it on the mark. Come back to the pad for more.", "Ikarga ang hiningi ng bawat lugar, hanggang 8 kg sa 3 kawit, at ihulog sa marka. Bumalik sa pad para sa susunod na karga." } },
            { "mission.hatid.tip", new[] { "Drop from below 4 m, or the package breaks. In this wind a falling package drifts: aim a little upwind. A heavy load drains the battery faster.", "Ihulog mula sa mababa sa 4 m, kung hindi ay masisira ang pakete. Sa hanging ito, inaanod ang nahuhulog na pakete: umasinta nang kaunti pasalungat sa hangin. Mas mabilis maubos ang baterya kapag mabigat ang karga." } },

            // ---- the three ways to fly ----
            { "mode.desk", new[] { "Command Desk", "Command Desk" } },
            { "mode.desk.about", new[] { "Stay at the map table. Draw the route, load the drone, launch. The autopilot flies.", "Manatili sa mesa ng mapa. Iguhit ang ruta, kargahan ang drone, paliparin. Ang autopilot ang lilipad." } },
            { "mode.pilot", new[] { "FPV Pilot", "FPV Pilot" } },
            { "mode.pilot.about", new[] { "Put on the goggles and fly the drone yourself, from take-off to landing.", "Isuot ang goggles at ikaw mismo ang magpalipad ng drone, mula paglipad hanggang paglapag." } },
            { "mode.hybrid", new[] { "Hybrid", "Hybrid" } },
            { "mode.hybrid.about", new[] { "Draw the route on the table, and take the sticks yourself for the risky parts.", "Iguhit ang ruta sa mesa, at ikaw ang humawak ng stick sa mga delikadong bahagi." } },

            // ---- the mission board and the briefing ----
            { "board.title", new[] { "Mission board", "Talaan ng misyon" } },
            { "board.pick", new[] { "Point at a mission and press the trigger.", "Ituro ang isang misyon at pindutin ang trigger." } },
            { "board.notflown", new[] { "Not flown yet", "Hindi pa nalilipad" } },
            { "board.best", new[] { "Best: {0}, {1} points", "Pinakamahusay: {0}, {1} puntos" } },
            { "board.first", new[] { "Start here", "Dito magsimula" } },
            { "stars", new[] { "{0} of 3 stars", "{0} sa 3 bituin" } },
            { "brief.weather", new[] { "Wind: {0} m/s from the {1} ({2}).", "Hangin: {0} m/s mula sa {1} ({2})." } },
            { "brief.flood", new[] { "Flood: {0} m above the river's normal level.", "Baha: {0} m mula sa karaniwang taas ng ilog." } },
            { "brief.flood.rising", new[] { "Flood: {0} m above the river's normal level, and rising.", "Baha: {0} m mula sa karaniwang taas ng ilog, at tumataas pa." } },
            { "brief.flood.falling", new[] { "Flood: {0} m above the river's normal level, and going down.", "Baha: {0} m mula sa karaniwang taas ng ilog, at humuhupa na." } },
            { "brief.minutes", new[] { "Time: {0} minutes.", "Oras: {0} minuto." } },
            { "brief.mode", new[] { "How will you fly it?", "Paano mo ito lilipadin?" } },
            { "brief.begin", new[] { "Begin the mission", "Simulan ang misyon" } },
            { "brief.back", new[] { "Back", "Bumalik" } },
            { "brief.wait.class", new[] { "Waiting for your classmates: {0} of {1} are ready.", "Hinihintay ang mga kaklase mo: {0} sa {1} ang handa na." } },
            { "brief.ready", new[] { "Ready. The mission starts for the whole class in a moment.", "Handa na. Magsisimula ang misyon para sa buong klase maya-maya." } },

            // ---- the console by the map table ----
            { "console.route.none", new[] { "Point at the map and press the trigger to put a waypoint.", "Ituro ang mapa at pindutin ang trigger para maglagay ng waypoint." } },
            { "console.route", new[] { "Route: {0} waypoints, {1} m, about {2}. Battery needed: {3}%.", "Ruta: {0} waypoint, {1} m, mga {2}. Bateryang kailangan: {3}%." } },
            { "console.route.toofar", new[] { "Too far for one battery. Make the route shorter.", "Masyadong malayo para sa isang baterya. Paikliin ang ruta." } },
            { "console.route.pilot", new[] { "FPV Pilot: no route. Go to the pilot's seat and fly.", "FPV Pilot: walang ruta. Pumunta sa upuan ng piloto at lumipad." } },
            { "console.alt", new[] { "Waypoint height: {0} m", "Taas ng waypoint: {0} m" } },
            { "console.cargo", new[] { "On the hooks: {0} ({1} of {2} kg)", "Nasa mga kawit: {0} ({1} sa {2} kg)" } },
            { "console.cargo.none", new[] { "Nothing on the hooks.", "Walang nakakabit sa mga kawit." } },
            { "console.load.how", new[] { "Grab a crate from the shelf and bring it to the drone on the dock. Or point at it and press the trigger.", "Kunin ang kahon sa estante at dalhin sa drone na nasa dock. O ituro ito at pindutin ang trigger." } },
            { "console.status", new[] { "Battery {0}% · Height {1} m · Speed {2} m/s", "Baterya {0}% · Taas {1} m · Bilis {2} m/s" } },
            { "console.time", new[] { "Time left: {0}", "Natitirang oras: {0}" } },
            { "console.wind", new[] { "Wind: {0} m/s from the {1}", "Hangin: {0} m/s mula sa {1}" } },
            { "console.flood", new[] { "Flood: {0} m", "Baha: {0} m" } },
            { "console.tasks.lipad", new[] { "Rings: {0} of {1}", "Singsing: {0} sa {1}" } },
            { "console.tasks.bantay", new[] { "Hazards photographed: {0} of {1}", "Panganib na nakuhanan: {0} sa {1}" } },
            { "console.tasks.sagip", new[] { "Groups found: {0} of {1} · aboard a boat: {2}", "Grupong natagpuan: {0} sa {1} · nakasakay na sa bangka: {2}" } },
            { "console.tasks.hatid", new[] { "Needs met: {0} of {1}", "Pangangailangang natugunan: {0} sa {1}" } },
            { "console.state.pad", new[] { "The drone is on the pad.", "Nasa pad ang drone." } },
            { "console.state.ground", new[] { "The drone is on the ground, away from the pad.", "Nasa lupa ang drone, malayo sa pad." } },
            { "console.state.auto", new[] { "Autopilot: flying to waypoint {0} of {1}.", "Autopilot: lumilipad patungo sa waypoint {0} sa {1}." } },
            { "console.state.drop", new[] { "Autopilot: coming down to drop.", "Autopilot: bumababa para maghulog." } },
            { "console.state.home", new[] { "Autopilot: coming home.", "Autopilot: pauwi na." } },
            { "console.state.homelow", new[] { "Autopilot: battery low, coming home.", "Autopilot: mababa ang baterya, pauwi na." } },
            { "console.state.wait", new[] { "Autopilot: hovering. Put a waypoint, or call it home.", "Autopilot: nakahinto sa ere. Maglagay ng waypoint, o pauwiin ito." } },
            { "console.state.manual", new[] { "The pilot has the sticks.", "Hawak ng piloto ang mga stick." } },
            { "console.state.lost", new[] { "Drone lost. A spare is coming to the pad.", "Nawala ang drone. May paparating na reserba sa pad." } },
            { "need.line", new[] { "{0} needs: {1}", "Kailangan ng {0}: {1}" } },
            { "need.done", new[] { "{0}: everything delivered", "{0}: naihatid na lahat" } },
            { "btn.higher", new[] { "Higher", "Itaas" } },
            { "btn.lower", new[] { "Lower", "Ibaba" } },
            { "btn.undo", new[] { "Undo", "Bawiin" } },
            { "btn.clear", new[] { "Clear", "Burahin" } },
            { "btn.launch", new[] { "Launch", "Paliparin" } },
            { "btn.recall", new[] { "Come home", "Pauwiin" } },
            { "btn.take", new[] { "Take the sticks", "Hawakan ang stick" } },
            { "btn.give", new[] { "Hand back", "Ibalik sa autopilot" } },
            { "btn.fly", new[] { "Fly (FPV)", "Lumipad (FPV)" } },
            { "btn.end", new[] { "End the mission", "Tapusin ang misyon" } },
            { "btn.again", new[] { "Fly it again", "Liparin muli" } },
            { "btn.board", new[] { "Back to the board", "Bumalik sa talaan" } },
            { "btn.close", new[] { "Close", "Isara" } },
            { "btn.lang", new[] { "Filipino", "English" } },
            { "btn.view.goggles", new[] { "View: goggles", "Tanaw: goggles" } },
            { "btn.view.screen", new[] { "View: screen", "Tanaw: screen" } },
            { "btn.summary", new[] { "Operations summary", "Buod ng operasyon" } },

            // ---- flying ----
            { "hud.controls", new[] { "Left stick: up, down, turn · Right stick: forward, back, sideways · Trigger: {0} · A: back to the room", "Kaliwang stick: taas, baba, ikot · Kanang stick: abante, atras, patagilid · Trigger: {0} · A: balik sa silid" } },
            { "hud.controls.hybrid", new[] { "X: hand back to the autopilot · Y: come home", "X: ibalik sa autopilot · Y: pauwiin" } },
            { "hud.controls.desktop", new[] { "W S: forward, back · A D: sideways · Q E: turn · R F: up, down · Click: {0} · T: thermal · Tab: back to the room", "W S: abante, atras · A D: patagilid · Q E: ikot · R F: taas, baba · Click: {0} · T: thermal · Tab: balik sa silid" } },
            { "tool.none", new[] { "(nothing)", "(wala)" } },
            { "tool.photo", new[] { "photograph", "litrato" } },
            { "tool.mark", new[] { "mark people · grip: drop the life ring · B: thermal camera", "markahan ang tao · grip: ihulog ang salbabida · B: thermal camera" } },
            { "tool.drop", new[] { "drop a package", "ihulog ang pakete" } },
            { "hud.battery", new[] { "BATTERY {0}%", "BATERYA {0}%" } },
            { "hud.height", new[] { "HEIGHT {0} m", "TAAS {0} m" } },
            { "hud.speed", new[] { "SPEED {0} m/s", "BILIS {0} m/s" } },
            { "hud.wind", new[] { "WIND {0} m/s from {1}", "HANGIN {0} m/s mula {1}" } },
            { "hud.home", new[] { "PAD {0} m", "PAD {0} m" } },
            { "hud.time", new[] { "TIME {0}", "ORAS {0}" } },
            { "hud.hooks", new[] { "HOOKS: {0}", "KAWIT: {0}" } },
            { "hud.thermal", new[] { "THERMAL CAMERA", "THERMAL CAMERA" } },
            { "hud.auto", new[] { "AUTOPILOT", "AUTOPILOT" } },
            { "hud.next.gate", new[] { "Ring {0}: {1} m", "Singsing {0}: {1} m" } },
            { "hud.next.pad", new[] { "Land on the pad: {0} m", "Lumapag sa pad: {0} m" } },
            { "hud.next.site", new[] { "{0}: {1} m", "{0}: {1} m" } },
            { "hud.drop.height", new[] { "Height above the mark: {0} m", "Taas mula sa marka: {0} m" } },
            { "hud.drop.high", new[] { "Too high to drop: {0} m", "Masyadong mataas para maghulog: {0} m" } },
            { "hud.ceiling", new[] { "Ceiling: 120 m (the 400 ft of the CAAP's rules).", "Hangganan ng taas: 120 m (ang 400 ft sa patakaran ng CAAP)." } },
            { "pilot.seat", new[] { "Pilot's seat", "Upuan ng piloto" } },
            { "pilot.cannot.desk", new[] { "Command Desk: the autopilot flies. Choose Hybrid or FPV Pilot to take the sticks.", "Command Desk: ang autopilot ang lumilipad. Piliin ang Hybrid o FPV Pilot para humawak ng stick." } },
            { "pilot.took", new[] { "You have the sticks.", "Hawak mo na ang mga stick." } },
            { "pilot.gave", new[] { "The autopilot has the drone.", "Nasa autopilot na ang drone." } },

            // ---- what the radio says ----
            { "wind.calm", new[] { "calm", "payapa" } },
            { "wind.light", new[] { "a light breeze", "mahinang hangin" } },
            { "wind.moderate", new[] { "a moderate breeze", "katamtamang hangin" } },
            { "wind.fresh", new[] { "a fresh breeze", "malakas-lakas na hangin" } },
            { "wind.strong", new[] { "a strong wind", "malakas na hangin" } },
            { "wind.gale", new[] { "near gale", "halos unos" } },
            { "wind.raised", new[] { "The wind is picking up.", "Lumalakas ang hangin." } },
            { "dir.N", new[] { "north", "hilaga" } },
            { "dir.NE", new[] { "north-east", "hilagang-silangan" } },
            { "dir.E", new[] { "east", "silangan" } },
            { "dir.SE", new[] { "south-east", "timog-silangan" } },
            { "dir.S", new[] { "south", "timog" } },
            { "dir.SW", new[] { "south-west", "timog-kanluran" } },
            { "dir.W", new[] { "west", "kanluran" } },
            { "dir.NW", new[] { "north-west", "hilagang-kanluran" } },
            { "crash.building", new[] { "The drone hit a building.", "Bumangga ang drone sa gusali." } },
            { "crash.roof", new[] { "The drone hit a roof.", "Bumangga ang drone sa bubong." } },
            { "crash.tower", new[] { "The drone hit a tower.", "Bumangga ang drone sa tore." } },
            { "crash.tree", new[] { "The drone flew into a tree.", "Sumabit ang drone sa puno." } },
            { "crash.pole", new[] { "The drone hit an electric pole.", "Bumangga ang drone sa poste ng kuryente." } },
            { "crash.bridge", new[] { "The drone hit the bridge.", "Bumangga ang drone sa tulay." } },
            { "crash.water", new[] { "The drone went down in the water. It is lost.", "Bumagsak ang drone sa tubig. Nawala na ito." } },
            { "crash.hard", new[] { "The drone came down too hard and broke.", "Napalakas ang bagsak ng drone at nasira ito." } },
            { "crash.spare", new[] { "A spare drone is on the pad.", "May reserbang drone na sa pad." } },
            { "battery.low", new[] { "Battery at 30%. Time to come home.", "30% na lang ang baterya. Oras nang umuwi." } },
            { "battery.critical", new[] { "Battery almost empty: the drone is coming down by itself. Steer it to a dry, open spot.", "Halos ubos na ang baterya: kusang bumababa ang drone. Igiya ito sa tuyo at bukas na lugar." } },
            { "battery.swapped", new[] { "Fresh battery. Ready to fly.", "Bagong baterya. Handa nang lumipad." } },
            { "mission.complete", new[] { "Everything done, and the drone is back on the pad. Mission complete!", "Tapos na ang lahat, at nakabalik na sa pad ang drone. Tapos ang misyon!" } },
            { "mission.timeup", new[] { "Time is up.", "Ubos na ang oras." } },
            { "mission.go", new[] { "{0}: the mission has begun.", "{0}: nagsimula na ang misyon." } },
            { "load.pad", new[] { "Supplies can be changed only while the drone stands on the pad.", "Mapapalitan lang ang karga habang nasa pad ang drone." } },
            { "load.hooks", new[] { "All three hooks are taken.", "May laman na ang tatlong kawit." } },
            { "load.heavy", new[] { "Too heavy: the drone lifts 8 kg.", "Masyadong mabigat: 8 kg lang ang kayang buhatin ng drone." } },
            { "load.on", new[] { "Loaded: {0}.", "Naikarga: {0}." } },
            { "load.off", new[] { "Taken off: {0}.", "Inalis: {0}." } },
            { "drop.empty", new[] { "Nothing on the hooks.", "Walang nakakabit sa mga kawit." } },
            { "drop.landed", new[] { "The drone is on the ground.", "Nasa lupa ang drone." } },
            { "drop.sunk", new[] { "The {0} fell in the water. It is lost.", "Nahulog sa tubig ang {0}. Nawala na ito." } },
            { "drop.broken", new[] { "The {0} fell from too high and broke.", "Nahulog mula sa sobrang taas ang {0} at nasira." } },
            { "drop.missed", new[] { "The {0} landed outside the mark.", "Lumapag sa labas ng marka ang {0}." } },
            { "drop.notneeded", new[] { "The {0} landed at {1}, which did not ask for it.", "Lumapag ang {0} sa {1}, na hindi naman humingi nito." } },
            { "drop.delivered", new[] { "Delivered: {0} to {1}, {2} m from the mark.", "Naihatid: {0} sa {1}, {2} m mula sa marka." } },
            { "lipad.gate", new[] { "Ring {0} of {1}. On to the next.", "Singsing {0} sa {1}. Sa susunod naman." } },
            { "lipad.gates.done", new[] { "All the rings! Now land on the pad.", "Lahat ng singsing! Ngayon, lumapag sa pad." } },
            { "photo.nothing", new[] { "No hazard in the picture. Come within 45 m and put it in the middle.", "Walang panganib sa larawan. Lumapit sa loob ng 45 m at ilagay ito sa gitna." } },
            { "photo.nobetter", new[] { "{0}: the picture you already have is better.", "{0}: mas maganda ang larawang hawak mo na." } },
            { "photo.fine", new[] { "{0}: a clear photograph ({1}%).", "{0}: malinaw na litrato ({1}%)." } },
            { "photo.enough", new[] { "{0}: photographed ({1}%). Closer and steadier would be clearer.", "{0}: nakuhanan ({1}%). Mas lilinaw kung mas malapit at mas panatag." } },
            { "photo.poor", new[] { "{0}: too blurred or too far to use ({1}%). Try again.", "{0}: masyadong malabo o malayo para magamit ({1}%). Subukan muli." } },
            { "bantay.alldone", new[] { "All seven hazards are photographed. Bring the drone home.", "Nakuhanan na ang pitong panganib. Iuwi na ang drone." } },
            { "bantay.tooclose", new[] { "Too close to {0}! Keep a safe distance.", "Masyadong malapit sa {0}! Manatili sa ligtas na layo." } },
            { "sagip.found", new[] { "Marked: {0} ({1} people). A boat will be there in {2}.", "Namarkahan: {0} ({1} tao). May bangkang darating sa loob ng {2}." } },
            { "sagip.found.class", new[] { "A classmate marked {0}.", "Namarkahan ng kaklase mo ang {0}." } },
            { "sagip.needsring", new[] { "Marked: {0}. He is in fast water: drop him a life ring, and a boat can pull him in.", "Namarkahan: {0}. Nasa malakas na agos siya: hulugan siya ng salbabida para mahila siya ng bangka." } },
            { "sagip.alldone", new[] { "All eight groups are marked. Bring the drone home; the boats do the rest.", "Namarkahan na ang walong grupo. Iuwi na ang drone; ang mga bangka na ang bahala." } },
            { "sagip.trythermal", new[] { "Something warm is near, out of sight. Switch on the thermal camera.", "May mainit sa malapit na hindi tanaw. Buksan ang thermal camera." } },
            { "sagip.nothing", new[] { "Nobody in the middle of the picture.", "Walang tao sa gitna ng larawan." } },
            { "sagip.late", new[] { "{0} could not wait any longer. Every minute counts.", "Hindi na nakapaghintay ang {0}. Mahalaga ang bawat minuto." } },
            { "sagip.ring", new[] { "The life ring reached {0}. A boat will pull him in, in {1}.", "Naabot ng {0} ang salbabida. May bangkang hihila sa kanya sa loob ng {1}." } },
            { "sagip.ring.unmarked", new[] { "The life ring reached {0}. Mark him, so a boat knows where to go.", "Naabot ng {0} ang salbabida. Markahan siya, para malaman ng bangka kung saan pupunta." } },
            { "sagip.ring.missed", new[] { "The life ring landed too far from anyone who needs it.", "Masyadong malayo ang binagsakan ng salbabida sa nangangailangan nito." } },
            { "hatid.alldone", new[] { "Every place has what it asked for. Bring the drone home.", "Nakuha na ng bawat lugar ang hiningi nito. Iuwi na ang drone." } },

            // ---- names ----
            { "hazard.fire", new[] { "the fire in the fallen house", "ang sunog sa gumuhong bahay" } },
            { "hazard.wire", new[] { "the fallen power line", "ang bumagsak na kawad ng kuryente" } },
            { "hazard.bridge", new[] { "the crack in the bridge", "ang bitak sa tulay" } },
            { "hazard.slide", new[] { "the landslide", "ang pagguho ng lupa" } },
            { "hazard.spill", new[] { "the fuel spill", "ang natapong gasolina" } },
            { "hazard.tank", new[] { "the leaning water tower", "ang nakahilig na tangke ng tubig" } },
            { "hazard.dam", new[] { "the debris blocking the creek", "ang guhong bumabara sa sapa" } },
            { "hazard.fire.act", new[] { "Report: call the fire brigade, and keep people away from the house.", "Ulat: tawagin ang bumbero, at ilayo ang mga tao sa bahay." } },
            { "hazard.wire.act", new[] { "Report: tell the electric cooperative, and let nobody near the wire.", "Ulat: ipaalam sa electric cooperative, at huwag palapitin ang sinuman sa kawad." } },
            { "hazard.bridge.act", new[] { "Report: close the bridge until an engineer has checked it.", "Ulat: isara ang tulay hangga't hindi nasusuri ng inhinyero." } },
            { "hazard.slide.act", new[] { "Report: move the families below the slope, before the next rain or aftershock.", "Ulat: ilikas ang mga pamilya sa ibaba ng dalisdis, bago ang susunod na ulan o aftershock." } },
            { "hazard.spill.act", new[] { "Report: no flames near the station; keep the fuel out of the creek.", "Ulat: bawal ang apoy malapit sa gasolinahan; huwag paabutin sa sapa ang gasolina." } },
            { "hazard.tank.act", new[] { "Report: clear the houses beside the tower, and find another source of clean water.", "Ulat: palikasin ang mga bahay sa tabi ng tore, at maghanap ng ibang mapagkukunan ng malinis na tubig." } },
            { "hazard.dam.act", new[] { "Report: clear the creek before it rains, or the street will flood.", "Ulat: linisin ang sapa bago umulan, kung hindi ay babahain ang kalye." } },
            { "report.title", new[] { "Reported by residents", "Iniulat ng mga residente" } },
            { "survivor.roof1", new[] { "the family on the roof by the river", "ang pamilya sa bubong sa tabing-ilog" } },
            { "survivor.market", new[] { "the two on the market's roof", "ang dalawa sa bubong ng palengke" } },
            { "survivor.roof2", new[] { "the mother and child on the roof by the creek", "ang mag-ina sa bubong sa tabi ng sapa" } },
            { "survivor.tree", new[] { "the man in the coconut tree", "ang lalaki sa puno ng niyog" } },
            { "survivor.jeep", new[] { "the two on the jeepney", "ang dalawa sa ibabaw ng jeepney" } },
            { "survivor.grove", new[] { "the family on the raft under the trees", "ang pamilya sa balsa sa ilalim ng mga puno" } },
            { "survivor.gym", new[] { "the people in the covered court", "ang mga tao sa covered court" } },
            { "survivor.sitio", new[] { "the couple on the roof in the sitio", "ang mag-asawa sa bubong sa sitio" } },
            { "site.evac", new[] { "the evacuation center", "ang evacuation center" } },
            { "site.sitio", new[] { "the sitio across the river", "ang sitio sa kabila ng ilog" } },
            { "site.health", new[] { "the health center", "ang health center" } },
            { "site.roof", new[] { "the family on the roof", "ang pamilya sa bubong" } },
            { "supply.water", new[] { "water", "tubig" } },
            { "supply.food", new[] { "food packs", "food pack" } },
            { "supply.medicine", new[] { "medicines", "gamot" } },
            { "supply.firstaid", new[] { "first aid kit", "first aid kit" } },
            { "supply.radio", new[] { "radio", "radyo" } },
            { "supply.ring", new[] { "life ring", "salbabida" } },
            { "supply.kg", new[] { "{0} · {1} kg", "{0} · {1} kg" } },
            { "place.hall", new[] { "Barangay hall · Operations center", "Barangay hall · Operations center" } },
            { "place.eoc", new[] { "BDRRMC Emergency Operations Center", "BDRRMC Emergency Operations Center" } },
            { "place.table", new[] { "Map table", "Mesa ng mapa" } },
            { "place.dock", new[] { "Loading dock", "Loading dock" } },
            { "place.shelf", new[] { "Supplies", "Mga suplay" } },
            { "class.drones", new[] { "Drones in the air: {0}", "Mga drone sa ere: {0}" } },

            // ---- the mission report ----
            { "results.title", new[] { "{0}: mission report", "{0}: ulat ng misyon" } },
            { "results.score", new[] { "{0} · {1} points of 100", "{0} · {1} puntos sa 100" } },
            { "results.parts", new[] { "The task {0}% · Safe flying {1}% · Time {2}%", "Ang gawain {0}% · Ligtas na paglipad {1}% · Oras {2}%" } },
            { "end.crashes", new[] { "Drones lost: {0}.", "Mga drone na nawala: {0}." } },
            { "end.strikes", new[] { "Flew too close to a hazard: {0} times.", "Lumipad nang masyadong malapit sa panganib: {0} beses." } },
            { "end.home", new[] { "The drone is back on the pad.", "Nakabalik sa pad ang drone." } },
            { "end.nothome", new[] { "The drone did not come back to the pad.", "Hindi nakabalik sa pad ang drone." } },
            { "end.time", new[] { "Finished in {0} of {1}.", "Natapos sa {0} ng {1}." } },
            { "end.unfinished", new[] { "The mission ended before everything was done.", "Natapos ang misyon bago magawa ang lahat." } },
            { "end.lipad.gates", new[] { "Rings flown through: {0} of {1}.", "Singsing na nadaanan: {0} sa {1}." } },
            { "end.bantay.count", new[] { "Hazards photographed: {0} of {1}, in {2} pictures.", "Panganib na nakuhanan: {0} sa {1}, sa {2} litrato." } },
            { "end.bantay.missed", new[] { "Not photographed: {0}.", "Hindi nakuhanan: {0}." } },
            { "end.sagip.count", new[] { "Groups found: {0} of {1}. People aboard a boat in time: {2} of {3}.", "Grupong natagpuan: {0} sa {1}. Taong naisakay sa bangka sa tamang oras: {2} sa {3}." } },
            { "end.sagip.missed", new[] { "Never found: {0} ({1} people).", "Hindi natagpuan: {0} ({1} tao)." } },
            { "end.sagip.late", new[] { "Found too late: {0}.", "Huli nang natagpuan: {0}." } },
            { "end.sagip.noring", new[] { "Found, but no life ring reached him: {0}.", "Natagpuan, pero walang salbabidang nakaabot sa kanya: {0}." } },
            { "end.hatid.count", new[] { "Needs met: {0} of {1}.", "Pangangailangang natugunan: {0} sa {1}." } },
            { "end.hatid.waiting", new[] { "Still waiting: {0} ({1} more).", "Naghihintay pa: {0} ({1} pa)." } },
            { "end.hatid.lost", new[] { "Packages lost: {0}.", "Mga paketeng nasayang: {0}." } },
            { "sum.title", new[] { "Operations summary", "Buod ng operasyon" } },
            { "sum.line", new[] { "{0}: {1}, {2} points ({3})", "{0}: {1}, {2} puntos ({3})" } },
            { "sum.notflown", new[] { "{0}: not flown yet", "{0}: hindi pa nalilipad" } },
            { "sum.total", new[] { "Stars: {0} of {1}", "Mga bituin: {0} sa {1}" } },
            { "sum.all", new[] { "You have flown the three uses of drones in a disaster: watching for hazards, finding people, and bringing relief. A drone goes where it is not safe for people to go. The decisions stay with people.", "Nalipad mo na ang tatlong gamit ng drone sa sakuna: pagbabantay sa panganib, paghahanap ng tao, at paghahatid ng tulong. Nakakapunta ang drone sa lugar na hindi ligtas para sa tao. Tao pa rin ang nagpapasya." } },
            { "sum.more", new[] { "Fly BANTAY, SAGIP and HATID to see the three uses of drones in a disaster.", "Liparin ang BANTAY, SAGIP at HATID para makita ang tatlong gamit ng drone sa sakuna." } },
        };

        public static bool Has(string key) { return T.ContainsKey(key); }

        public static string Get(string key, Lang lang)
        {
            string[] v;
            if (!T.TryGetValue(key, out v)) return key;
            return v[lang == Lang.Fil ? 1 : 0];
        }

        public static string Format(string key, Lang lang, params object[] args)
        {
            return args == null || args.Length == 0 ? Get(key, lang) : string.Format(Get(key, lang), args);
        }

        /// <summary>Puts names in the place of keys: "hazard.fire" becomes "the fire in the fallen house".</summary>
        public static object[] Names(object[] args, Lang lang)
        {
            if (args == null) return null;
            var o = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var s = args[i] as string;
                o[i] = s != null && T.ContainsKey(s) ? Get(s, lang) : args[i];
            }
            return o;
        }

        /// <summary>Minutes and seconds, as 3:07.</summary>
        public static string Clock(double seconds)
        {
            int s = (int)System.Math.Round(System.Math.Max(0, seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public static string Stars(int n, Lang lang) { return Format("stars", lang, n); }
        /// <summary>For lines that already carry their language (the summary).</summary>
        public static string Stars(int n) { return n + "/3"; }

        /// <summary>The first letter as a capital: names start sentences on the boards.</summary>
        public static string Capital(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        /// <summary>The name of the direction a heading points to: "north-west".</summary>
        public static string Direction(double heading, Lang lang) { return Get("dir." + Compass.Point(heading), lang); }

        /// <summary>Every key, for the tests.</summary>
        public static IEnumerable<string> Keys { get { return T.Keys; } }
        public static string[] Both(string key) { return T[key]; }
    }
}
