using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyHop
{
    /// <summary>One place that reads keyboard, gamepad and touch controls.</summary>
    public static class HopInput
    {
        public static Vector2 TouchMove;
        public static bool TouchJumpHeld;
        private static bool _touchJumpDown;

        public static void PressTouchJump() { _touchJumpDown = true; TouchJumpHeld = true; }
        public static void ReleaseTouchJump() { TouchJumpHeld = false; }

        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
            }
            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();
            v += TouchMove;
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static bool JumpDown()
        {
            bool d = _touchJumpDown;
            _touchJumpDown = false;
            var k = Keyboard.current; if (k != null && k.spaceKey.wasPressedThisFrame) d = true;
            var g = Gamepad.current; if (g != null && g.buttonSouth.wasPressedThisFrame) d = true;
            return d;
        }

        public static bool JumpHeld()
        {
            bool h = TouchJumpHeld;
            var k = Keyboard.current; if (k != null && k.spaceKey.isPressed) h = true;
            var g = Gamepad.current; if (g != null && g.buttonSouth.isPressed) h = true;
            return h;
        }

        /// <summary>Camera orbit input in degrees this frame (x = yaw, y = pitch).</summary>
        public static Vector2 Look()
        {
            Vector2 l = Vector2.zero;
            var m = Mouse.current;
            if (m != null && (m.rightButton.isPressed || m.middleButton.isPressed)) l += m.delta.ReadValue() * 0.18f;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.eKey.isPressed) l.x += 110f * Time.deltaTime;
                if (k.qKey.isPressed) l.x -= 110f * Time.deltaTime;
            }
            var g = Gamepad.current;
            if (g != null) { var r = g.rightStick.ReadValue(); l += new Vector2(r.x, -r.y) * 150f * Time.deltaTime; }
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    var sp = t.startPosition.ReadValue();
                    if (sp.x > Screen.width * 0.45f && sp.y > Screen.height * 0.3f) l += t.delta.ReadValue() * 0.22f;
                }
            }
            return l;
        }
    }
}
