// The loading dock of the operations center, on its east wall: a shelf of supplies, and a miniature of the
// drone on its cradle. The operator takes a crate from the shelf and hangs it on the miniature's hooks; the
// ground crew at the pad hangs the same on the drone. What hangs on the miniature is what the drone carries.
//
// Two ways to do it:
//   - grab a crate (grip), carry it to the miniature and let go near it: it is hung on a hook;
//   - point at a crate and pull the trigger: it flies to the miniature by itself.
// Point at a crate under the miniature and pull the trigger to take it off. The rules say no (and the radio
// says why) when the drone is not on the pad, when three hooks are taken, or when 8 kg would be too heavy.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DroneVR.Vr
{
    public sealed class Dock
    {
        /// <summary>The crates and the drone on the dock are this much smaller than the real ones.</summary>
        public const float Small = 0.5f;
        /// <summary>A crate let go this near the miniature is hung on it.</summary>
        const float HangWithin = 0.45f;

        readonly DroneApp app;
        readonly World world;
        readonly Transform root;
        readonly DroneModel drone;
        readonly Panel label, shelfLabel;
        readonly TextMeshPro labelText, shelfText;
        readonly List<GameObject> crates = new List<GameObject>();
        readonly List<GameObject> hung = new List<GameObject>();
        string hungKey = "";
        Flight flight;

        Lang L { get { return app.Lang; } }

        public Dock(DroneApp app)
        {
            this.app = app; world = app.World;
            root = new GameObject("Loading dock").transform;
            drone = new DroneModel(world, root, Small);
            drone.Root.transform.position = world.Point("dock");
            drone.Root.transform.rotation = Quaternion.LookRotation(world.Facing("dock"), Vector3.up);
            label = new Panel(world, root, "Dock", 1.3f, 0.62f, Palette.Navy);
            label.PlaceOnWall(world.Point("dock_label"), world.Facing("dock_label"));
            labelText = label.Text("", 0, 0, 1.22f, 0.56f, 0.3f, Palette.Cream);
            shelfLabel = new Panel(world, root, "Shelf", 1.3f, 0.3f, Palette.Paper);
            shelfLabel.PlaceOnWall(world.Point("shelf_label"), world.Facing("shelf_label"));
            shelfText = shelfLabel.Text("", 0, 0, 1.22f, 0.26f, 0.32f, Palette.Navy, TextAlignmentOptions.Center, true);
            Show(null);
        }

        /// <summary>Puts the mission's supplies on the shelf (none between missions, or in a mission that needs none).</summary>
        public void Show(Flight f)
        {
            flight = f;
            foreach (var c in crates) Object.Destroy(c);
            crates.Clear();
            hungKey = "-";
            bool any = f != null && f.Mission.Offers.Length > 0;
            label.Visible = shelfLabel.Visible = any;
            drone.Show(true);
            if (!any) { Hang(new string[0]); return; }
            foreach (var item in f.Mission.Offers)
            {
                var spot = world.Point("shelf_" + item);
                // three of each, side by side
                for (int k = 0; k < 3; k++) crates.Add(Crate(item, spot + Vector3.Cross(world.Facing("shelf_" + item), Vector3.up) * ((k - 1) * 0.2f)));
            }
            Refresh();
        }

        GameObject Crate(string item, Vector3 at)
        {
            var c = world.Spawn("supply_" + item, root);
            c.transform.localScale = Vector3.one * Small;
            c.transform.position = at;
            var home = at;
            var box = c.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.15f, 0); box.size = new Vector3(0.4f, 0.32f, 0.3f);
            // the trigger: it flies to the drone by itself
            var p = c.AddComponent<Pointable>();
            p.Reach = 6;
            p.CanPress = () => flight != null && !flight.Mission.Over;
            p.OnPress = () => Load(item, c.transform, home);
            // the grip: carry it there by hand
            var body = c.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var grab = c.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.selectExited.AddListener(args =>
            {
                bool near = Vector3.Distance(c.transform.position, drone.Root.transform.position) < HangWithin;
                if (near) Load(item, c.transform, home);
                else Flights.Fly(c.transform, () => home, Small, Small);
            });
            return c;
        }

        void Load(string item, Transform crate, Vector3 home)
        {
            if (flight == null) return;
            if (flight.Mission.Load(item, flight.Drone))
            {
                Sfx.Play("pop");
                app.Say(Note.Good, "load.on", "supply." + item);
                // the crate goes to the drone's hooks, and a new one takes its place on the shelf
                Flights.Fly(crate, () => drone.Root.transform.position + Vector3.down * 0.15f, Small, Small * 0.6f, () =>
                {
                    crate.position = home; crate.localScale = Vector3.one * Small;
                }, 0.35f);
            }
            else
            {
                Sfx.Play("sad", 0.5f);
                Flights.Fly(crate, () => home, Small, Small);
            }
            Refresh();
        }

        void Hang(IList<string> items)
        {
            string key = string.Join(",", items);
            if (key == hungKey) return;
            hungKey = key;
            foreach (var h in hung) Object.Destroy(h);
            hung.Clear();
            var hook = World.Find(drone.Root, "A__hook");
            for (int k = 0; k < items.Count; k++)
            {
                var item = items[k];
                var m = world.Spawn("supply_" + item, hook != null ? hook : drone.Root.transform);
                m.transform.localPosition = new Vector3((k - (items.Count - 1) / 2f) * 0.17f, -0.32f, 0);
                var box = m.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.15f, 0); box.size = new Vector3(0.4f, 0.32f, 0.3f);
                var p = m.AddComponent<Pointable>();
                p.Reach = 6;
                p.CanPress = () => flight != null && !flight.Mission.Over;
                p.OnPress = () =>
                {
                    if (flight.Mission.Unload(item, flight.Drone)) { Sfx.Play("drop"); app.Say(Note.Info, "load.off", "supply." + item); }
                    else app.Say(Note.Warn, "load.pad");
                    Refresh();
                };
                hung.Add(m);
            }
        }

        public void Tick()
        {
            if (flight == null) return;
            Hang(flight.Mission.Cargo.Items);
        }

        public void Refresh()
        {
            if (flight == null) return;
            // the shelf's label names what is on it, top board then bottom, with the weight of each
            var names = new List<string>();
            foreach (var item in flight.Mission.Offers) names.Add(Strings.Format("supply.kg", L, Strings.Get("supply." + item, L), TownPlan.Supply(item).Kg));
            shelfText.text = Strings.Get("place.shelf", L) + "\n<size=70%>" + string.Join("   ", names) + "</size>";
            var c = flight.Mission.Cargo;
            labelText.text = "<b>" + Strings.Get("place.dock", L) + "</b>\n"
                + (c.Items.Count == 0 ? Strings.Get("console.cargo.none", L)
                    : Strings.Format("console.cargo", L, string.Join(", ", c.Items.ConvertAll(i => Strings.Get("supply." + i, L))), c.Kg.ToString("0"), DroneSpec.MaxPayloadKg.ToString("0"))) + "\n"
                + "<size=80%>" + Strings.Get("console.load.how", L) + " " + Strings.Get("dock.crew", L) + "</size>";
        }
    }
}
