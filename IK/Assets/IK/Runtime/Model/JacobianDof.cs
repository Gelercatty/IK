using UnityEngine;

namespace GelerIK.Runtime.Model
{
    // cache of jacobin column 
    [System.Serializable]
    public struct JacobianDof
    {
        public int jointIndex;
        public int axisIndex;
        public string axisName;
        public Vector3 localAxis;
        public float weight;

        public JacobianDof(int jointIndex, int axisIndex, string axisName, Vector3 localAxis, float weight)
        {
            this.jointIndex = jointIndex;
            this.axisIndex = axisIndex;
            this.axisName = axisName;
            this.localAxis = localAxis.sqrMagnitude > 1e-8f ? localAxis.normalized : Vector3.forward;
            this.weight = Mathf.Max(0f, weight);
        }
    }
}
