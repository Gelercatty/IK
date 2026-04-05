using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public struct JointState
    {
        public Quaternion localRotation;
        public Vector3 worldPosition;
        public Quaternion worldRotation;

        public JointState(Quaternion localRotation, Vector3 worldPosition, Quaternion worldRotation)
        {
            this.localRotation = localRotation;
            this.worldPosition = worldPosition;
            this.worldRotation = worldRotation;
        }
    }
}
