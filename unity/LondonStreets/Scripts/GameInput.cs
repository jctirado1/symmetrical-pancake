using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LondonStreets
{
    /// <summary>
    /// Reads the keyboard and mouse. Works whether your project uses Unity's new
    /// Input System or the old Input Manager (Edit > Project Settings > Player >
    /// Active Input Handling), so you don't have to set anything up.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        static bool Down(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        public static bool Forward => Held(Key.W) || Held(Key.UpArrow);
        public static bool Back => Held(Key.S) || Held(Key.DownArrow);
        public static bool Left => Held(Key.A) || Held(Key.LeftArrow);
        public static bool Right => Held(Key.D) || Held(Key.RightArrow);
        public static bool Sprint => Held(Key.LeftShift) || Held(Key.RightShift);
        public static bool JumpHeld => Held(Key.Space);
        public static bool JumpPressed => Down(Key.Space);
        public static bool InteractPressed => Down(Key.E) || Down(Key.F);
        public static bool EscapePressed => Down(Key.Escape);
        public static bool ClickPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        /// <summary>How far the mouse moved this frame, in pixels (up is positive).</summary>
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        public static bool Forward => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        public static bool Back => Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        public static bool Left => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        public static bool Right => Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        public static bool Sprint => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool JumpHeld => Input.GetKey(KeyCode.Space);
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
        public static bool EscapePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool ClickPressed => Input.GetMouseButtonDown(0);
        // The old input manager scales mouse movement by 0.1, so undo that to get pixels.
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif

        /// <summary>WASD / arrow keys as a direction: x = right, y = forward.</summary>
        public static Vector2 Move
        {
            get
            {
                var move = new Vector2((Right ? 1f : 0f) - (Left ? 1f : 0f), (Forward ? 1f : 0f) - (Back ? 1f : 0f));
                return Vector2.ClampMagnitude(move, 1f);
            }
        }
    }

    /// <summary>Esc frees the mouse; clicking in the game window captures it again.</summary>
    public static class GameCursor
    {
        public static bool Captured => Cursor.lockState == CursorLockMode.Locked;

        public static void Capture()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Call once a frame from whatever the player is controlling.</summary>
        public static void Tick()
        {
            if (GameInput.EscapePressed)
                Release();
            else if (GameInput.ClickPressed)
                Capture();
        }
    }
}
