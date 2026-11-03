using UnityEngine;

namespace LondonStreets
{
    /// <summary>On-screen text with a black outline, readable on any background.</summary>
    public static class Hud
    {
        static GUIStyle _style;

        public static void Label(Rect rect, string text, int fontSize, TextAnchor anchor)
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
            float scale = Screen.height / 720f;
            _style.fontSize = Mathf.RoundToInt(fontSize * scale);
            _style.alignment = anchor;

            var old = GUI.color;
            _style.normal.textColor = Color.black;
            for (int dx = -2; dx <= 2; dx += 2)
            {
                for (int dy = -2; dy <= 2; dy += 2)
                {
                    if (dx != 0 || dy != 0)
                        GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, _style);
                }
            }
            _style.normal.textColor = Color.white;
            GUI.Label(rect, text, _style);
            GUI.color = old;
        }
    }
}
