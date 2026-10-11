// ============================================================================
//  InputHelper.cs
//  Second STATIC class: hides the difference between Unity's old Input
//  Manager and the new Input System package, so the controls work in any
//  project (Unity 2021 -> Unity 6) without changing Project Settings.
//  The compiler symbols are defined by Unity according to
//  "Project Settings > Player > Active Input Handling".
//
//  [H4] Adds named game actions (GameKey) with key bindings in ONE place,
//  so gameplay code asks "was Eat pressed?" instead of "was E pressed?".
// ============================================================================

using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThodupunuriAnshul.EcoSim.Utilities
{
    /// <summary>[H4] Every discrete action of the game. Bindings are defined in InputHelper.</summary>
    public enum GameKey
    {
        Sprint,       // Left / right Shift
        Eat,          // E
        Mate,         // F
        Thump,        // Space
        Dig,          // B
        Pause,        // Escape or P
        Confirm,      // Enter
        OrbitLeft,    // Z
        OrbitRight    // C
    }

    public static class InputHelper
    {
        // ====================================================================
        //  Axes (Homework 3)
        // ====================================================================

        /// <summary>WASD / arrow keys as a 2D vector (x = right, y = forward).</summary>
        public static Vector2 GetMoveAxis()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#else
            return Vector2.zero;
#endif
        }

        /// <summary>Q / E as -1 / +1 (orbit of the sandbox observation camera).</summary>
        public static float GetRotateAxis()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return 0f;
            return (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
#else
            return 0f;
#endif
        }

        /// <summary>Mouse wheel (positive = zoom in).</summary>
        public static float GetZoomDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse == null ? 0f : mouse.scroll.ReadValue().y / 120f;   // new system reports ~120 per notch
#else
            return 0f;
#endif
        }

        /// <summary>[H4] Horizontal mouse movement while the RIGHT button is held (camera orbit).</summary>
        public static float GetMouseOrbitDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1) ? Input.GetAxis("Mouse X") : 0f;
#elif ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return 0f;
            return mouse.delta.ReadValue().x * 0.1f;   // pixels -> roughly the old "Mouse X" units
#else
            return 0f;
#endif
        }

        // ====================================================================
        //  [H4] Named actions
        // ====================================================================

        /// <summary>True while the key of this action is held down.</summary>
        public static bool GetKey(GameKey action)
        {
            return IsActionActive(action, false);
        }

        /// <summary>True only during the frame the key of this action is pressed.</summary>
        public static bool GetKeyDown(GameKey action)
        {
            return IsActionActive(action, true);
        }

        /// <summary>Text shown in the HUD for this action ("E", "Space"...).</summary>
        public static string GetKeyLabel(GameKey action)
        {
            switch (action)
            {
                case GameKey.Sprint:     return "Shift";
                case GameKey.Eat:        return "E";
                case GameKey.Mate:       return "F";
                case GameKey.Thump:      return "Space";
                case GameKey.Dig:        return "B";
                case GameKey.Pause:      return "Esc";
                case GameKey.Confirm:    return "Enter";
                case GameKey.OrbitLeft:  return "Z";
                case GameKey.OrbitRight: return "C";
                default:                 return "?";
            }
        }

        private static bool IsActionActive(GameKey action, bool pressedThisFrameOnly)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            GetLegacyBindings(action, out KeyCode primary, out KeyCode secondary);
            return pressedThisFrameOnly
                ? Input.GetKeyDown(primary) || (secondary != KeyCode.None && Input.GetKeyDown(secondary))
                : Input.GetKey(primary) || (secondary != KeyCode.None && Input.GetKey(secondary));
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            GetInputSystemBindings(action, out Key primary, out Key secondary);
            return IsKeyActive(keyboard, primary, pressedThisFrameOnly)
                || IsKeyActive(keyboard, secondary, pressedThisFrameOnly);
#else
            return false;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static void GetLegacyBindings(GameKey action, out KeyCode primary, out KeyCode secondary)
        {
            secondary = KeyCode.None;
            switch (action)
            {
                case GameKey.Sprint:     primary = KeyCode.LeftShift; secondary = KeyCode.RightShift; break;
                case GameKey.Eat:        primary = KeyCode.E; break;
                case GameKey.Mate:       primary = KeyCode.F; break;
                case GameKey.Thump:      primary = KeyCode.Space; break;
                case GameKey.Dig:        primary = KeyCode.B; break;
                case GameKey.Pause:      primary = KeyCode.Escape; secondary = KeyCode.P; break;
                case GameKey.Confirm:    primary = KeyCode.Return; secondary = KeyCode.KeypadEnter; break;
                case GameKey.OrbitLeft:  primary = KeyCode.Z; break;
                case GameKey.OrbitRight: primary = KeyCode.C; break;
                default:                 primary = KeyCode.None; break;
            }
        }
#elif ENABLE_INPUT_SYSTEM
        private static void GetInputSystemBindings(GameKey action, out Key primary, out Key secondary)
        {
            secondary = Key.None;
            switch (action)
            {
                case GameKey.Sprint:     primary = Key.LeftShift; secondary = Key.RightShift; break;
                case GameKey.Eat:        primary = Key.E; break;
                case GameKey.Mate:       primary = Key.F; break;
                case GameKey.Thump:      primary = Key.Space; break;
                case GameKey.Dig:        primary = Key.B; break;
                case GameKey.Pause:      primary = Key.Escape; secondary = Key.P; break;
                case GameKey.Confirm:    primary = Key.Enter; secondary = Key.NumpadEnter; break;
                case GameKey.OrbitLeft:  primary = Key.Z; break;
                case GameKey.OrbitRight: primary = Key.C; break;
                default:                 primary = Key.None; break;
            }
        }

        private static bool IsKeyActive(Keyboard keyboard, Key key, bool pressedThisFrameOnly)
        {
            if (key == Key.None) return false;
            var control = keyboard[key];
            return pressedThisFrameOnly ? control.wasPressedThisFrame : control.isPressed;
        }
#endif
    }
}
