using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// CCD（Cyclic Coordinate Descent）求解器。
    /// 当前版本先只处理位置目标，后续再逐步补充限位、阻尼和姿态目标。
    /// </summary>
    public class CCDSolver : IIKSolver
    {
        public string SolverName => "CCD";

        public IKSolveResult Solve(IKSolveRequest request)
        {
            IKSolveResult result = new IKSolveResult();

            if (!IsRequestValid(request))
            {
                return result;
            }

            result.positionErrorHistory = new float[request.maxIterations];

            for (int iteration = 0; iteration < request.maxIterations; iteration++)
            {
                ForwardKinematics.Evaluate(request.definition, request.state);

                Vector3 endEffector = request.state.endEffectorPosition;
                float positionError = Vector3.Distance(endEffector, request.targetPosition);
                result.positionErrorHistory[iteration] = positionError;
                result.iterations = iteration + 1;
                result.finalPositionError = positionError;

                if (positionError <= request.positionTolerance)
                {
                    result.converged = true;
                    break;
                }

                SolveIteration(request);
            }

            result.finalRotationErrorDegrees = 0f;
            return result;
        }

        private static void SolveIteration(IKSolveRequest request)
        {
            for (var jointIndex = request.definition.JointCount - 1; jointIndex >= 0; jointIndex--)
            {
                var jointDefinition = request.definition.joints[jointIndex];
                if (jointDefinition.locked)
                {
                    continue;
                }

                var jointState = request.state.joints[jointIndex];
                var jointPosition = jointState.worldPosition;
                var toEnd = request.state.endEffectorPosition - jointPosition;
                var toTarget = request.targetPosition - jointPosition;

                if (toEnd.sqrMagnitude < 1e-8f || toTarget.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                Quaternion worldDelta = Quaternion.FromToRotation(toEnd, toTarget);
                float weight = Mathf.Max(0f, jointDefinition.weight);
                float step = Mathf.Clamp01(request.stepScale * weight);
                Quaternion scaledWorldDelta = Quaternion.Slerp(Quaternion.identity, worldDelta, step);

                Quaternion parentWorldRotation = GetParentWorldRotation(request, jointDefinition);
                Quaternion localDelta =
                    Quaternion.Inverse(parentWorldRotation) * scaledWorldDelta * parentWorldRotation;

                jointState.localRotation = localDelta * jointState.localRotation;
                request.state.joints[jointIndex] = jointState;

                ForwardKinematics.Evaluate(request.definition, request.state);
            }
        }

        private static Quaternion GetParentWorldRotation(IKSolveRequest request, JointDefinition jointDefinition)
        {
            if (jointDefinition.parentIndex < 0)
            {
                return request.state.rootWorldRotation;
            }

            return request.state.joints[jointDefinition.parentIndex].worldRotation;
        }

        private static bool IsRequestValid(IKSolveRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.definition == null || request.state == null)
            {
                return false;
            }

            if (!request.definition.IsValid)
            {
                return false;
            }

            if (request.state.joints == null || request.state.joints.Length != request.definition.JointCount)
            {
                return false;
            }

            if (!request.solvePosition)
            {
                return false;
            }

            return true;
        }
    }
}
