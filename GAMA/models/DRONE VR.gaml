/**
* Name: DRONE VR
* Links the DRONE model (DRONE.gaml) to the DRONE VR app in Unity, through the SIMPLE plugin and the SIMPLE
* WebPlatform. Each headset is a drone operator in the barangay's operations center. The drones are flown on
* the headsets; GAMA runs what they fly in and what they work for: the wind and the flood, the people to find,
* the rescue boats, the hazards and the sites, for the whole class at once (Unity:
* Assets/DRONEVR/Scripts/Runtime/DroneSimulationManager.cs and Scenario.cs).
*
* A class flies one incident together. Each headset tells GAMA when its operator is ready. When every headset
* is ready, GAMA waits a few seconds, then starts the mission for all of them: one wind, one flood, the same
* people to find, the same boats. The class flies the mission its first ready headset chose. A group one drone
* finds is found for everybody, and every headset sees the others' drones. The teacher can start a mission at
* any time, end it, and raise or calm the wind.
*
* What GAMA sends to Unity (send_message), each a map whose "dronevr" entry says what it is:
*   class     every two seconds: ready, how many headsets are ready; players, how many headsets; mission, the
*             mission the class is getting ready for or flying ("" if none)
*   start     the mission begins: mission, its name; t, the milliseconds it has already run
*   busy      the class is well into a mission: this headset flies its own
*   end       the mission is over
*   weather   t: milliseconds of the mission; wind and mean: the wind's speed now and its mean, cm/s at 60 m;
*             from: where it blows from, tenths of a degree; level: the flood's level, mm
*   targets   mission, its name; s: how far each thing to do has come. BANTAY: the best photograph of each hazard,
*             percent. SAGIP: for each group 1 found + 2 a life ring reached it + 4 aboard a boat. HATID: for each
*             site 1 for its first need met + 2 for its second
*   boats     p: for each boat x, z (decimetres), heading (degrees), what it is doing (0 to 4), the group it goes to (or -1)
*   drones    ids: the headsets flying; p: for each x, y, z (decimetres), heading (degrees), battery (percent)
* The weather, the targets, the boats and the drones go to the headsets in the mission; the count to every headset.
* What Unity asks GAMA to do (actions of unity_linker, every argument a string):
*   dr_hello(id, lang), dr_event(id, kind, detail), dr_ready(id, mission, mode), dr_leave(id),
*   dr_drone(id, x, y, z, yaw, battery, aloft), dr_gate(id, n), dr_photo(id, hazard, quality),
*   dr_found(id, who), dr_ring(id, who), dr_delivered(id, site, need),
*   dr_result(id, mission, stars, score, seconds)
* Everything the operators do is also written to results/dronevr_log.csv (with the headset's id only).
* Author: DRONE VR
* Tags: Unity, SIMPLE, drone, search and rescue, logistics
*/
model DRONE_VR

import "DRONE.gaml"

global {
	string log_file <- "../results/dronevr_log.csv";

	// ---------- a mission for the class (Unity: MissionStart) ----------
	// seconds between the last headset being ready and the start
	float start_delay <- 3.0;
	// after the first headset is ready, the mission starts without those who are not, this many seconds later
	float ready_patience <- 30.0;
	// a headset that is ready within this many seconds of the start joins the mission
	float join_window <- 20.0;
	// seconds after a mission's time is up before GAMA ends it (the headsets end it themselves first)
	float end_grace <- 5.0;
	// seconds between two counts of the class, two lists of targets and two lists of drones sent to the headsets
	float count_every <- 2.0;
	float targets_every <- 1.0;
	float drones_every <- 0.25;
}

species unity_linker parent: abstract_unity_linker {
	string player_species <- string(unity_player);
	int min_num_players <- 0;
	int max_num_players <- 6;
	// the operators stand in the operations center under the barangay hall: DRONE VR (-172, -134) and 6 m below
	// the river's normal level, so GAMA (68, 46, -6)
	list<point> init_locations <- [{68.0, 49.0, -6.0}, {66.5, 49.0, -6.0}, {69.5, 49.0, -6.0}, {68.0, 50.2, -6.0}, {66.5, 50.2, -6.0}, {69.5, 50.2, -6.0}];
	// DRONE VR sends its own messages, not the template's geometries
	bool do_send_world <- false;

	// the mission the class is getting ready for (its place in mission_names), or -1
	int class_mission <- -1;
	float ready_timer <- 0.0;
	float start_timer <- 0.0;
	float count_timer <- 0.0;
	float targets_timer <- 0.0;
	float drones_timer <- 0.0;

	action tell_players (map<string, unknown> mes) {
		if (!empty(unity_player)) {
			do send_message players: unity_player as list mes: mes;
		}
	}

	// sends a message to some of the headsets only
	action tell_some (list<unity_player> who, map<string, unknown> mes) {
		list<unity_player> there <- who where (each != nil and !dead(each));
		if (!empty(there)) {
			do send_message players: there mes: mes;
		}
	}

	action write_log (string id, string kind, string detail) {
		save [cycle, string(#now), id, kind, detail] to: log_file format: "csv" rewrite: false header: false;
	}

	list<unity_player> flying {
		return unity_player where (each.in_mission);
	}

	// ---------- what Unity asks ----------

	action dr_hello (string id, string lang) {
		unity_player p <- unity_player first_with (each.name = id);
		if (p != nil) {
			p.ready <- false;
			p.in_mission <- false;
			p.drone_flying <- false;
		}
		do write_log(id, "hello", lang);
	}

	action dr_event (string id, string kind, string detail) {
		do write_log(id, kind, detail);
	}

	// the operator pressed Begin: ready to fly. The class flies the mission its first ready headset chose.
	action dr_ready (string id, string mission_name, string mode) {
		unity_player p <- unity_player first_with (each.name = id);
		int m <- mission_names index_of mission_name;
		if (p != nil and m >= 0) {
			p.mode <- mode;
			p.in_mission <- false;
			if (mission_running and mission_time > join_window) {
				// the class is well into a mission: this headset flies its own, and GAMA sends it nothing
				p.ready <- false;
				do tell_some([p], ["dronevr"::"busy"]);
				do write_log(id, "flies_alone", mission_name);
			} else {
				p.ready <- true;
				if (!mission_running and class_mission < 0) {
					class_mission <- m;
					ready_timer <- 0.0;
					start_timer <- 0.0;
				}
				do write_log(id, "ready", mission_name + " " + mode);
			}
		}
	}

	// the operator went back to the board, or the mission is over on that headset
	action dr_leave (string id) {
		unity_player p <- unity_player first_with (each.name = id);
		if (p != nil) {
			p.ready <- false;
			p.in_mission <- false;
			p.drone_flying <- false;
		}
		do write_log(id, "leave", "");
	}

	// where a headset's drone is (twice a second): x, y, z in decimetres, DRONE VR's coordinates
	action dr_drone (string id, string x, string y, string z, string yaw, string battery, string aloft) {
		unity_player p <- unity_player first_with (each.name = id);
		if (p != nil) {
			p.drone_at <- {int(x) / 10.0, int(z) / 10.0, int(y) / 10.0};
			p.drone_heading <- float(yaw);
			p.drone_battery <- int(battery);
			p.drone_flying <- (aloft = "1");
		}
	}

	action dr_gate (string id, string n) {
		do write_log(id, "ring", n);
	}

	// a photograph of a hazard: the class keeps its best one
	action dr_photo (string id, string hazard, string quality) {
		int h <- int(hazard);
		float q <- int(quality) / 100.0;
		if (mission_running and h >= 0 and h < length(hazard_quality) and q > hazard_quality[h]) {
			hazard_quality[h] <- q;
			targets_timer <- 0.0;
		}
		do write_log(id, "photo", ((h >= 0 and h < length(hazard_names)) ? hazard_names[h] : hazard) + " " + quality + "%");
	}

	// a drone marked a group of people: a boat goes to them
	action dr_found (string id, string who) {
		int s <- int(who);
		if (world.searching() and s >= 0 and s < length(survivor_names)) {
			ask world { do group_found(s, id); }
			targets_timer <- 0.0;
		}
		do write_log(id, "found", (s >= 0 and s < length(survivor_names)) ? survivor_names[s] : who);
	}

	// a life ring reached a group that needed one
	action dr_ring (string id, string who) {
		int s <- int(who);
		if (world.searching() and s >= 0 and s < length(survivor_names)) {
			ask world { do group_ringed(s); }
			targets_timer <- 0.0;
		}
		do write_log(id, "ring_reached", (s >= 0 and s < length(survivor_names)) ? survivor_names[s] : who);
	}

	// a package landed on its mark: that need of that site is met
	action dr_delivered (string id, string site, string need) {
		int s <- int(site);
		int n <- int(need);
		if (mission_running and s >= 0 and s < length(site_got) and n >= 0 and n < length(site_got[s])) {
			list<bool> got <- site_got[s];
			got[n] <- true;
			site_got[s] <- got;
			targets_timer <- 0.0;
			do write_log(id, "delivered", site_needs[s][n] + " to " + site_names[s]);
		}
	}

	// the mission is over on a headset: its result
	action dr_result (string id, string mission_name, string stars, string score, string seconds) {
		unity_player p <- unity_player first_with (each.name = id);
		if (p != nil) {
			p.ready <- false;
			p.in_mission <- false;
			p.drone_flying <- false;
			p.last_result <- upper_case(mission_name) + " " + stars + "/3";
		}
		do write_log(id, "result", mission_name + ": " + stars + " stars, " + score + " points, " + seconds + " s");
	}

	// ---------- the class's mission ----------

	// the mission begins for these headsets (or goes on, for one that joins a little late)
	action call_start (list<unity_player> who) {
		ask who {
			in_mission <- true;
			ready <- false;
		}
		do tell_some(who, ["dronevr"::"start", "mission"::mission_names[mission], "t"::int(mission_time * 1000)]);
	}

	// the teacher's button: the mission starts now, for every headset
	action teacher_starts (int m) {
		if (!mission_running) {
			ask world { do start_mission(m); }
			class_mission <- m;
		}
		do call_start(unity_player where (!each.in_mission));
		do write_log("teacher", "start", mission_names[mission]);
	}

	action teacher_ends {
		if (mission_running) {
			do end_mission;
			do write_log("teacher", "end", "");
		}
	}

	action end_mission {
		do tell_some(flying(), ["dronevr"::"end"]);
		ask unity_player {
			in_mission <- false;
			ready <- false;
			drone_flying <- false;
		}
		ask world { do stop_mission; }
		class_mission <- -1;
	}

	// when every headset is ready, the mission starts for all of them together
	reflex gather when: !mission_running and class_mission >= 0 {
		list<unity_player> those <- unity_player where (each.ready);
		if (empty(those)) {
			class_mission <- -1;
		} else {
			ready_timer <- ready_timer + real_dt;
			if (length(those) = length(unity_player)) { start_timer <- start_timer + real_dt; } else { start_timer <- 0.0; }
			if (start_timer >= start_delay or ready_timer >= ready_patience) {
				int m <- class_mission;
				ask world { do start_mission(m); }
				do call_start(those);
				do write_log("class", "start", mission_names[mission] + " with " + length(those) + " headsets");
			}
		}
	}

	// a mission started with the model's own button (experiment DRONE): the whole class flies it
	reflex from_the_model when: mission_running and class_mission < 0 {
		class_mission <- mission;
		do call_start(unity_player where (!each.in_mission));
	}

	// a headset that is ready a little after the start joins the mission
	reflex late_comers when: mission_running and mission_time <= join_window {
		list<unity_player> those <- unity_player where (each.ready and !each.in_mission);
		if (!empty(those)) { do call_start(those); }
	}

	// the mission is over when its time is up, or when no headset is flying it any more
	reflex finish when: mission_running {
		if (mission_time >= mission_minutes[mission] * 60.0 + end_grace or (mission_time > join_window and empty(flying()))) {
			do write_log("class", "end", mission_names[mission]);
			do end_mission;
		}
	}

	// the count of the class, for the headsets that wait
	reflex tell_count when: !empty(unity_player) {
		count_timer <- count_timer - real_dt;
		if (count_timer <= 0) {
			count_timer <- count_every;
			int m <- mission_running ? mission : class_mission;
			do tell_players(["dronevr"::"class", "ready"::length(unity_player where (each.ready or each.in_mission)), "players"::length(unity_player), "mission"::(m >= 0 ? mission_names[m] : "")]);
		}
	}

	// ---------- what GAMA sends ----------

	reflex send_weather when: mission_running and !empty(flying()) {
		do tell_some(flying(), ["dronevr"::"weather", "t"::int(mission_time * 1000), "wind"::int(wind_now * 100), "mean"::int(wind_mean * 100),
			"from"::int(wind_from_now * 10), "level"::int(flood_level * 1000)]);
	}

	reflex send_targets when: mission_running and !empty(flying()) {
		targets_timer <- targets_timer - real_dt;
		if (targets_timer <= 0) {
			targets_timer <- targets_every;
			string m <- mission_names[mission];
			list<int> s <- [];
			if (m = "bantay") {
				loop q over: hazard_quality { s << int(round(q * 100)); }
			} else if (m = "sagip") {
				loop k from: 0 to: length(survivor_names) - 1 {
					survivor v <- survivor first_with (each.index = k);
					s << v.told();
				}
			} else if (m = "hatid") {
				loop got over: site_got {
					int bits <- 0;
					loop n from: 0 to: length(got) - 1 { if (got[n]) { bits <- bits + int(2 ^ n); } }
					s << bits;
				}
			}
			if (!empty(s)) { do tell_some(flying(), ["dronevr"::"targets", "mission"::m, "s"::s]); }
		}
	}

	reflex send_boats when: world.searching() and !empty(flying()) {
		list<int> p <- [];
		loop k from: 0 to: length(boat_homes) - 1 {
			boat b <- boat first_with (each.index = k);
			p << int(b.web.x * 10); p << int(b.web.y * 10); p << int(b.heading); p << b.phase; p << b.job;
		}
		do tell_some(flying(), ["dronevr"::"boats", "p"::p]);
	}

	// every headset sees the others' drones
	reflex send_drones when: mission_running and length(flying()) > 1 {
		drones_timer <- drones_timer - real_dt;
		if (drones_timer <= 0) {
			drones_timer <- drones_every;
			list<string> ids <- [];
			list<int> p <- [];
			loop d over: (flying() where (each.drone_flying)) {
				ids << d.name;
				p << int(d.drone_at.x * 10); p << int(d.drone_at.z * 10); p << int(d.drone_at.y * 10); p << int(d.drone_heading); p << d.drone_battery;
			}
			do tell_some(flying(), ["dronevr"::"drones", "ids"::ids, "p"::p]);
		}
	}
}

species unity_player parent: abstract_unity_player {
	float player_size <- 3.0;
	rgb color <- #red;
	float cone_distance <- 12.0;
	float cone_amplitude <- 90.0;
	float player_rotation <- 90.0;
	bool to_display <- true;

	// where this headset is in the session
	bool ready <- false;       // its operator pressed Begin
	bool in_mission <- false;  // it flies the class's mission
	string mode <- "";         // desk, pilot or hybrid
	string last_result <- "";
	// its drone: {x, z, height} in DRONE VR's coordinates, its heading (degrees), its battery (percent)
	point drone_at <- {-148.0, -134.0, 0.0};
	float drone_heading <- 90.0;
	int drone_battery <- 100;
	bool drone_flying <- false;

	aspect default {
		if to_display {
			if selected { draw circle(player_size) color: rgb(#blue, 0.5); }
			draw circle(player_size / 2.0) color: color;
			draw player_perception_cone() color: rgb(color, 0.5);
			if (in_mission) {
				// its drone
				point spot <- {drone_at.x + offset_x, drone_at.y + offset_y};
				string label <- name + " " + int(drone_at.z) + " m " + drone_battery + "%";
				draw square(6.0) at: spot rotate: drone_heading + 45.0 color: (drone_flying ? #orangered : #white) border: #black;
				draw label at: {spot.x + 6, spot.y - 3} color: #black font: font("Helvetica", 10, #plain);
			}
		}
	}
}

experiment vr_xp parent: DRONE autorun: false type: unity {
	float minimum_cycle_duration <- 0.1;
	string unity_linker_species <- string(unity_linker);
	list<string> displays_to_hide <- ["barangay"];

	user_command "Start the class's mission now" {
		ask unity_linker {
			do teacher_starts(mission_running ? mission : (class_mission >= 0 ? class_mission : (mission >= 0 ? mission : 2)));
		}
	}

	user_command "End the class's mission" {
		ask unity_linker { do teacher_ends; }
	}

	user_command "Raise the wind" {
		ask world { wind_scale <- min([2.0, wind_scale + 0.25]); }
	}

	user_command "Calm the wind" {
		ask world { wind_scale <- max([0.5, wind_scale - 0.25]); }
	}

	action create_player (string id) {
		ask unity_linker { do create_player(id); }
	}

	action remove_player (string id_input) {
		if (not empty(unity_player)) {
			ask first(unity_player where (each.name = id_input)) { do die; }
		}
	}

	output {
		display barangay_vr parent: barangay {
			species unity_player;
			graphics "class" {
				int m <- mission_running ? mission : (empty(unity_linker) ? -1 : first(unity_linker).class_mission);
				string label <- "Headsets: " + length(unity_player) + "   ready or flying: " + length(unity_player where (each.ready or each.in_mission)) + ((m >= 0 and !mission_running) ? "   getting ready for " + upper_case(mission_names[m]) : "");
				draw label at: {6, 40} color: #black font: font("Helvetica", 12, #plain);
			}
		}
	}
}
