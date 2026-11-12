// Where things are, and the panels in the world.
//
// DRONE VR's places (the plan, the models, DroneVR.Core) use their own coordinates: metres, x to the east, y up,
// z to the south. In Unity the same point is (x, y, -z), so north is Unity's forward (+z). GAMA uses
// (x + 240, z + 180) on the ground (see DroneSimulationManager).
//
// Panels: a card with text and buttons, facing the operator. Everything the operator can press (buttons, the
// map table, the supplies) carries a Pointable, which the Pointer finds with a ray from the controller (or the
// mouse) and presses with the trigger. (The panels are TANGHALAN VR's, with a press that knows where it landed.)
using System;
using TMPro;
using UnityEngine;

namespace DroneVR.Vr
{
    public static class Coords
    {
        public static Vector3 ToUnity(double x, double y, double z) { return new Vector3((float)x, (float)y, (float)-z); }
        public static Vector3 ToUnity(P3 p) { return ToUnity(p.X, p.Y, p.Z); }
        public static P3 FromUnity(Vector3 u) { return new P3(u.x, u.y, -u.z); }

        /// <summary>A heading in degrees (0 north, 90 east) as a Unity rotation.</summary>
        public static Quaternion Heading(double degrees) { return Quaternion.Euler(0, (float)degrees, 0); }

        /// <summary>The heading (0 north, 90 east) of a Unity direction.</summary>
        public static float HeadingOf(Vector3 dir) { return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; }

        /// <summary>A Unity direction as DRONE VR's: east, up, south.</summary>
        public static P3 Direction(Vector3 u) { return new P3(u.x, u.y, -u.z); }
    }

    public sealed class Pointable : MonoBehaviour
    {
        public Action OnPress;
        /// <summary>Called instead of OnPress when it matters where the ray landed (the map table).</summary>
        public Action<Vector3> OnPressAt;
        public Func<bool> CanPress;
        /// <summary>How far away it can be pressed from, in metres.</summary>
        public float Reach = 12f;
        public Renderer Highlight;
        public Color Normal = Color.white, Hover = new Color(1f, 0.85f, 0.4f);
        bool hovered;

        public bool Active { get { return isActiveAndEnabled && (CanPress == null || CanPress()); } }

        public void SetHover(bool on)
        {
            if (hovered == on) return;
            hovered = on;
            if (Highlight != null) Ui.SetColor(Highlight, on ? Hover : Normal);
        }

        public void Press(Vector3 at)
        {
            if (!Active) return;
            if (OnPressAt != null) OnPressAt(at);
            else if (OnPress != null) OnPress();
        }
    }

    public static class Palette
    {
        public static readonly Color Navy = Hex(0x16303f), Paper = Hex(0xfffaf0), Cream = Hex(0xfff5df), Gold = Hex(0xffc53d),
            Orange = Hex(0xd9650a), Green = Hex(0x128253), Teal = Hex(0x2a6b8f), Red = Hex(0xc62f22), Grey = Hex(0x4f6572), White = Color.white,
            Yellow = Hex(0xfff3a0), Amber = Hex(0xf2a33a), Sky = Hex(0x9fc3d8), Dark = Hex(0x1f2a32), Lime = Hex(0x5fd35f), Safety = Hex(0xff7a1a);

        public static Color Hex(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        /// <summary>The color of a radio message.</summary>
        public static Color Of(Note n) { return n == Note.Good ? Lime : n == Note.Bad ? Hex(0xff6a5a) : n == Note.Warn ? Gold : Cream; }

        public static string Html(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
    }

    public sealed class Panel
    {
        public readonly GameObject Root;
        public readonly float Width, Height;
        readonly World world;
        readonly Renderer back;

        public Panel(World world, Transform parent, string name, float width, float height, Color background)
        {
            this.world = world;
            Width = width; Height = height;
            Root = new GameObject("Panel " + name);
            Root.transform.SetParent(parent, false);
            back = Ui.Quad(Root.transform, world.FlatMaterial(background), 0, 0, width, height, 0, false);
        }

        public void SetBackground(Color c) { Ui.SetColor(back, c); }

        /// <summary>Some text in a box: x and y are the box's center from the panel's center, in metres.</summary>
        public TextMeshPro Text(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool bold = false)
        {
            return Ui.Text(Root.transform, text, x, y, w, h, size, color, align, bold);
        }

        public Renderer Picture(Texture texture, float x, float y, float w, float h)
        {
            var m = new Material(world.Flat);
            m.SetTexture("_BaseMap", texture); m.mainTexture = texture;
            m.SetColor("_BaseColor", Color.white); m.color = Color.white;
            return Ui.Quad(Root.transform, m, x, y, w, h, -0.003f, false);
        }

        public Renderer Box(float x, float y, float w, float h, Color c)
        {
            return Ui.Quad(Root.transform, world.FlatMaterial(c), x, y, w, h, -0.002f, false);
        }

        public Button Button(string label, float x, float y, float w, float h, Color color, Action onPress)
        {
            var bg = Ui.Quad(Root.transform, world.FlatMaterial(color), x, y, w, h, -0.004f, true);
            var text = Ui.Text(bg.transform.parent, label, x, y, w - 0.03f, h - 0.02f, 0.42f, Palette.White, TextAlignmentOptions.Center, true);
            text.transform.localPosition += new Vector3(0, 0, -0.003f);
            var p = bg.gameObject.AddComponent<Pointable>();
            p.Highlight = bg; p.Normal = color; p.Hover = Color.Lerp(color, Color.white, 0.35f); p.OnPress = onPress;
            return new Button { Pointable = p, Label = text, Background = bg, Color = color };
        }

        /// <summary>Puts the panel at a point, its face turned towards a viewer standing at another point.</summary>
        public void Place(Vector3 at, Vector3 viewer)
        {
            Root.transform.position = at;
            var d = at - viewer; d.y = 0;
            Root.transform.rotation = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
        }

        /// <summary>Puts the panel flat against a wall, its face out along the wall's facing.</summary>
        public void PlaceOnWall(Vector3 at, Vector3 facing)
        {
            Root.transform.position = at;
            facing.y = 0;
            Root.transform.rotation = Quaternion.LookRotation(-facing.normalized, Vector3.up);
        }

        /// <summary>Leans the panel back by some degrees, like a desk console.</summary>
        public void Tilt(float degrees) { Root.transform.rotation = Root.transform.rotation * Quaternion.Euler(degrees, 0, 0); }

        public bool Visible { get { return Root.activeSelf; } set { Root.SetActive(value); } }

        public void Clear()
        {
            for (int i = Root.transform.childCount - 1; i >= 0; i--)
            {
                var c = Root.transform.GetChild(i);
                if (c.GetComponent<Renderer>() != back) UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        public void Destroy() { UnityEngine.Object.Destroy(Root); }
    }

    public sealed class Button
    {
        public Pointable Pointable;
        public TextMeshPro Label;
        public Renderer Background;
        public Color Color;
        public bool Visible { set { Background.gameObject.SetActive(value); Label.gameObject.SetActive(value); } }
        public string Text { set { Label.text = value; } }

        /// <summary>A button that is on (a choice that is made) shows lighter, with a gold edge in its label.</summary>
        public void Chosen(bool on)
        {
            Pointable.Normal = on ? Color.Lerp(Color, Color.white, 0.25f) : Color;
            Ui.SetColor(Background, Pointable.Normal);
            Label.color = on ? Palette.Gold : Palette.White;
        }

        /// <summary>A button that cannot be pressed now is greyed out.</summary>
        public void Enabled(bool on)
        {
            Pointable.enabled = on;
            Ui.SetColor(Background, on ? Pointable.Normal : Palette.Grey);
            Label.color = on ? Palette.White : new Color(1, 1, 1, 0.45f);
        }
    }

    public static class Ui
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static void SetColor(Renderer r, Color c)
        {
            var m = r.material;
            m.SetColor(BaseColor, c); m.color = c;
        }

        /// <summary>A flat rectangle facing the panel's front. Its collider is kept only when it can be pressed.</summary>
        public static Renderer Quad(Transform parent, Material mat, float x, float y, float w, float h, float z, bool pressable)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = q.GetComponent<Collider>();
            UnityEngine.Object.Destroy(col);
            if (pressable) { var b = q.AddComponent<BoxCollider>(); b.size = new Vector3(1, 1, 0.05f); }
            q.transform.SetParent(parent, false);
            q.transform.localPosition = new Vector3(x, y, z);
            q.transform.localScale = new Vector3(w, h, 1);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        public static TextMeshPro Text(Transform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, -0.006f);
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(w, h);
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMax = size; t.fontSizeMin = size * 0.4f;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            return t;
        }

        /// <summary>A small ball of one color, without a collider: a marker.</summary>
        public static Renderer Ball(Transform parent, Material mat, float size)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localScale = Vector3.one * size;
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        /// <summary>An upright stick of one color, without a collider, from its foot up to a height.</summary>
        public static Renderer Stick(Transform parent, Material mat, float width)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(parent, false);
            s.transform.localScale = new Vector3(width, 1, width);
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        public static void StickBetween(Renderer stick, Vector3 foot, float height)
        {
            var t = stick.transform;
            t.position = foot + Vector3.up * (height / 2);
            var s = t.localScale; s.y = Mathf.Max(0.0005f, height) / (t.parent != null ? t.parent.lossyScale.y : 1); t.localScale = s;
        }

        /// <summary>A line through some points, of one color and width.</summary>
        public static LineRenderer Line(Transform parent, Material mat, float width)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = mat; l.useWorldSpace = true; l.startWidth = l.endWidth = width;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false;
            l.numCapVertices = 2; l.positionCount = 0;
            return l;
        }
    }

    /// <summary>Finds what the operator points at, lights it up, and presses it when they pull the trigger.</summary>
    public sealed class Pointer : MonoBehaviour
    {
        public Rig Rig;
        /// <summary>Off while the operator looks through the drone's goggles: then the trigger is the camera's.</summary>
        public bool On = true;
        /// <summary>The frame in which the trigger pressed something: that press was not meant for the drone.</summary>
        public static int UsedFrame = -1;
        /// <summary>Where the ray meets something, and whether it meets anything.</summary>
        public Vector3 HitPoint { get; private set; }
        public Collider HitCollider { get; private set; }
        Pointable hovered;
        Transform dot;
        int lastHand = 1;

        void Start()
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(d.GetComponent<Collider>());
            d.name = "Pointer dot";
            d.transform.localScale = Vector3.one * 0.025f;
            var r = d.GetComponent<Renderer>();
            r.sharedMaterial = new Material(Resources.Load<Material>(World.MaterialPath + "DroneFlat"));
            r.sharedMaterial.SetColor("_BaseColor", Palette.Gold);
            dot = d.transform;
        }

        void Update()
        {
            if (Rig == null || Rig.Head == null) return;
            int hand;
            bool pressed = Rig.Pressed(out hand);
            if (pressed && hand != 0) lastHand = hand;
            Pointable found = null;
            HitCollider = null;
            if (On)
            {
                var ray = Rig.PointingRay(Rig.Desktop ? 0 : lastHand);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 15f, ~0, QueryTriggerInteraction.Collide))
                {
                    var p = hit.collider.GetComponentInParent<Pointable>();
                    if (p != null && p.Active && hit.distance <= p.Reach) found = p;
                    HitPoint = hit.point; HitCollider = hit.collider;
                    dot.gameObject.SetActive(true);
                    dot.position = hit.point;
                    dot.localScale = Vector3.one * (found != null ? 0.04f : 0.02f);
                }
                else dot.gameObject.SetActive(false);
            }
            else dot.gameObject.SetActive(false);
            if (found != hovered)
            {
                if (hovered != null) hovered.SetHover(false);
                hovered = found;
                if (hovered != null) { hovered.SetHover(true); Rig.Haptic(0.15f, 0.03f); }
            }
            if (pressed && hovered != null) { Sfx.Play("click"); UsedFrame = Time.frameCount; hovered.Press(HitPoint); }
        }
    }

    /// <summary>Something flying from one place to another in a short arc: a crate going to the drone.</summary>
    public sealed class Flights : MonoBehaviour
    {
        sealed class Flight
        {
            public Transform T; public Vector3 From; public Func<Vector3> To;
            public float K, Duration, S0, S1; public Action Done;
        }

        static Flights instance;
        readonly System.Collections.Generic.List<Flight> list = new System.Collections.Generic.List<Flight>();

        public static void Fly(Transform t, Func<Vector3> to, float scaleFrom, float scaleTo, Action done = null, float seconds = 0.4f)
        {
            if (instance == null) instance = new GameObject("Flights").AddComponent<Flights>();
            instance.list.RemoveAll(f => f.T == t);
            instance.list.Add(new Flight { T = t, From = t.position, To = to, Duration = seconds, S0 = scaleFrom, S1 = scaleTo, Done = done });
        }

        void Update()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                if (f.T == null) { list.RemoveAt(i); continue; }
                f.K = Mathf.Min(1, f.K + Time.deltaTime / f.Duration);
                float e = f.K * f.K * (3 - 2 * f.K);
                f.T.position = Vector3.Lerp(f.From, f.To(), e) + Vector3.up * Mathf.Sin(f.K * Mathf.PI) * 0.25f;
                f.T.localScale = Vector3.one * Mathf.Lerp(f.S0, f.S1, e);
                if (f.K >= 1) { list.RemoveAt(i); if (f.Done != null) f.Done(); }
            }
        }
    }
}
