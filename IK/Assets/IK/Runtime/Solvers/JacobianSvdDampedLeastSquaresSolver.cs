using System.Collections.Generic;
using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// DLS solver with an adaptive damping factor driven by Jacobian singular values.
    /// The current position Jacobian is analyzed each iteration and the damping factor
    /// is increased as the smallest singular value approaches zero.
    /// </summary>
    public class JacobianSvdDampedLeastSquaresSolver : IIKSolver
    {
        private readonly List<JacobianDof> dofs = new();
        private readonly List<Vector3> positionJacobianColumns = new();
        private readonly List<float> normalMatrix = new();
        private readonly List<float> rhsVector = new();
        private readonly List<float> angleDeltasRadians = new();
        private readonly float minimumDamping;
        private readonly float singularityThreshold;
        private readonly float dampingGain;
        private readonly float maximumDamping;

        public JacobianSvdDampedLeastSquaresSolver(
            float minimumDamping = 0.02f,
            float singularityThreshold = 0.1f,
            float dampingGain = 1.5f,
            float maximumDamping = 1.0f)
        {
            this.minimumDamping = Mathf.Max(0.0001f, minimumDamping);
            this.singularityThreshold = Mathf.Max(0.0001f, singularityThreshold);
            this.dampingGain = Mathf.Max(0f, dampingGain);
            this.maximumDamping = Mathf.Max(this.minimumDamping, maximumDamping);
        }

        public string SolverName => "Jacobian SVD DLS";

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
            JacobianMath.EnsureSquareMatrixBuffer(normalMatrix, dofs.Count);
            JacobianMath.EnsureVectorBuffer(rhsVector, dofs.Count);
            JacobianMath.EnsureVectorBuffer(angleDeltasRadians, dofs.Count);

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

            Vector3 singularValues = JacobianSvdUtility.ComputeSingularValues(positionJacobianColumns);
            float adaptiveDamping = JacobianSvdUtility.ComputeAdaptiveDamping(
                singularValues,
                minimumDamping,
                singularityThreshold,
                dampingGain,
                maximumDamping);

            if (!JacobianMath.BuildNormalEquations(
                    positionJacobianColumns,
                    positionErrorVector,
                    adaptiveDamping,
                    normalMatrix,
                    rhsVector))
            {
                return;
            }

            ClearVector(angleDeltasRadians);

            if (!JacobianMath.SolveLinearSystem(normalMatrix, rhsVector, angleDeltasRadians))
            {
                return;
            }

            JacobianBuilder.ApplyAngleDeltas(
                request.definition,
                request.state,
                dofs,
                angleDeltasRadians,
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
