using System.Collections.Generic;
using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// Position-only Jacobian Damped The Least Squares solver.
    /// Solves (J^T J + lambda^2 I) * delta = J^T e each iteration.
    /// </summary>
    public class JacobianDampedLeastSquaresSolver : IIKSolver
    {
        private readonly List<JacobianDof> _dofs = new();
        private readonly List<Vector3> _positionJacobianColumns = new();
        private readonly List<float> _normalMatrix = new();
        private readonly List<float> _rhsVector = new();
        private readonly List<float> _angleDeltasRadians = new();
        private readonly float _damping;

        public JacobianDampedLeastSquaresSolver(float damping = 0.1f)
        {
            this._damping = Mathf.Max(0.0001f, damping);
        }

        public string SolverName => "Jacobian DLS";

        public IKSolveResult Solve(IKSolveRequest request)
        {
            IKSolveResult result = new IKSolveResult();

            if (!IsRequestValid(request))
            {
                return result;
            }

            JacobianBuilder.CollectActiveDofs(request.definition, _dofs);
            if (_dofs.Count == 0)
            {
                return result;
            }

            JacobianBuilder.EnsureColumnBuffer(_positionJacobianColumns, _dofs.Count);
            JacobianMath.EnsureSquareMatrixBuffer(_normalMatrix, _dofs.Count);
            JacobianMath.EnsureVectorBuffer(_rhsVector, _dofs.Count);
            JacobianMath.EnsureVectorBuffer(_angleDeltasRadians, _dofs.Count);

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
                    _dofs,
                    _positionJacobianColumns))
            {
                return;
            }

            if (!JacobianMath.BuildNormalEquations(
                    _positionJacobianColumns,
                    positionErrorVector,
                    _damping,
                    _normalMatrix,
                    _rhsVector))
            {
                return;
            }

            ClearVector(_angleDeltasRadians);

            if (!JacobianMath.SolveLinearSystem(_normalMatrix, _rhsVector, _angleDeltasRadians))
            {
                return;
            }

            JacobianBuilder.ApplyAngleDeltas(
                request.definition,
                request.state,
                _dofs,
                _angleDeltasRadians,
                request.stepScale);

            ForwardKinematics.Evaluate(request.definition, request.state);
        }

        private static void ClearVector(IList<float> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                values[i] = 0f;
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

            return request.solvePosition;
        }
    }
}
