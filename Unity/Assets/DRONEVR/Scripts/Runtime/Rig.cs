// The operator's body: the SIMPLE template's FPS Player (an XR Origin with a camera and two controllers), seen
// from DRONE VR. It moves the operator (to a spot in the operations center, or into the drone when they put
// the goggles on), reads the buttons and the sticks of the drone's radio control, and, with no headset, lets
// them play with the keyboard and the mouse.
//
// The radio control is the two controllers, held as on a real drone's transmitter (the usual "mode 2"):
//   left stick   up and down, and turn                      right stick   forward, back and sideways
//   right trigger  the camera (a photograph, a mark)        right grip    let a package go
//   A   the goggles on or off                               B   the thermal camera
//   X   hand the drone back to the autopilot (Hybrid)       Y   call the drone home
// While the operator holds the radio control (Flying), the sticks fly the drone and do not walk.
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace DroneVR.Vr
{
    public sealed class Rig : MonoBehaviour
    {
        public XROrigin Origin { get; private set; }
        public Camera Head { get; private set; }
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }
        /// <summary>True when no headset is running: keyboard and mouse.</summary>
        public bool Desktop { get; private set; }
        /// <summary>The operator holds the drone's radio control: the sticks fly the drone.</summary>
        public bool Flying;
        /// <summary>The view rides on the drone (the goggles are on).</summary>
        public bool Following { get; private set; }

        CharacterController body;
        GameObject locomotion;
        Transform floorOffset;
        float offsetBase;
        float yaw, pitch, lookYaw;
        Renderer fade;
        float fadeNow, fadeWant;
        Vector3 followEye;
        float followYaw;
        bool canWalk = true;

        InputAction trigger, triggerLeft, grip, buttonA, buttonB, buttonX, buttonY, mousePress, leftStick, rightStick;

        /// <summary>A stick is read as zero below this, so a resting thumb does not move the drone.</summary>
        const float DeadZone = 0.12f;

        public void Init()
        {
            Origin = FindFirstObjectByType<XROrigin>();
            if (Origin == null) { Debug.LogError("DRONE VR: no XR Origin (FPS Player) in the scene."); return; }
            Head = Origin.Camera != null ? Origin.Camera : Camera.main;
            body = Origin.GetComponent<CharacterController>();
            floorOffset = Origin.CameraFloorOffsetObject != null ? Origin.CameraFloorOffsetObject.transform : Head.transform.parent;
            offsetBase = floorOffset.localPosition.y;
            foreach (var t in Origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Right Controller") RightHand = t;
                else if (t.name == "Left Controller") LeftHand = t;
                else if (t.name == "Locomotion") locomotion = t.gameObject;
            }

            Desktop = !XRSettings.isDeviceActive;
            if (Desktop) StartDesktop();

            trigger = Button("trigger", "<XRController>{RightHand}/triggerPressed", "<XRController>{RightHand}/{TriggerButton}");
            triggerLeft = Button("triggerLeft", "<XRController>{LeftHand}/triggerPressed", "<XRController>{LeftHand}/{TriggerButton}");
            grip = Button("grip", "<XRController>{RightHand}/gripPressed", "<XRController>{RightHand}/{GripButton}", "<Keyboard>/g");
            buttonA = Button("A", "<XRController>{RightHand}/primaryButton", "<XRController>{RightHand}/{PrimaryButton}", "<Keyboard>/tab");
            buttonB = Button("B", "<XRController>{RightHand}/secondaryButton", "<XRController>{RightHand}/{SecondaryButton}", "<Keyboard>/t");
            buttonX = Button("X", "<XRController>{LeftHand}/primaryButton", "<XRController>{LeftHand}/{PrimaryButton}", "<Keyboard>/h");
            buttonY = Button("Y", "<XRController>{LeftHand}/secondaryButton", "<XRController>{LeftHand}/{SecondaryButton}", "<Keyboard>/y");
            mousePress = Button("mouse", "<Mouse>/leftButton");
            leftStick = Stick("leftStick", "<XRController>{LeftHand}/{Primary2DAxis}", "<XRController>{LeftHand}/thumbstick");
            rightStick = Stick("rightStick", "<XRController>{RightHand}/{Primary2DAxis}", "<XRController>{RightHand}/thumbstick");

            // a black sphere around the head, to fade the view while the operator is moved
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(f.GetComponent<Collider>());
            f.name = "Fade"; f.transform.SetParent(Head.transform, false); f.transform.localScale = Vector3.one * 0.6f;
            fade = f.GetComponent<Renderer>();
            fade.sharedMaterial = new Material(Resources.Load<Material>(World.MaterialPath + "DroneWater"));
            fade.sharedMaterial.SetColor("_BaseColor", new Color(0, 0, 0, 0));
            fade.sharedMaterial.SetFloat("_Cull", 0); // seen from inside
            fade.enabled = false;
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        static InputAction Stick(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Value, null, null, null, "Vector2");
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        void OnDestroy()
        {
            foreach (var a in new[] { trigger, triggerLeft, grip, buttonA, buttonB, buttonX, buttonY, mousePress, leftStick, rightStick }) if (a != null) a.Dispose();
        }

        // ---------- what the operator presses ----------

        /// <summary>The trigger (or the mouse button) went down this frame. hand: 1 right, -1 left, 0 mouse.</summary>
        public bool Pressed(out int hand)
        {
            hand = 0;
            if (trigger.WasPressedThisFrame()) { hand = 1; return true; }
            if (triggerLeft.WasPressedThisFrame()) { hand = -1; return true; }
            if (Desktop && mousePress.WasPressedThisFrame()) { hand = 0; return true; }
            return false;
        }

        /// <summary>The right trigger (or the mouse button), for the drone's camera.</summary>
        public bool CameraPressed { get { return trigger.WasPressedThisFrame() || (Desktop && mousePress.WasPressedThisFrame()); } }
        public bool GripPressed { get { return grip.WasPressedThisFrame(); } }
        public bool APressed { get { return buttonA.WasPressedThisFrame(); } }
        public bool BPressed { get { return buttonB.WasPressedThisFrame(); } }
        public bool XPressed { get { return buttonX.WasPressedThisFrame(); } }
        public bool YPressed { get { return buttonY.WasPressedThisFrame(); } }

        /// <summary>What the drone's sticks say now (zero unless the operator holds the radio control).</summary>
        public DroneInput Sticks()
        {
            if (!Flying) return DroneInput.None;
            Vector2 l = Clean(leftStick.ReadValue<Vector2>()), r = Clean(rightStick.ReadValue<Vector2>());
            var input = new DroneInput(r.y, r.x, l.y, l.x);
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.Forward += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.Forward -= 1;
                if (kb.dKey.isPressed) input.Right += 1;
                if (kb.aKey.isPressed) input.Right -= 1;
                if (kb.eKey.isPressed || kb.rightArrowKey.isPressed) input.Yaw += 1;
                if (kb.qKey.isPressed || kb.leftArrowKey.isPressed) input.Yaw -= 1;
                if (kb.rKey.isPressed || kb.pageUpKey.isPressed) input.Up += 1;
                if (kb.fKey.isPressed || kb.pageDownKey.isPressed) input.Up -= 1;
                // a gentle touch with Shift, for drops and photographs
                if (kb.leftShiftKey.isPressed) { input.Forward *= 0.35; input.Right *= 0.35; input.Up *= 0.5; input.Yaw *= 0.5; }
            }
            return input;
        }

        static Vector2 Clean(Vector2 v)
        {
            float m = v.magnitude;
            if (m < DeadZone) return Vector2.zero;
            return v / m * Mathf.Min(1, (m - DeadZone) / (1 - DeadZone));
        }

        public Ray PointingRay(int hand)
        {
            if (Desktop || hand == 0)
            {
                var m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                return Head.ScreenPointToRay(m);
            }
            var t = hand < 0 ? LeftHand : RightHand;
            if (t == null) return new Ray(Head.transform.position, Head.transform.forward);
            return new Ray(t.position, t.forward);
        }

        public void Haptic(float amplitude, float seconds)
        {
            if (Desktop) return;
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var dev = InputDevices.GetDeviceAtXRNode(node);
                if (dev.isValid) dev.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
            }
        }

        // ---------- where the operator is ----------

        /// <summary>The point on the floor under the operator's head.</summary>
        public Vector3 Feet
        {
            get { var p = Head.transform.position; return new Vector3(p.x, Origin.transform.position.y, p.z); }
        }

        public Vector3 Forward
        {
            get { var f = Head.transform.forward; f.y = 0; return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; }
        }

        /// <summary>Moves the operator so that they stand at a point, looking along a direction.</summary>
        public void Place(Vector3 feet, Vector3 look)
        {
            StopFollowing();
            if (body != null) body.enabled = false;
            look.y = 0;
            if (look.sqrMagnitude > 1e-4f)
            {
                float turn = Vector3.SignedAngle(Forward, look.normalized, Vector3.up);
                Origin.transform.RotateAround(Head.transform.position, Vector3.up, turn);
                if (Desktop) yaw = Origin.transform.eulerAngles.y;
            }
            var head = Head.transform.position;
            var delta = new Vector3(feet.x - head.x, 0, feet.z - head.z);
            Origin.transform.position += delta;
            var p = Origin.transform.position; p.y = feet.y; Origin.transform.position = p;
            if (body != null) body.enabled = canWalk;
        }

        /// <summary>The view rides on the drone: the eyes at a point, looking the way the drone faces (degrees).</summary>
        public void Follow(Vector3 eye, float heading)
        {
            if (!Following) { lookYaw = 0; pitch = 0; if (body != null) body.enabled = false; if (locomotion != null) locomotion.SetActive(false); }
            Following = true; followEye = eye; followYaw = heading;
        }

        public void StopFollowing()
        {
            if (!Following) return;
            Following = false;
            if (Desktop) { pitch = 0; Head.transform.localRotation = Quaternion.identity; }
            if (locomotion != null) locomotion.SetActive(canWalk);
            if (body != null) body.enabled = canWalk;
        }

        /// <summary>Walking on or off (the stick, teleporting, turning).</summary>
        public void CanMove(bool on)
        {
            canWalk = on;
            if (Following) return;
            if (locomotion != null) locomotion.SetActive(on);
            if (body != null) body.enabled = on;
        }

        public void FadeOut() { fadeWant = 1; }
        public void FadeIn() { fadeWant = 0; }
        public bool Faded { get { return fadeNow > 0.99f; } }

        void Update()
        {
            if (Origin == null) return;
            var lp = floorOffset.localPosition; lp.y = offsetBase; floorOffset.localPosition = lp;
            fadeNow = Mathf.MoveTowards(fadeNow, fadeWant, Time.deltaTime * 3f);
            fade.enabled = fadeNow > 0.001f;
            if (fade.enabled) fade.sharedMaterial.SetColor("_BaseColor", new Color(0, 0, 0, fadeNow));
            if (Desktop) DesktopUpdate();
        }

        void LateUpdate()
        {
            if (Origin == null || !Following) return;
            float y = followYaw + (Desktop ? lookYaw : 0);
            Origin.transform.rotation = Quaternion.Euler(0, y, 0);
            if (Desktop) Head.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Origin.transform.position += followEye - Head.transform.position;
        }

        // ---------- no headset: keyboard and mouse ----------

        void StartDesktop()
        {
            // the headset's pose drivers would hold the camera still; with no headset the mouse turns it
            foreach (var c in Head.GetComponents<Behaviour>())
                if (c.GetType().Name.Contains("TrackedPoseDriver")) c.enabled = false;
            var lp = Head.transform.localPosition; lp.y = 1.6f; Head.transform.localPosition = lp;
            Head.transform.localRotation = Quaternion.identity;
            yaw = Origin.transform.eulerAngles.y;
            if (RightHand != null) RightHand.gameObject.SetActive(false);
            if (LeftHand != null) LeftHand.gameObject.SetActive(false);
            Debug.Log("DRONE VR: no headset found; playing with the keyboard and the mouse.");
        }

        void DesktopUpdate()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                if (Following) lookYaw = Mathf.Clamp(lookYaw + d.x * 0.15f, -120, 120); else yaw += d.x * 0.15f;
                pitch = Mathf.Clamp(pitch - d.y * 0.15f, -80, 80);
            }
            if (Following) return;
            if (kb != null && !Flying)
            {
                if (kb.leftArrowKey.isPressed || kb.qKey.isPressed) yaw -= 90 * Time.deltaTime;
                if (kb.rightArrowKey.isPressed || kb.eKey.isPressed) yaw += 90 * Time.deltaTime;
            }
            Origin.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Head.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (!canWalk || Flying || kb == null || body == null || !body.enabled) return;
            var v = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += Vector3.forward;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v += Vector3.back;
            if (kb.aKey.isPressed) v += Vector3.left;
            if (kb.dKey.isPressed) v += Vector3.right;
            v = Origin.transform.rotation * Vector3.ClampMagnitude(v, 1) * 2.2f;
            body.Move((v + Vector3.down * 3f) * Time.deltaTime);
        }
    }
}
