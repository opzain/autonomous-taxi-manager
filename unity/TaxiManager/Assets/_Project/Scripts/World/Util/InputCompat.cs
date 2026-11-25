using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TaxiSim.World
{
    /// <summary>Keys used by the 3D world demo (keyboard fallback for the ESP32 console).</summary>
    public enum TsKey
    {
        W, A, S, D, Up, Down, Left, Right,
        Q, E, R, F, Z, X, H, C, T, M, B, L, P, O, U, I, V, G, N, J, K, Y,
        Tab, Space, Escape, LeftShift,
        LeftBracket, RightBracket,
        Alpha1, Alpha2, Alpha3, Alpha4,
        F1, F2, F3, F4, F5, F9,
        // dispatch game (keyboard stand-in for the console buttons)
        Enter, Backspace, Delete, Comma, Period, Slash, Minus, EqualSign
    }

    /// <summary>
    /// Works with both the new Input System (Unity 6 default) and the legacy Input Manager,
    /// so the project runs whatever "Active Input Handling" is set to.
    /// </summary>
    public static class InputCompat
    {
        public static bool Held(TsKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[ToKey(key)].isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(ToKeyCode(key));
#else
            return false;
#endif
        }

        public static bool Pressed(TsKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[ToKey(key)].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(ToKeyCode(key));
#else
            return false;
#endif
        }

        /// <summary>Mouse wheel in "notches" (positive = away from the user).</summary>
        public static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return 0f;
            float s = m.scroll.ReadValue().y;
            return Mathf.Abs(s) > 20f ? s / 120f : s;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        public static Vector2 MouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m == null ? Vector2.zero : m.delta.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }

        public static bool RightMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        public static bool MiddleMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(2);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Key ToKey(TsKey k)
        {
            switch (k)
            {
                case TsKey.W: return Key.W;
                case TsKey.A: return Key.A;
                case TsKey.S: return Key.S;
                case TsKey.D: return Key.D;
                case TsKey.Up: return Key.UpArrow;
                case TsKey.Down: return Key.DownArrow;
                case TsKey.Left: return Key.LeftArrow;
                case TsKey.Right: return Key.RightArrow;
                case TsKey.Q: return Key.Q;
                case TsKey.E: return Key.E;
                case TsKey.R: return Key.R;
                case TsKey.F: return Key.F;
                case TsKey.Z: return Key.Z;
                case TsKey.X: return Key.X;
                case TsKey.H: return Key.H;
                case TsKey.C: return Key.C;
                case TsKey.T: return Key.T;
                case TsKey.M: return Key.M;
                case TsKey.B: return Key.B;
                case TsKey.L: return Key.L;
                case TsKey.P: return Key.P;
                case TsKey.O: return Key.O;
                case TsKey.U: return Key.U;
                case TsKey.I: return Key.I;
                case TsKey.V: return Key.V;
                case TsKey.G: return Key.G;
                case TsKey.N: return Key.N;
                case TsKey.J: return Key.J;
                case TsKey.K: return Key.K;
                case TsKey.Y: return Key.Y;
                case TsKey.Tab: return Key.Tab;
                case TsKey.Space: return Key.Space;
                case TsKey.Escape: return Key.Escape;
                case TsKey.LeftShift: return Key.LeftShift;
                case TsKey.LeftBracket: return Key.LeftBracket;
                case TsKey.RightBracket: return Key.RightBracket;
                case TsKey.Alpha1: return Key.Digit1;
                case TsKey.Alpha2: return Key.Digit2;
                case TsKey.Alpha3: return Key.Digit3;
                case TsKey.Alpha4: return Key.Digit4;
                case TsKey.F1: return Key.F1;
                case TsKey.F2: return Key.F2;
                case TsKey.F3: return Key.F3;
                case TsKey.F4: return Key.F4;
                case TsKey.F5: return Key.F5;
                case TsKey.F9: return Key.F9;
                case TsKey.Enter: return Key.Enter;
                case TsKey.Backspace: return Key.Backspace;
                case TsKey.Delete: return Key.Delete;
                case TsKey.Comma: return Key.Comma;
                case TsKey.Period: return Key.Period;
                case TsKey.Slash: return Key.Slash;
                case TsKey.Minus: return Key.Minus;
                case TsKey.EqualSign: return Key.Equals;
                default: return Key.None;
            }
        }
#else
        private static KeyCode ToKeyCode(TsKey k)
        {
            switch (k)
            {
                case TsKey.Up: return KeyCode.UpArrow;
                case TsKey.Down: return KeyCode.DownArrow;
                case TsKey.Left: return KeyCode.LeftArrow;
                case TsKey.Right: return KeyCode.RightArrow;
                case TsKey.Tab: return KeyCode.Tab;
                case TsKey.Space: return KeyCode.Space;
                case TsKey.Escape: return KeyCode.Escape;
                case TsKey.LeftShift: return KeyCode.LeftShift;
                case TsKey.LeftBracket: return KeyCode.LeftBracket;
                case TsKey.RightBracket: return KeyCode.RightBracket;
                case TsKey.Alpha1: return KeyCode.Alpha1;
                case TsKey.Alpha2: return KeyCode.Alpha2;
                case TsKey.Alpha3: return KeyCode.Alpha3;
                case TsKey.Alpha4: return KeyCode.Alpha4;
                case TsKey.F1: return KeyCode.F1;
                case TsKey.F2: return KeyCode.F2;
                case TsKey.F3: return KeyCode.F3;
                case TsKey.F4: return KeyCode.F4;
                case TsKey.F5: return KeyCode.F5;
                case TsKey.F9: return KeyCode.F9;
                case TsKey.Enter: return KeyCode.Return;
                case TsKey.Backspace: return KeyCode.Backspace;
                case TsKey.Delete: return KeyCode.Delete;
                case TsKey.Comma: return KeyCode.Comma;
                case TsKey.Period: return KeyCode.Period;
                case TsKey.Slash: return KeyCode.Slash;
                case TsKey.Minus: return KeyCode.Minus;
                case TsKey.EqualSign: return KeyCode.Equals;
                default: return (KeyCode)System.Enum.Parse(typeof(KeyCode), k.ToString());
            }
        }
#endif
    }
}
