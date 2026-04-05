using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public class ChainState
    {
        public Vector3 rootWorldPosition;
        public Quaternion rootWorldRotation = Quaternion.identity;
        public JointState[] joints;
        public Vector3 endEffectorPosition;
        public Quaternion endEffectorRotation = Quaternion.identity;

        public int JointCount => joints == null ? 0 : joints.Length;

        public void EnsureSize(int jointCount)
        {
            if (joints != null && joints.Length == jointCount)
            {
                return;
            }

            joints = new JointState[jointCount];
        }
    }
}
