using System.Collections.Generic;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Core
{
    /// <summary>
    /// Shared utilities for Jacobian-based IK solvers.
    /// Position Jacobian columns are represented as Vector3 values,
    /// where each column stores dX / dTheta for one rotational DOF.
    /// </summary>
    public static class JacobianBuilder
    {
        public static int CountActiveDofs(ChainDefinition definition)
        {
            if (definition == null || definition.joints == null)
            {
                return 0;
            }

            int count = 0;

            for (int jointIndex = 0; jointIndex < definition.JointCount; jointIndex++)
            {
                JointDefinition joint = definition.joints[jointIndex];
                if (joint.locked || joint.axes == null)
                {
                    continue;
                }

                for (int axisIndex = 0; axisIndex < joint.axes.Length; axisIndex++)
                {
                    if (joint.axes[axisIndex].enabled)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static int CollectActiveDofs(ChainDefinition definition, List<JacobianDof> dofs)
        {
            if (dofs == null)
            {
                return 0;
            }

            dofs.Clear();

            if (definition == null || definition.joints == null)
            {
                return 0;
            }

            for (int jointIndex = 0; jointIndex < definition.JointCount; jointIndex++)
            {
                JointDefinition joint = definition.joints[jointIndex];
                if (joint.locked || joint.axes == null)
                {
                    continue;
                }

                float weight = Mathf.Max(0f, joint.weight);

                for (int axisIndex = 0; axisIndex < joint.axes.Length; axisIndex++)
                {
                    JointAxis axis = joint.axes[axisIndex];
                    if (!axis.enabled)
                    {
                        continue;
                    }

                    dofs.Add(new JacobianDof(jointIndex, axisIndex, axis.name, axis.localAxis, weight));
                }
            }

            return dofs.Count;
        }

        public static bool BuildPositionJacobian(
            ChainDefinition definition,
            ChainState state,
            IList<JacobianDof> dofs,
            IList<Vector3> columns)
        {
            if (!IsValid(definition, state, dofs, columns))
            {
                return false;
            }

            Vector3 endEffector = state.endEffectorPosition;

            for (int columnIndex = 0; columnIndex < dofs.Count; columnIndex++)
            {
                JacobianDof dof = dofs[columnIndex];
                JointState jointState = state.joints[dof.jointIndex];
                Vector3 worldAxis = jointState.worldRotation * dof.localAxis;
                // axis_world x (endEffector - jointPos)
                columns[columnIndex] = Vector3.Cross(worldAxis, endEffector - jointState.worldPosition);
            }

            return true;
        }

        public static Vector3 GetWorldAxis(ChainState state, JacobianDof dof)
        {
            if (state == null || state.joints == null)
            {
                return Vector3.zero;
            }

            if (dof.jointIndex < 0 || dof.jointIndex >= state.joints.Length)
            {
                return Vector3.zero;
            }

            return state.joints[dof.jointIndex].worldRotation * dof.localAxis;
        }

        public static bool ApplyAngleDeltas(
            ChainDefinition definition,
            ChainState state,
            IList<JacobianDof> dofs,
            IList<float> angleDeltasRadians,
            float stepScale = 1f)
        {
            if (definition == null || state == null || state.joints == null || dofs == null || angleDeltasRadians == null)
            {
                return false;
            }

            if (dofs.Count != angleDeltasRadians.Count)
            {
                return false;
            }

            for (int i = 0; i < dofs.Count; i++)
            {
                JacobianDof dof = dofs[i];
                if (dof.jointIndex < 0 || dof.jointIndex >= definition.JointCount)
                {
                    continue;
                }

                JointDefinition jointDefinition = definition.joints[dof.jointIndex];
                if (jointDefinition.locked)
                {
                    continue;
                }

                float scaledDeltaRadians = angleDeltasRadians[i] * stepScale * dof.weight;
                if (Mathf.Abs(scaledDeltaRadians) < 1e-8f)
                {
                    continue;
                }

                Quaternion localDelta = Quaternion.AngleAxis(
                    scaledDeltaRadians * Mathf.Rad2Deg,
                    dof.localAxis);

                var jointState = state.joints[dof.jointIndex];
                jointState.localRotation = jointState.localRotation * localDelta;
                state.joints[dof.jointIndex] = jointState;
            }

            return true;
        }

        public static void EnsureColumnBuffer(List<Vector3> columns, int dofCount)
        {
            if (columns == null)
            {
                return;
            }

            if (columns.Capacity < dofCount)
            {
                columns.Capacity = dofCount;
            }

            while (columns.Count < dofCount)
            {
                columns.Add(Vector3.zero);
            }

            while (columns.Count > dofCount)
            {
                columns.RemoveAt(columns.Count - 1);
            }
        }

        private static bool IsValid(
            ChainDefinition definition,
            ChainState state,
            IList<JacobianDof> dofs,
            IList<Vector3> columns)
        {
            if (definition == null || state == null || dofs == null || columns == null)
            {
                return false;
            }

            if (definition.joints == null || state.joints == null)
            {
                return false;
            }

            if (state.joints.Length != definition.JointCount)
            {
                return false;
            }

            return columns.Count >= dofs.Count;
        }
    }
}
