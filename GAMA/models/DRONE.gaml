/**
* Name: DRONE
* The riverside barangay of DRONE VR as an agent-based model:
*   - its ground, as a formula, and the flood that rises over it;
*   - the weather of each mission: the wind, with its gusts, and the flood's level;
*   - the people stranded by the flood (agents) and the rescue boats (agents) that go to those the drones find,
*     along the flooded streets, and bring them back;
*   - the hazards after the earthquake and the places that asked for supplies, and how far the class has come.
* "DRONE VR.gaml" links this model to the Unity app through the SIMPLE plugin: the drones are flown on the
* headsets, and what they find is told to this model. "DRONE Town.gaml" holds the places; it is written from the
* same plan as the 3D models and Unity's data (Tools/world-export/src/plan.js).
*
* The numbers here are the same as in Unity (Assets/DRONEVR/Scripts/Core: Terrain, Weather, Rescue, MissionStart)
* and in Tools/flight-reference/flight.py. Tools/check_constants.py checks them: change them in every place.
*
* Places are given in DRONE VR's coordinates (metres; x east, z south) as points {x, z}, and drawn in GAMA at
* (x + 240, z + 180): GAMA's world is 480 m by 360 m with its origin at the north-west corner.
* Author: DRONE VR
* Tags: drone, flood, wind, search and rescue, logistics, SIMPLE
*/
model DRONE

import "DRONE Town.gaml"

global {
	geometry shape <- rectangle(480, 360);
	float offset_x <- 240.0;
	float offset_y <- 180.0;

	// ---------- the ground (Unity: Terrain) ----------
	float river_north <- 100.0;
	float river_south <- 140.0;
	float river_bank <- 4.0;
	float river_depth <- 3.1;
	float north_bank <- 0.6;
	float north_slope <- 0.010;
	float valley_bank <- 0.3;
	float valley_slope <- 0.004;
	float valley_side <- 0.022;
	float creek_x <- -40.0;
	float creek_half <- 4.0;
	float creek_depth <- 1.5;
	float south_bank <- 0.8;
	float south_slope <- 0.03;
	float hill_x <- 150.0;
	float hill_z <- -110.0;
	float hill_r <- 90.0;
	float hill_peak <- 6.0;
	float hill_top <- 4.0;
	float rise_x <- -170.0;
	float rise_z <- -125.0;
	float rise_r <- 70.0;
	float rise_peak <- 3.0;
	float rise_top <- 1.5;
	// the map shows the ground in cells of 10 m: the height of the middle of each, row by row from the north
	float cell <- 10.0;
	int ncx <- 48;
	int ncz <- 36;
	list<float> cell_ground <- [];

	// ---------- the wind (Unity: Wind) ----------
	float gust_period_a <- 11.0;
	float gust_period_b <- 4.3;
	float gust_share_a <- 0.6;
	float gust_share_b <- 0.4;
	float gust_phase_b <- 1.3;
	float swing_period <- 23.0;
	float swing_degrees <- 15.0;

	// ---------- the mission that is running ----------
	// which mission (its place in mission_names), or -1 when none is running
	int mission <- -1;
	bool mission_running <- false;
	// seconds since the mission began
	float mission_time <- 0.0;
	// the teacher's hand on the wind: 1 is the mission's own wind
	float wind_scale <- 1.0 min: 0.0 max: 2.0;
	// the weather now: the wind's speed and its mean (m/s at 60 m), where it blows from (degrees), the flood's level (m)
	float wind_now <- 0.0;
	float wind_mean <- 0.0;
	float wind_from_now <- 0.0;
	float flood_level <- 0.0;

	// ---------- the rescue boats (Unity: RescueFleet) ----------
	float boat_speed <- 6.0;
	float load_seconds <- 6.0;
	float unload_seconds <- 4.0;
	// the landing of each boat: two at the west landing, one at the east
	list<int> boat_homes <- [0, 0, 1];

	// ---------- what the class has done ----------
	// the best photograph of each hazard so far, from 0 to 1 (Unity: HazardPatrol)
	list<float> hazard_quality <- [];
	// for each site, which of its needs are met (Unity: ReliefFlights)
	list<list<bool>> site_got <- [];

	// the clock: the boats and the weather run in real time, whatever the length of a GAMA cycle
	float last_machine_time <- -1.0;
	float real_dt <- 0.0;

	init {
		loop j from: 0 to: ncz - 1 {
			loop i from: 0 to: ncx - 1 {
				cell_ground << ground_at(-offset_x + (i + 0.5) * cell, -offset_y + (j + 0.5) * cell);
			}
		}
		loop k from: 0 to: length(survivor_names) - 1 {
			create survivor {
				index <- k;
				name <- survivor_names[k];
				web <- survivor_at[k];
				people <- survivor_people[k];
				hidden <- survivor_hidden[k];
				limit <- survivor_limit[k] * 60.0;
				needs_ring <- survivor_ring[k];
				location <- {web.x + offset_x, web.y + offset_y};
			}
		}
		loop k from: 0 to: length(boat_homes) - 1 {
			create boat {
				index <- k;
				home <- boat_homes[k];
				do go_home;
			}
		}
		do reset_targets;
	}

	// ---------- the ground ----------

	float clamp01 (float v) {
		return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
	}

	// the height of the ground at a point, in metres above the river's normal level
	float ground_at (float x, float z) {
		if (z >= river_south) { return south_bank + (z - river_south) * south_slope; }
		float side <- max([0.0, abs(x - creek_x) - creek_half]) * valley_side;
		float cut <- creek_depth * clamp01(creek_half - abs(x - creek_x));
		if (z > river_north) {
			float bank <- min([north_bank, valley_bank + side]) - cut;
			float k <- (z - river_north) / (river_south - river_north);
			float dip <- clamp01(min([z - river_north, river_south - z]) / river_bank);
			return bank + (south_bank - bank) * k - river_depth * dip;
		}
		float up <- river_north - z;
		float g <- min([north_bank + up * north_slope, valley_bank + up * valley_slope + side]);
		g <- g + min([hill_top, hill_peak * max([0.0, 1.0 - sqrt((x - hill_x) ^ 2 + (z - hill_z) ^ 2) / hill_r])]);
		g <- g + min([rise_top, rise_peak * max([0.0, 1.0 - sqrt((x - rise_x) ^ 2 + (z - rise_z) ^ 2) / rise_r])]);
		return g - cut;
	}

	// ---------- the weather ----------

	// the wind's speed at 60 m up, t seconds into a mission (sin takes degrees in GAML: a whole wave is 360)
	float wind_speed (float mean, float gust, float t) {
		float wave <- gust_share_a * sin(360.0 * t / gust_period_a) + gust_share_b * sin(360.0 * t / gust_period_b + gust_phase_b * 180.0 / #pi);
		return max([0.0, mean * (1.0 + gust * wave)]);
	}

	// where the wind blows from (degrees), t seconds into a mission
	float wind_from (float from, float t) {
		return from + swing_degrees * sin(360.0 * t / swing_period);
	}

	// the flood's level t seconds into mission m
	float level_at (int m, float t) {
		list<float> mins <- mission_flood_minutes[m];
		list<float> levs <- mission_flood_levels[m];
		float mt <- t / 60.0;
		if (mt <= mins[0]) { return levs[0]; }
		if (length(mins) > 1) {
			loop k from: 1 to: length(mins) - 1 {
				if (mt <= mins[k]) {
					return levs[k - 1] + (levs[k] - levs[k - 1]) * (mt - mins[k - 1]) / (mins[k] - mins[k - 1]);
				}
			}
		}
		return last(levs);
	}

	reflex clock {
		float now <- gama.machine_time;
		real_dt <- last_machine_time < 0 ? 0.0 : min([0.5, (now - last_machine_time) / 1000.0]);
		last_machine_time <- now;
	}

	reflex run_mission when: mission_running {
		mission_time <- mission_time + real_dt;
		wind_mean <- mission_wind[mission] * wind_scale;
		wind_now <- wind_speed(wind_mean, mission_gust[mission], mission_time);
		wind_from_now <- wind_from(mission_from[mission], mission_time);
		flood_level <- level_at(mission, mission_time);
	}

	// ---------- starting and ending a mission ----------

	action reset_targets {
		hazard_quality <- [];
		loop k from: 0 to: length(hazard_names) - 1 { hazard_quality << 0.0; }
		site_got <- [];
		loop k from: 0 to: length(site_names) - 1 {
			list<bool> got <- [];
			loop n from: 0 to: length(site_needs[k]) - 1 { got << false; }
			site_got << got;
		}
		ask survivor { do reset; }
		ask boat { do go_home; }
	}

	action start_mission (int m) {
		mission <- m;
		mission_time <- 0.0;
		do reset_targets;
		wind_mean <- mission_wind[m] * wind_scale;
		wind_now <- wind_mean;
		wind_from_now <- mission_from[m];
		flood_level <- level_at(m, 0.0);
		mission_running <- true;
	}

	action stop_mission {
		mission_running <- false;
		mission <- -1;
		flood_level <- 0.0;
		wind_now <- 0.0;
		wind_mean <- 0.0;
	}

	bool searching {
		return mission_running and mission_names[mission] = "sagip";
	}

	// ---------- the boats ----------

	// the length of the way from a landing to a group, in metres
	float way (int home, int s) {
		list<point> r <- boat_routes[home][s];
		float len <- 0.0;
		if (length(r) > 1) {
			loop k from: 1 to: length(r) - 1 {
				len <- len + sqrt((r[k].x - r[k - 1].x) ^ 2 + (r[k].y - r[k - 1].y) ^ 2);
			}
		}
		return len;
	}

	// a group can be reached: the boat that can be there soonest is given the job
	action send_boat (int s) {
		boat best <- nil;
		float eta <- 1.0e12;
		loop b over: boat {
			float t <- max([mission_time, b.free_at]) + way(b.home, s) / boat_speed;
			if (t < eta - 1.0e-9) { eta <- t; best <- b; }
		}
		if (best != nil) {
			float done <- max([mission_time, best.free_at]) + 2.0 * way(best.home, s) / boat_speed + load_seconds + unload_seconds;
			ask best {
				free_at <- done;
				jobs << s;
			}
			ask (survivor first_with (each.index = s)) { boat_sent <- true; }
		}
	}

	// a drone found this group: a boat goes, unless the group is in fast water and has no life ring yet
	action group_found (int s, string by) {
		survivor v <- survivor first_with (each.index = s);
		if (v != nil and !v.found) {
			v.found <- true;
			v.found_at <- mission_time;
			v.found_by <- by;
			if (!v.boat_sent and (!v.needs_ring or v.ringed)) { do send_boat(s); }
		}
	}

	// a life ring reached this group: now a boat can pull it in
	action group_ringed (int s) {
		survivor v <- survivor first_with (each.index = s);
		if (v != nil and !v.ringed) {
			v.ringed <- true;
			if (v.found and !v.boat_sent) { do send_boat(s); }
		}
	}
}

// people waiting for rescue in the flood
species survivor {
	int index;
	// where they are, in DRONE VR's coordinates {x, z}
	point web;
	int people;
	// they cannot be seen from above: only the thermal camera finds them, from close
	bool hidden;
	// seconds they can safely wait
	float limit;
	// they are in fast water: a boat can only pull them in once a life ring has been dropped to them
	bool needs_ring;
	bool found <- false;
	bool ringed <- false;
	bool boat_sent <- false;
	bool aboard <- false;
	bool ashore <- false;
	float found_at <- -1.0;
	float aboard_at <- -1.0;
	string found_by <- "";

	action reset {
		found <- false; ringed <- false; boat_sent <- false; aboard <- false; ashore <- false;
		found_at <- -1.0; aboard_at <- -1.0; found_by <- "";
	}

	// what the headsets are told: 1 found, 2 a life ring has reached them, 4 aboard a boat
	int told {
		return (found ? 1 : 0) + (ringed ? 2 : 0) + (aboard ? 4 : 0);
	}

	bool too_late {
		return !aboard and mission_time > limit;
	}

	// yellow: not found yet; red: not found, and out of time; orange: found, waiting for a life ring;
	// green: found, a boat is coming; grey: aboard
	aspect default {
		if (!ashore and world.searching()) {
			rgb c <- aboard ? #grey : (found ? (needs_ring and !ringed ? #orange : #green) : (too_late() ? #red : #yellow));
			string label <- (found ? "" : "? ") + name + " (" + people + ")";
			draw circle(aboard ? 2.0 : 3.5) color: c border: #black;
			draw label at: {location.x + 5, location.y - 2} color: #black font: font("Helvetica", 10, #plain);
		}
	}
}

// a rescue boat: it waits at its landing, goes to the group it is sent to, takes the people aboard and brings them back
species boat {
	int index;
	// its landing, a place in staging_at
	int home;
	// where it is, in DRONE VR's coordinates {x, z}
	point web;
	// degrees; 0 faces north, 90 east
	float heading <- 0.0;
	// 0 waiting, 1 on its way out, 2 taking people aboard, 3 on its way back, 4 putting them ashore
	int phase <- 0;
	// the group it is going to or carrying, or -1
	int job <- -1;
	list<int> jobs <- [];
	// the second at which it will be back with nothing more to do
	float free_at <- 0.0;
	list<point> route <- [];
	int leg <- 0;
	float timer <- 0.0;

	action go_home {
		web <- staging_at[home];
		phase <- 0; job <- -1; jobs <- []; free_at <- 0.0; route <- []; leg <- 0; timer <- 0.0; heading <- 0.0;
		location <- {web.x + offset_x + (index - 1) * 4.0, web.y + offset_y};
	}

	// moves along its way, forwards (1) or back (-1); true at the end of the way
	bool sail (int dir) {
		float go <- boat_speed * real_dt;
		loop while: go > 0 and leg >= 0 and leg < length(route) {
			point t <- route[leg];
			float d <- sqrt((t.x - web.x) ^ 2 + (t.y - web.y) ^ 2);
			if (d > 1.0e-6) { heading <- atan2(t.x - web.x, -(t.y - web.y)); }
			if (go >= d) {
				web <- t; go <- go - d; leg <- leg + dir;
			} else {
				web <- {web.x + (t.x - web.x) * go / d, web.y + (t.y - web.y) * go / d};
				go <- 0.0;
			}
		}
		return leg < 0 or leg >= length(route);
	}

	reflex work when: world.searching() {
		if (phase = 0) {
			if (!empty(jobs)) {
				job <- first(jobs);
				remove index: 0 from: jobs;
				route <- boat_routes[home][job];
				leg <- 1;
				phase <- 1;
			}
		} else if (phase = 1) {
			if (sail(1)) { phase <- 2; timer <- 0.0; }
		} else if (phase = 2) {
			timer <- timer + real_dt;
			if (timer >= load_seconds) {
				ask (survivor first_with (each.index = job)) { aboard <- true; aboard_at <- mission_time; }
				phase <- 3;
				leg <- length(route) - 2;
			}
		} else if (phase = 3) {
			if (sail(-1)) { phase <- 4; timer <- 0.0; }
		} else if (phase = 4) {
			timer <- timer + real_dt;
			if (timer >= unload_seconds) {
				ask (survivor first_with (each.index = job)) { ashore <- true; }
				job <- -1;
				phase <- 0;
			}
		}
		if (phase != 0) { location <- {web.x + offset_x, web.y + offset_y}; }
	}

	aspect default {
		if (world.searching()) {
			draw triangle(7.0) rotate: heading color: (phase = 0 ? #white : #orangered) border: #black;
		}
	}
}

experiment DRONE type: gui {
	parameter "Mission (0 LIPAD, 1 BANTAY, 2 SAGIP, 3 HATID)" var: mission min: -1 max: 3;
	parameter "Wind (1 is the mission's own)" var: wind_scale;
	user_command "Start the mission" { ask world { do start_mission(mission < 0 ? 2 : mission); } }
	user_command "End the mission" { ask world { do stop_mission; } }
	// without headsets: pretend a drone found the next group, and dropped a life ring where one is needed
	user_command "A drone finds the next group" {
		ask world {
			survivor v <- first(survivor where (!each.found));
			if (v != nil and searching()) {
				do group_found(v.index, "nobody");
				if (v.needs_ring) { do group_ringed(v.index); }
			}
		}
	}

	output {
		display barangay type: 2d {
			graphics "ground" {
				// the ground, cell by cell: green and higher is browner; under the flood it is blue
				loop j from: 0 to: ncz - 1 {
					loop i from: 0 to: ncx - 1 {
						float g <- cell_ground[j * ncx + i];
						float k <- min([1.0, max([0.0, g / 8.0])]);
						rgb col <- g < 0.0 ? rgb(40, 90, 180) : rgb(int(140 + 60 * k), int(175 - 20 * k), int(110 - 20 * k));
						if (mission_running and g >= 0.0 and g < flood_level) { col <- flood_level - g < 1.0 ? rgb(90, 150, 220) : rgb(50, 110, 200); }
						draw square(cell) at: {(i + 0.5) * cell, (j + 0.5) * cell} color: col;
					}
				}
				loop s over: street_lines {
					draw line([{s[0] + offset_x, s[1] + offset_y}, {s[2] + offset_x, s[3] + offset_y}]) color: rgb(95, 95, 100) width: 3;
				}
				loop b over: building_boxes {
					draw rectangle(b[2], b[3]) at: {b[0] + offset_x, b[1] + offset_y} color: rgb(235, 232, 220) border: #grey;
				}
				loop t over: tower_at {
					draw circle(3.0) at: {t.x + offset_x, t.y + offset_y} color: #darkgrey border: #black;
				}
				draw square(8.0) at: {pad_at.x + offset_x, pad_at.y + offset_y} color: #white border: #black;
				draw "PAD" at: {pad_at.x + offset_x + 6, pad_at.y + offset_y + 2} color: #black font: font("Helvetica", 10, #bold);
			}
			graphics "mission" {
				if (mission_running) {
					string m <- mission_names[mission];
					if (m = "lipad") {
						loop k from: 0 to: length(gate_at) - 1 {
							draw circle(5.0) at: {gate_at[k].x + offset_x, gate_at[k].y + offset_y} color: rgb(255, 255, 255, 80) border: #black;
							draw string(k + 1) at: {gate_at[k].x + offset_x - 2, gate_at[k].y + offset_y + 2} color: #black font: font("Helvetica", 10, #bold);
						}
					}
					if (m = "bantay") {
						loop k from: 0 to: length(hazard_at) - 1 {
							float q <- hazard_quality[k];
							draw square(9.0) at: {hazard_at[k].x + offset_x, hazard_at[k].y + offset_y} color: (q >= 0.5 ? #green : (q > 0.0 ? #orange : #red)) border: #black;
							draw hazard_names[k] + (q > 0.0 ? " " + int(q * 100) + "%" : "") at: {hazard_at[k].x + offset_x + 7, hazard_at[k].y + offset_y + 3} color: #black font: font("Helvetica", 10, #plain);
						}
					}
					if (m = "sagip") {
						loop s over: staging_at {
							draw square(10.0) at: {s.x + offset_x, s.y + offset_y} color: rgb(255, 255, 255, 120) border: #black;
						}
					}
					if (m = "hatid") {
						loop k from: 0 to: length(site_at) - 1 {
							int got <- site_got[k] count (each);
							draw circle(site_radius[k] + 3.0) at: {site_at[k].x + offset_x, site_at[k].y + offset_y} color: (got = length(site_got[k]) ? #green : (got > 0 ? #orange : #yellow)) border: #black;
							draw site_names[k] + " " + got + "/" + length(site_got[k]) at: {site_at[k].x + offset_x + 10, site_at[k].y + offset_y + 3} color: #black font: font("Helvetica", 10, #plain);
						}
					}
				}
			}
			species survivor;
			species boat;
			graphics "weather" {
				if (mission_running) {
					draw "Mission: " + upper_case(mission_names[mission]) + "   " + int(mission_time / 60) + " min " + (int(mission_time) mod 60) + " s of " + int(mission_minutes[mission]) + " min" at: {6, 12} color: #black font: font("Helvetica", 14, #bold);
					draw "Wind: " + (int(wind_now * 10) / 10.0) + " m/s from " + int(wind_from_now) + " degrees   Flood: " + (int(flood_level * 100) / 100.0) + " m" at: {6, 26} color: #black font: font("Helvetica", 12, #plain);
				} else {
					draw "No mission is running." at: {6, 12} color: #black font: font("Helvetica", 14, #bold);
				}
			}
		}
	}
}
