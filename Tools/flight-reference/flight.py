"""Reference version of DRONE VR's ground, wind and drone flight (the same steps as the C# version).

The ground is the formula of Unity/Assets/DRONEVR/Scripts/Core/Terrain.cs, the wind that of Weather.cs, and
one step of the drone that of Drone.cs (DroneModel.Step), over open ground: the reference knows the pad, but
no buildings or trees. Tools/flight-reference/check_reference.py flies a few set flights with it and compares
them with the recorded results, which the C# tests also use. Tools/check_constants.py checks that the numbers
here are the ones in C#, in the plan (Tools/world-export/src/plan.js) and in the GAMA model.
"""
import math

# ---------- the ground (Terrain.cs) ----------
X0, X1, Z0, Z1 = -240.0, 240.0, -180.0, 180.0
RIVER_NORTH = 100.0
RIVER_SOUTH = 140.0
RIVER_BANK = 4.0
RIVER_DEPTH = 3.1
NORTH_BANK = 0.6
NORTH_SLOPE = 0.010
VALLEY_BANK = 0.3
VALLEY_SLOPE = 0.004
VALLEY_SIDE = 0.022
CREEK_X = -40.0
CREEK_HALF = 4.0
CREEK_DEPTH = 1.5
SOUTH_BANK = 0.8
SOUTH_SLOPE = 0.03
HILL_X, HILL_Z, HILL_R, HILL_PEAK, HILL_TOP = 150.0, -110.0, 90.0, 6.0, 4.0
RISE_X, RISE_Z, RISE_R, RISE_PEAK, RISE_TOP = -170.0, -125.0, 70.0, 3.0, 1.5
PAD_X, PAD_Z, PAD_SIZE = -148.0, -134.0, 8.0


def clamp01(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def ground(x, z):
    """The height of the ground at a point, in metres above the river's normal level."""
    if z >= RIVER_SOUTH:
        return SOUTH_BANK + (z - RIVER_SOUTH) * SOUTH_SLOPE
    side = max(0.0, abs(x - CREEK_X) - CREEK_HALF) * VALLEY_SIDE
    cut = CREEK_DEPTH * clamp01(CREEK_HALF - abs(x - CREEK_X))
    if z > RIVER_NORTH:
        bank = min(NORTH_BANK, VALLEY_BANK + side) - cut
        k = (z - RIVER_NORTH) / (RIVER_SOUTH - RIVER_NORTH)
        dip = clamp01(min(z - RIVER_NORTH, RIVER_SOUTH - z) / RIVER_BANK)
        return bank + (SOUTH_BANK - bank) * k - RIVER_DEPTH * dip
    up = RIVER_NORTH - z
    g = min(NORTH_BANK + up * NORTH_SLOPE, VALLEY_BANK + up * VALLEY_SLOPE + side)
    g += min(HILL_TOP, HILL_PEAK * max(0.0, 1 - math.hypot(x - HILL_X, z - HILL_Z) / HILL_R))
    g += min(RISE_TOP, RISE_PEAK * max(0.0, 1 - math.hypot(x - RISE_X, z - RISE_Z) / RISE_R))
    return g - cut


def pad_y():
    return ground(PAD_X, PAD_Z) + 0.2


def on_pad(x, z):
    return abs(x - PAD_X) <= PAD_SIZE / 2 and abs(z - PAD_Z) <= PAD_SIZE / 2


def surface(x, z):
    """The ground, or the top of the pad."""
    s = ground(x, z)
    return max(s, pad_y()) if on_pad(x, z) else s


# ---------- the wind (Weather.cs) ----------
GUST_PERIOD_A = 11.0
GUST_PERIOD_B = 4.3
GUST_SHARE_A = 0.6
GUST_SHARE_B = 0.4
GUST_PHASE_B = 1.3
SWING_PERIOD = 23.0
SWING_DEGREES = 15.0
LOW_SHARE = 0.6
FULL_HEIGHT = 60.0
DEG = math.pi / 180


def wind_speed(mean, gust, t):
    wave = GUST_SHARE_A * math.sin(2 * math.pi * t / GUST_PERIOD_A) + GUST_SHARE_B * math.sin(2 * math.pi * t / GUST_PERIOD_B + GUST_PHASE_B)
    return max(0.0, mean * (1 + gust * wave))


def wind_from(frm, t):
    return frm + SWING_DEGREES * math.sin(2 * math.pi * t / SWING_PERIOD)


def at_height(above):
    return LOW_SHARE + (1 - LOW_SHARE) * clamp01(above / FULL_HEIGHT)


def wind_at(mean, gust, frm, t, above):
    """The wind this high above the ground, t seconds in: how fast the air moves east (x) and south (z)."""
    s = wind_speed(mean, gust, t) * at_height(above)
    f = wind_from(frm, t) * DEG
    return -math.sin(f) * s, math.cos(f) * s


def steady_at(mean, frm, above):
    s = mean * at_height(above)
    return -math.sin(frm * DEG) * s, math.cos(frm * DEG) * s


# ---------- the drone (Drone.cs) ----------
DT = 0.02
MAX_SPEED = 14.0
MAX_AIR_SPEED = 18.0
MAX_CLIMB = 5.0
MAX_DESCENT = 3.5
YAW_RATE = 100.0
TAU_H = 0.7
TAU_V = 0.4
GUST_HOLD = 0.65
GEAR = 0.25
CEILING = 120.0
MAX_PAYLOAD_KG = 8.0
LOAD_SLOW = 0.25
LOAD_CLIMB_SLOW = 0.3
HOVER_SECONDS = 420.0
PAYLOAD_DRAIN = 0.06
SPEED_DRAIN = 0.35
CLIMB_DRAIN = 0.25
LAND_SPEED = 2.5
LAND_SLIDE = 3.0
NONE, OBSTACLE, HARD_LANDING, WATER = 0, 1, 2, 3


def wrap(deg):
    deg = math.fmod(deg, 360.0)
    return deg + 360.0 if deg < 0 else deg


def clamp(v):
    return -1.0 if v < -1 else 1.0 if v > 1 else v


class Drone:
    def __init__(self, payload=0.0):
        self.x, self.z = PAD_X, PAD_Z
        self.y = pad_y() + GEAR
        self.vx = self.vy = self.vz = 0.0
        self.heading = 90.0
        self.battery = 1.0
        self.payload = payload
        self.landed = True
        self.crash = NONE

    def step(self, fwd, right, up, yaw, wx, wz, sx, sz, level):
        """One step of DT seconds. (wx, wz) is the wind at the drone, (sx, sz) its steady part."""
        if self.crash != NONE:
            return
        dt = DT
        dead = self.battery <= 0
        fwd, right, up, yaw = clamp(fwd), clamp(right), clamp(up), clamp(yaw)
        if dead:
            fwd, right, yaw, up = 0.0, 0.0, 0.0, -0.6
        self.heading = wrap(self.heading + yaw * YAW_RATE * dt)
        if self.landed:
            self.vx = self.vy = self.vz = 0.0
            if up > 0.1 and not dead:
                self.landed = False
            else:
                return
        ln = math.sqrt(fwd * fwd + right * right)
        if ln > 1:
            fwd /= ln; right /= ln
        share = min(1.0, self.payload / MAX_PAYLOAD_KG)
        mx = MAX_SPEED * (1 - LOAD_SLOW * share)
        h = self.heading * DEG
        cx = (math.sin(h) * fwd + math.cos(h) * right) * mx
        cz = (-math.cos(h) * fwd + math.sin(h) * right) * mx
        ax, az = cx - sx, cz - sz
        air = math.sqrt(ax * ax + az * az)
        if air > MAX_AIR_SPEED:
            ax *= MAX_AIR_SPEED / air; az *= MAX_AIR_SPEED / air
        gx = ax + sx + (1 - GUST_HOLD) * (wx - sx)
        gz = az + sz + (1 - GUST_HOLD) * (wz - sz)
        kh, kv = min(1.0, dt / TAU_H), min(1.0, dt / TAU_V)
        self.vx += (gx - self.vx) * kh
        self.vz += (gz - self.vz) * kh
        cy = up * MAX_CLIMB * (1 - LOAD_CLIMB_SLOW * share) if up >= 0 else up * MAX_DESCENT
        self.vy += (cy - self.vy) * kv
        avx, avz = self.vx - wx, self.vz - wz
        nx, ny, nz = self.x + self.vx * dt, self.y + self.vy * dt, self.z + self.vz * dt
        if nx < X0 + 2: nx = X0 + 2; self.vx = max(0.0, self.vx)
        if nx > X1 - 2: nx = X1 - 2; self.vx = min(0.0, self.vx)
        if nz < Z0 + 2: nz = Z0 + 2; self.vz = max(0.0, self.vz)
        if nz > Z1 - 2: nz = Z1 - 2; self.vz = min(0.0, self.vz)
        top = pad_y() + CEILING
        if ny > top:
            ny = top; self.vy = min(0.0, self.vy)
        s = surface(nx, nz)
        if ny <= max(s, level) + GEAR:
            self.x, self.z = nx, nz
            if level > s + 0.05:
                self.y = level; self.vx = self.vy = self.vz = 0.0; self.crash = WATER
                return
            self.y = s + GEAR
            hard = -self.vy > LAND_SPEED or math.sqrt(self.vx * self.vx + self.vz * self.vz) > LAND_SLIDE
            self.vx = self.vy = self.vz = 0.0
            if hard:
                self.crash = HARD_LANDING
            else:
                self.landed = True
            return
        self.x, self.y, self.z = nx, ny, nz
        if not dead:
            a = math.sqrt(avx * avx + avz * avz) / MAX_AIR_SPEED
            load = 1 + PAYLOAD_DRAIN * self.payload + SPEED_DRAIN * a * a + CLIMB_DRAIN * max(0.0, self.vy) / MAX_CLIMB
            self.battery = max(0.0, self.battery - dt * load / HOVER_SECONDS)


def fly(run):
    """Flies a set flight: its weather, its load and its list of [steps, forward, right, up, yaw]. Returns rows of numbers."""
    d = Drone(run['payload'])
    rows = []
    step = 0
    for n, fwd, right, up, yaw in run['sticks']:
        for _ in range(n):
            t = step * DT
            above = d.y - ground(d.x, d.z)
            wx, wz = wind_at(run['wind'], run['gust'], run['from'], t, above)
            sx, sz = steady_at(run['wind'], run['from'], above)
            d.step(fwd, right, up, yaw, wx, wz, sx, sz, run['level'])
            step += 1
            if step % run['every'] == 0:
                rows.append([step, d.x, d.y, d.z, d.vx, d.vy, d.vz, d.heading, d.battery, 1 if d.landed else 0, d.crash])
    return rows


# The set flights. Each tries something else: a calm circuit, a loaded flight in the typhoon's wind, a wind too
# strong to fly against, a landing that is too hard, an empty battery, and coming down in a flood.
RUNS = [
    dict(name='a calm circuit', wind=1.5, gust=0.2, **{'from': 60.0}, payload=0.0, level=0.0, every=25,
         sticks=[[250, 0, 0, 1, 0], [500, 1, 0, 0, 0], [250, 0.6, 0, 0.2, 0.5], [200, 0, 1, 0, 0], [300, 0, 0, 0, -1], [400, -1, 0.3, 0, 0], [1500, 0, 0, -0.5, 0]]),
    dict(name='loaded, in the wind after the typhoon', wind=9.0, gust=0.5, **{'from': 250.0}, payload=7.0, level=1.5, every=25,
         sticks=[[450, 0, 0, 1, 0], [750, 1, 0, 0, 0], [500, 0, 0, 0, 0], [90, 0, 0, 0, 1], [90, 0, 0, 0, 1], [750, 1, 0, 0, 0], [300, 0, -1, 0, 0], [2000, 0, 0, -0.6, 0]]),
    dict(name='a wind too strong to fly against', wind=22.0, gust=0.3, **{'from': 270.0}, payload=0.0, level=0.0, every=25,
         sticks=[[400, 0, 0, 1, 0], [90, 0, 0, 0, 1], [90, 0, 0, 0, 1], [600, 1, 0, 0, 0], [300, 0, 0, 0, 0]]),
    dict(name='a landing that is too hard', wind=1.5, gust=0.2, **{'from': 60.0}, payload=4.0, level=0.0, every=10,
         sticks=[[300, 0, 0, 1, 0], [100, 0.5, 0, 0, 0], [800, 0, 0, -1, 0]]),
    dict(name='an empty battery', wind=3.0, gust=0.3, **{'from': 120.0}, payload=8.0, level=0.0, every=500,
         sticks=[[200, 0, 0, 1, 0], [16000, 1, 0, 0, 0.12], [3000, 0, 0, 0, 0]]),
    dict(name='down in the flood', wind=5.0, gust=0.4, **{'from': 200.0}, payload=0.0, level=2.4, every=25,
         sticks=[[300, 0, 0, 1, 0], [400, 1, 0, 0, 0], [100, 0, 0, 0, 0], [1200, 0, 0, -0.5, 0]]),
]
