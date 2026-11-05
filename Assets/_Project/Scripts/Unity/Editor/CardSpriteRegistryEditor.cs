// This file must live in an Editor/ folder so it is only compiled in the Unity Editor.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CruceChaos.Unity;

namespace CruceChaos.Unity.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="CardSpriteRegistry"/> that adds an
    /// "Auto-Fill from Cards/ Folder" button so you never have to drag 24
    /// sprites by hand.
    /// </summary>
    [CustomEditor(typeof(CardSpriteRegistry))]
    public class CardSpriteRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Auto-Fill from Cards/ Folder", GUILayout.Height(32)))
            {
                var registry = (CardSpriteRegistry)target;
                registry.AutoFillFromCardsFolder();
                Debug.Log("[CardSpriteRegistry] Auto-fill complete.");
            }
        }
    }
}
#endif
