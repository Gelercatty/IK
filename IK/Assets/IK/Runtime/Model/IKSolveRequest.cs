using UnityEngine;

namespace GelerIK.Runtime.Model
{
    [System.Serializable]
    public class IKSolveRequest
    {
        public ChainDefinition definition;
        public ChainState state;
        public Vector3 targetPosition;
        public Quaternion targetRotation = Quaternion.identity;
        public bool solvePosition = true;
        public bool solveRotation;
        public int maxIterations = 16;
        public float positionTolerance = 0.001f;
        public float rotationToleranceDegrees = 0.5f;
        public float stepScale = 1f;
    }
}
