namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public class IKSolveResult
    {
        public bool converged;
        public int iterations;
        public float finalPositionError;
        public float finalRotationErrorDegrees;
        public float[] positionErrorHistory;
    }
}
