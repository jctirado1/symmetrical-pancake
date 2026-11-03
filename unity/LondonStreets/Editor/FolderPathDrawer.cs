using UnityEditor;
using UnityEngine;

namespace LondonStreets.EditorTools
{
    /// <summary>A text box with a "Choose" button, and you can drop a folder from the Project window onto it.</summary>
    [CustomPropertyDrawer(typeof(FolderPathAttribute))]
    public class FolderPathDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            var field = new Rect(position.x, position.y, position.width - 64, position.height);
            var button = new Rect(position.xMax - 60, position.y, 60, position.height);
            property.stringValue = EditorGUI.TextField(field, label, property.stringValue);

            // Dropping a folder onto the field.
            var current = Event.current;
            if ((current.type == EventType.DragUpdated || current.type == EventType.DragPerform) && field.Contains(current.mousePosition))
            {
                string folder = null;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(path))
                        folder = path;
                }
                if (folder != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    if (current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        property.stringValue = folder;
                    }
                    current.Use();
                }
            }

            if (GUI.Button(button, "Choose"))
            {
                var start = AssetDatabase.IsValidFolder(property.stringValue) ? property.stringValue : "Assets";
                var chosen = EditorUtility.OpenFolderPanel("Choose a folder inside Assets", start, "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    chosen = chosen.Replace('\\', '/');
                    var projectAssets = Application.dataPath.Replace('\\', '/');
                    if (chosen.StartsWith(projectAssets))
                        property.stringValue = "Assets" + chosen.Substring(projectAssets.Length);
                    else
                        EditorUtility.DisplayDialog("London Streets", "Please choose a folder inside this project's Assets folder.", "OK");
                }
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
            EditorGUI.EndProperty();
        }
    }
}
