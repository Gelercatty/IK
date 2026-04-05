using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public struct JointAxis
    {
        public string name;
        public Vector3 localAxis;
        public bool enabled;

        public JointAxis(string name, Vector3 localAxis, bool enabled = true)
        {
            this.name = name;
            this.localAxis = localAxis.normalized;
            this.enabled = enabled;
        }
    }
}
