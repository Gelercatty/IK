using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public class JointDefinition
    {
        public int index;
        public int parentIndex;
        public string name;
        public Transform transform;
        public Vector3 localBindOffset;
        public Quaternion restLocalRotation = Quaternion.identity;
        public JointAxis[] axes =
        {
            new JointAxis("X", Vector3.right),
            new JointAxis("Y", Vector3.up),
            new JointAxis("Z", Vector3.forward)
        };
        public JointLimit limit = JointLimit.Default;
        public float weight = 1f;
        public bool locked;

        public float BoneLength => localBindOffset.magnitude;
    }
}
