using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public class ChainDefinition
    {
        public string name;
        public Transform root;
        public Transform endEffector;
        public JointDefinition[] joints;

        public int JointCount => joints == null ? 0 : joints.Length;
        public bool IsValid => root != null && endEffector != null && JointCount > 0;
    }
}
