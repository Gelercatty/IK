using System.Collections.Generic;
using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// Minimal Jacobian Transpose solver for position-only IK.
    /// Each iteration builds J, projects the position error onto each column,
    /// applies the resulting angle deltas, and refreshes FK.
    /// </summary>
    public class JacobianTransposeSolver : IIKSolver
    {
        private readonly List<JacobianDof> dofs = new();
        private readonly List<Vector3> positionJacobianColumns = new();
        private readonly List<float> angleDeltasRadians = new();

        public string SolverName => "Jacobian Transpose";

        public IKSolveResult Solve(IKSolveRequest request)
        {
            IKSolveResult result = new IKSolveResult();

            if (!IsRequestValid(request))
            {
                return result;
            }

            JacobianBuilder.CollectActiveDofs(request.definition, dofs);
            if (dofs.Count == 0)
            {
                return result;
            }

            JacobianBuilder.EnsureColumnBuffer(positionJacobianColumns, dofs.Count);
            EnsureAngleDeltaBuffer(dofs.Count);

            result.positionErrorHistory = new float[request.maxIterations];

            for (int iteration = 0; iteration < request.maxIterations; iteration++)
            {
                ForwardKinematics.Evaluate(request.definition, request.state);

                Vector3 positionErrorVector = request.targetPosition - request.state.endEffectorPosition;
                float positionError = positionErrorVector.magnitude;
                result.positionErrorHistory[iteration] = positionError;
                result.iterations = iteration + 1;
                result.finalPositionError = positionError;

                if (positionError <= request.positionTolerance)
                {
                    result.converged = true;
                    break;
                }

                SolveIteration(request, positionErrorVector);
            }

            result.finalRotationErrorDegrees = 0f;
            return result;
        }

        private void SolveIteration(IKSolveRequest request, Vector3 positionErrorVector)
        {
            if (!JacobianBuilder.BuildPositionJacobian(
                    request.definition,
                    request.state,
                    dofs,
                    positionJacobianColumns))
            {
                return;
            }

            for (int i = 0; i < dofs.Count; i++)
            {
                angleDeltasRadians[i] = Vector3.Dot(positionJacobianColumns[i], positionErrorVector);
            }

            JacobianBuilder.ApplyAngleDeltas(
                request.definition,
                request.state,
                dofs,
                angleDeltasRadians,
                request.stepScale);

            ForwardKinematics.Evaluate(request.definition, request.state);
        }

        private void EnsureAngleDeltaBuffer(int dofCount)
        {
            if (angleDeltasRadians.Capacity < dofCount)
            {
                angleDeltasRadians.Capacity = dofCount;
            }

            while (angleDeltasRadians.Count < dofCount)
            {
                angleDeltasRadians.Add(0f);
            }

            while (angleDeltasRadians.Count > dofCount)
            {
                angleDeltasRadians.RemoveAt(angleDeltasRadians.Count - 1);
            }
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
