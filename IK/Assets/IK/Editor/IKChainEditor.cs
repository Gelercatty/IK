using GelerIK.Runtime.Authoring;
using UnityEditor;
using UnityEngine;

namespace GelerIK.Editor
{
    [CustomEditor(typeof(IKChain))]
    public class IKChainEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            IKChain chain = (IKChain)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Chain"))
                {
                    Undo.RecordObject(chain, "Rebuild IK Chain");
                    chain.RebuildChain();
                    EditorUtility.SetDirty(chain);
                }

                if (GUILayout.Button("Solve Once"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(chain.gameObject, "Solve IK Once");
                    chain.SolveIK();
                    EditorUtility.SetDirty(chain);
                }
            }
        }
    }
}
