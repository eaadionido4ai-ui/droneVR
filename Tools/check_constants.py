#!/usr/bin/env python3
"""Checks that the places that describe the same thing use the same numbers:
  - the ground, in Unity (DroneVR.Core: Terrain.cs), GAMA (DRONE.gaml), the reference (flight.py) and the plan
    the models are built from (plan.js);
  - the wind, in Unity (Weather.cs), GAMA and the reference;
  - the drone, in Unity (Drone.cs) and the reference;
  - the rescue boats and the class's start, in Unity (Rescue.cs, MissionStart.cs) and GAMA;
  - where DRONE VR's world lies in GAMA's (DroneSimulationManager.cs, the GAML models), and where the operators
    stand when GAMA places them;
  - the pad, in the plan and the reference.
It also checks that every text the Unity scripts ask for by name exists in Strings.cs, in both languages.

    python3 Tools/check_constants.py
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CORE = os.path.join(ROOT, 'Unity', 'Assets', 'DRONEVR', 'Scripts', 'Core')
RUNTIME = os.path.join(ROOT, 'Unity', 'Assets', 'DRONEVR', 'Scripts', 'Runtime')
read = lambda *p: open(os.path.join(ROOT, *p), encoding='utf-8').read()
cs = ''.join(open(os.path.join(CORE, f), encoding='utf-8').read() for f in ('Terrain.cs', 'Weather.cs', 'Drone.cs', 'Rescue.cs', 'MissionStart.cs'))
sim = open(os.path.join(RUNTIME, 'DroneSimulationManager.cs'), encoding='utf-8').read()
gaml = read('GAMA', 'models', 'DRONE.gaml')
vr = read('GAMA', 'models', 'DRONE VR.gaml')
plan = read('Tools', 'world-export', 'src', 'plan.js')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'flight-reference'))
import flight  # noqa: E402

N = r'(-?[0-9.]+)'


def num(text, pattern, where):
    m = re.search(pattern, text)
    if not m:
        raise SystemExit('not found in %s: %s' % (where, pattern))
    return float(m.group(1))


C = lambda name: num(cs, r'\b' + name + r' = ' + N, 'Unity')
G = lambda name: num(gaml, r'\b' + name + r' <- ' + N, 'GAMA')
J = lambda name: num(plan, r'\b' + name + r': ' + N, 'the plan')
Py = lambda name: float(getattr(flight, name))

# name, Unity, GAMA, the reference, the plan (None where a place does not have it)
ground = [
    ('RiverNorth', 'river_north', 'RIVER_NORTH', 'riverNorth'), ('RiverSouth', 'river_south', 'RIVER_SOUTH', 'riverSouth'),
    ('RiverBank', 'river_bank', 'RIVER_BANK', 'riverBank'), ('RiverDepth', 'river_depth', 'RIVER_DEPTH', 'riverDepth'),
    ('NorthBank', 'north_bank', 'NORTH_BANK', 'northBank'), ('NorthSlope', 'north_slope', 'NORTH_SLOPE', 'northSlope'),
    ('ValleyBank', 'valley_bank', 'VALLEY_BANK', 'valleyBank'), ('ValleySlope', 'valley_slope', 'VALLEY_SLOPE', 'valleySlope'),
    ('ValleySide', 'valley_side', 'VALLEY_SIDE', 'valleySide'), ('CreekX', 'creek_x', 'CREEK_X', 'creekX'),
    ('CreekHalf', 'creek_half', 'CREEK_HALF', 'creekHalf'), ('CreekDepth', 'creek_depth', 'CREEK_DEPTH', 'creekDepth'),
    ('SouthBank', 'south_bank', 'SOUTH_BANK', 'southBank'), ('SouthSlope', 'south_slope', 'SOUTH_SLOPE', 'southSlope'),
    ('HillX', 'hill_x', 'HILL_X', 'hillX'), ('HillZ', 'hill_z', 'HILL_Z', 'hillZ'), ('HillR', 'hill_r', 'HILL_R', 'hillR'),
    ('HillPeak', 'hill_peak', 'HILL_PEAK', 'hillPeak'), ('HillTop', 'hill_top', 'HILL_TOP', 'hillTop'),
    ('RiseX', 'rise_x', 'RISE_X', 'riseX'), ('RiseZ', 'rise_z', 'RISE_Z', 'riseZ'), ('RiseR', 'rise_r', 'RISE_R', 'riseR'),
    ('RisePeak', 'rise_peak', 'RISE_PEAK', 'risePeak'), ('RiseTop', 'rise_top', 'RISE_TOP', 'riseTop'),
]
wind = [
    ('GustPeriodA', 'gust_period_a', 'GUST_PERIOD_A'), ('GustPeriodB', 'gust_period_b', 'GUST_PERIOD_B'),
    ('GustShareA', 'gust_share_a', 'GUST_SHARE_A'), ('GustShareB', 'gust_share_b', 'GUST_SHARE_B'),
    ('GustPhaseB', 'gust_phase_b', 'GUST_PHASE_B'), ('SwingPeriod', 'swing_period', 'SWING_PERIOD'),
    ('SwingDegrees', 'swing_degrees', 'SWING_DEGREES'), ('LowShare', None, 'LOW_SHARE'), ('FullHeight', None, 'FULL_HEIGHT'),
]
drone = [('Dt', 'DT'), ('MaxSpeed', 'MAX_SPEED'), ('MaxAirSpeed', 'MAX_AIR_SPEED'), ('MaxClimb', 'MAX_CLIMB'), ('MaxDescent', 'MAX_DESCENT'),
         ('YawRate', 'YAW_RATE'), ('TauH', 'TAU_H'), ('TauV', 'TAU_V'), ('GustHold', 'GUST_HOLD'), ('Gear', 'GEAR'), ('Ceiling', 'CEILING'),
         ('MaxPayloadKg', 'MAX_PAYLOAD_KG'), ('LoadSlow', 'LOAD_SLOW'), ('LoadClimbSlow', 'LOAD_CLIMB_SLOW'), ('HoverSeconds', 'HOVER_SECONDS'),
         ('PayloadDrain', 'PAYLOAD_DRAIN'), ('SpeedDrain', 'SPEED_DRAIN'), ('ClimbDrain', 'CLIMB_DRAIN'), ('LandSpeed', 'LAND_SPEED'), ('LandSlide', 'LAND_SLIDE')]

rows = []
for c, g, p, j in ground:
    rows.append(('ground: ' + c, [C(c), G(g), Py(p), J(j)]))
for c, g, p in wind:
    rows.append(('wind: ' + c, [C(c)] + ([G(g)] if g else []) + [Py(p)]))
for c, p in drone:
    rows.append(('drone: ' + c, [C(c), Py(p)]))
rows += [
    ('boats: speed', [C('Speed'), G('boat_speed')]),
    ('boats: loading (s)', [C('LoadSeconds'), G('load_seconds')]),
    ('boats: unloading (s)', [C('UnloadSeconds'), G('unload_seconds')]),
    ('class: start delay (s)', [C('StartDelay'), num(vr, r'start_delay <- ' + N, 'GAMA')]),
    ('GAMA x offset', [num(sim, r'OffsetX = ' + N + 'f', 'Unity'), G('offset_x')]),
    ('GAMA y offset', [num(sim, r'OffsetY = ' + N + 'f', 'Unity'), G('offset_y')]),
    ('pad x', [num(plan, r'PAD = \{ x: ' + N, 'the plan'), Py('PAD_X')]),
    ('pad z', [num(plan, r'PAD = \{ x: [-0-9.]+, z: ' + N, 'the plan'), Py('PAD_Z')]),
    ('pad size', [num(plan, r'PAD = \{[^}]*size: ' + N, 'the plan'), Py('PAD_SIZE')]),
]
homes_cs = re.findall(r'Homes = \{ ([^}]*) \}', cs)[0].replace(' ', '')
homes_gaml = re.findall(r'boat_homes <- \[([^\]]*)\]', gaml)[0].replace(' ', '')
bad = 0
for name, values in rows:
    same = all(abs(v - values[0]) < 1e-9 for v in values)
    print('%-26s %s %s' % (name, ' '.join('%-8g' % v for v in values), 'ok' if same else 'DIFFERENT'))
    bad += 0 if same else 1
print('%-26s %s' % ('boats: their landings', 'ok' if homes_cs == homes_gaml else 'DIFFERENT: %s / %s' % (homes_cs, homes_gaml)))
bad += 0 if homes_cs == homes_gaml else 1

# where GAMA places the operators: in the operations center, on its floor
eoc = re.search(r'EOC = \{ x: ' + N + r', z: ' + N + r', floor: ' + N, plan)
ex, ez, ef = (float(v) for v in eoc.groups())
first = re.search(r'init_locations <- \[\{' + N + r', ' + N + r', ' + N, vr)
gx, gy, gz = (float(v) for v in first.groups())
ok = abs(gx - (ex + 240)) < 1e-9 and abs(gy - (ez + 180)) < 4 and abs(gz - ef) < 1e-9
print('%-26s %s' % ('operators start', 'ok' if ok else 'DIFFERENT: GAMA (%g, %g, %g), the room (%g, %g, %g)' % (gx, gy, gz, ex + 240, ez + 180, ef)))
bad += 0 if ok else 1

# every text the scripts ask for by name is in Strings.cs
strings = open(os.path.join(CORE, 'Strings.cs'), encoding='utf-8').read()
keys = set(re.findall(r'\{ "([a-z0-9_.]+)", new\[\]', strings))
used = set()
for folder in (CORE, RUNTIME):
    for f in os.listdir(folder):
        if not f.endswith('.cs') or f == 'Strings.cs':
            continue
        text = open(os.path.join(folder, f), encoding='utf-8').read()
        used |= set(re.findall(r'Strings\.(?:Get|Format|Has)\("([a-z0-9_.]+)"', text))
        used |= set(re.findall(r'(?:Tell|Say|new MissionEvent)\(Note\.\w+, "([a-z0-9_.]+)"', text))
# a name built in the code ("mission." + id + ".name") is checked by the tests (StringsTests), not here
missing = sorted(k for k in used - keys if not k.endswith('.'))
print('%-26s %s' % ('texts (%d asked for)' % len(used), 'ok' if not missing else 'MISSING: ' + ', '.join(missing)))
bad += 1 if missing else 0
sys.exit(1 if bad else 0)
