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
            const float MinDirectionSqrMagnitude = 1e-6f;

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

                float toEndSqrMagnitude = toEnd.sqrMagnitude;
                float toTargetSqrMagnitude = toTarget.sqrMagnitude;

                if (toEndSqrMagnitude < MinDirectionSqrMagnitude || toTargetSqrMagnitude < MinDirectionSqrMagnitude)
                {
                    continue;
                }

                Vector3 fromDirection = toEnd / Mathf.Sqrt(toEndSqrMagnitude);
                Vector3 toDirection = toTarget / Mathf.Sqrt(toTargetSqrMagnitude);
                Quaternion worldDelta = SafeFromToRotation(fromDirection, toDirection);
                float weight = Mathf.Max(0f, jointDefinition.weight);
                float step = Mathf.Clamp01(request.stepScale * weight);
                Quaternion scaledWorldDelta = Quaternion.Slerp(Quaternion.identity, worldDelta, step);

                Quaternion parentWorldRotation = GetParentWorldRotation(request, jointDefinition);
                Quaternion localDelta =
                    Quaternion.Inverse(parentWorldRotation) * scaledWorldDelta * parentWorldRotation;

                jointState.localRotation = Normalize(localDelta * jointState.localRotation);
                request.state.joints[jointIndex] = jointState;

                ForwardKinematics.Evaluate(request.definition, request.state);
            }
        }

        private static Quaternion SafeFromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            float dot = Mathf.Clamp(Vector3.Dot(fromDirection, toDirection), -1f, 1f);

            if (dot > 1f - 1e-6f)
            {
                return Quaternion.identity;
            }

            if (dot < -1f + 1e-6f)
            {
                Vector3 axis = Vector3.Cross(fromDirection, Vector3.right);
                if (axis.sqrMagnitude < 1e-6f)
                {
                    axis = Vector3.Cross(fromDirection, Vector3.up);
                }

                if (axis.sqrMagnitude < 1e-6f)
                {
                    return Quaternion.identity;
                }

                return Quaternion.AngleAxis(180f, axis.normalized);
            }

            Vector3 rotationAxis = Vector3.Cross(fromDirection, toDirection);
            if (rotationAxis.sqrMagnitude < 1e-12f)
            {
                return Quaternion.identity;
            }

            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, rotationAxis.normalized);
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            float magnitude =
                Mathf.Sqrt(
                    rotation.x * rotation.x +
                    rotation.y * rotation.y +
                    rotation.z * rotation.z +
                    rotation.w * rotation.w);

            if (magnitude < 1e-8f)
            {
                return Quaternion.identity;
            }

            float inverseMagnitude = 1f / magnitude;
            return new Quaternion(
                rotation.x * inverseMagnitude,
                rotation.y * inverseMagnitude,
                rotation.z * inverseMagnitude,
                rotation.w * inverseMagnitude);
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
