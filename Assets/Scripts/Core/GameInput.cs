using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BombaBlast
{
    /// <summary>Lectura centralizada de teclado y gamepad (Input System).</summary>
    public static class GameInput
    {
        static readonly List<Vector2Int> heldOrder = new List<Vector2Int>();
        static float navRepeat;
        static Vector2Int lastNav;

        static bool Held(Vector2Int d)
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            bool held = false;
            if (k != null)
            {
                if (d == Vector2Int.up) held = k.upArrowKey.isPressed || k.wKey.isPressed;
                else if (d == Vector2Int.down) held = k.downArrowKey.isPressed || k.sKey.isPressed;
                else if (d == Vector2Int.left) held = k.leftArrowKey.isPressed || k.aKey.isPressed;
                else if (d == Vector2Int.right) held = k.rightArrowKey.isPressed || k.dKey.isPressed;
            }
            if (!held && g != null)
            {
                Vector2 s = g.leftStick.ReadValue() + g.dpad.ReadValue();
                if (d == Vector2Int.up) held = s.y > 0.5f;
                else if (d == Vector2Int.down) held = s.y < -0.5f;
                else if (d == Vector2Int.left) held = s.x < -0.5f;
                else if (d == Vector2Int.right) held = s.x > 0.5f;
            }
            return held;
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        /// <summary>Dirección de movimiento sostenida; prioriza la última tecla pulsada.</summary>
        public static Vector2Int Move()
        {
            foreach (var d in Dirs)
            {
                bool h = Held(d);
                bool inList = heldOrder.Contains(d);
                if (h && !inList) heldOrder.Add(d);
                else if (!h && inList) heldOrder.Remove(d);
            }
            return heldOrder.Count > 0 ? heldOrder[heldOrder.Count - 1] : Vector2Int.zero;
        }

        public static bool BombPressed
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                return (k != null && (k.spaceKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame))
                    || (g != null && g.buttonSouth.wasPressedThisFrame);
            }
        }

        public static bool DetonatePressed
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                return (k != null && (k.zKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.kKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame))
                    || (g != null && g.buttonEast.wasPressedThisFrame);
            }
        }

        public static bool PausePressed
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                return (k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame))
                    || (g != null && g.startButton.wasPressedThisFrame);
            }
        }

        public static bool ConfirmPressed
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                    || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
            }
        }

        public static bool BackPressed
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                return (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame))
                    || (g != null && g.buttonEast.wasPressedThisFrame);
            }
        }

        /// <summary>Navegación de menús con auto-repetición (usa tiempo no escalado).</summary>
        public static Vector2Int MenuNav()
        {
            Vector2Int d = Vector2Int.zero;
            foreach (var dir in Dirs) if (Held(dir)) { d = dir; break; }
            if (d == Vector2Int.zero) { lastNav = d; return d; }
            if (d != lastNav)
            {
                lastNav = d; navRepeat = 0.4f; return d;
            }
            navRepeat -= Time.unscaledDeltaTime;
            if (navRepeat <= 0f) { navRepeat = 0.12f; return d; }
            return Vector2Int.zero;
        }
    }
}
