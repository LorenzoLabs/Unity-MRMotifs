using UnityEditor;
using UnityEngine;

namespace MRMotifs.SharedAssets
{
    /// <summary>
    /// Lightweight custom inspector to surface test buttons without requiring NaughtyAttributes.
    /// </summary>
    [CustomEditor(typeof(PanelButtonActions))]
    public class PanelButtonActionsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var actions = (PanelButtonActions)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Close Panel"))
                {
                    actions.Test_ClosePanel();
                }
                if (GUILayout.Button("Close And Show Next"))
                {
                    actions.Test_CloseAndShow();
                }
                if (GUILayout.Button("Show Panel"))
                {
                    actions.Test_ShowPanel();
                }
            }
        }
    }
}


