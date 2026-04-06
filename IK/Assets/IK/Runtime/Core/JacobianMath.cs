using System.Collections.Generic;
using UnityEngine;

namespace GelerIK.Runtime.Core
{
    /// <summary>
    /// Small linear algebra helpers for Jacobian-based IK solvers.
    /// This class currently focuses on the position-only case where each
    /// Jacobian column is stored as a Vector3.
    /// </summary>
    public static class JacobianMath
    {
        public static void EnsureSquareMatrixBuffer(List<float> matrix, int dimension)
        {
            if (matrix == null)
            {
                return;
            }

            int targetCount = dimension * dimension;

            if (matrix.Capacity < targetCount)
            {
                matrix.Capacity = targetCount;
            }

            while (matrix.Count < targetCount)
            {
                matrix.Add(0f);
            }

            while (matrix.Count > targetCount)
            {
                matrix.RemoveAt(matrix.Count - 1);
            }
        }

        public static void EnsureVectorBuffer(List<float> vector, int dimension)
        {
            if (vector == null)
            {
                return;
            }

            if (vector.Capacity < dimension)
            {
                vector.Capacity = dimension;
            }

            while (vector.Count < dimension)
            {
                vector.Add(0f);
            }

            while (vector.Count > dimension)
            {
                vector.RemoveAt(vector.Count - 1);
            }
        }

        /// <summary>
        /// build a Equation: (JTJ+λ2I)Δθ=JTe
        ///                       A        B
        /// </summary>
        /// <param name="jacobianColumns"></param>
        /// <param name="errorVector"></param>
        /// <param name="damping"></param>
        /// <param name="matrixA"></param>
        /// <param name="vectorB"></param>
        /// <returns></returns>
        public static bool BuildNormalEquations(
            IList<Vector3> jacobianColumns,
            Vector3 errorVector,
            float damping,
            IList<float> matrixA,
            IList<float> vectorB)
        {
            if (jacobianColumns == null || matrixA == null || vectorB == null)
            {
                return false;
            }

            int dofCount = jacobianColumns.Count;
            if (matrixA.Count < dofCount * dofCount || vectorB.Count < dofCount)
            {
                return false;
            }

            float dampingSquared = Mathf.Max(0f, damping) * Mathf.Max(0f, damping);

            for (int row = 0; row < dofCount; row++)
            {
                var rowColumn = jacobianColumns[row];
                vectorB[row] = Vector3.Dot(rowColumn, errorVector);

                for (var col = 0; col < dofCount; col++)
                {
                    var value = Vector3.Dot(rowColumn, jacobianColumns[col]);
                    if (row == col)
                    {
                        value += dampingSquared;
                    }

                    matrixA[row * dofCount + col] = value;
                }
            }

            return true;
        }

        public static bool SolveLinearSystem(IList<float> matrixA, IList<float> vectorB, IList<float> solution)
        {
            if (matrixA == null || vectorB == null || solution == null)
            {
                return false;
            }

            int dimension = vectorB.Count;
            if (dimension == 0 || matrixA.Count < dimension * dimension || solution.Count < dimension)
            {
                return false;
            }

            float[] workingMatrix = new float[dimension * dimension];
            float[] workingVector = new float[dimension];

            for (int i = 0; i < workingMatrix.Length; i++)
            {
                workingMatrix[i] = matrixA[i];
            }

            for (int i = 0; i < dimension; i++)
            {
                workingVector[i] = vectorB[i];
            }

            for (int pivotIndex = 0; pivotIndex < dimension; pivotIndex++)
            {
                int pivotRow = pivotIndex;
                float pivotAbs = Mathf.Abs(workingMatrix[pivotIndex * dimension + pivotIndex]);

                for (int row = pivotIndex + 1; row < dimension; row++)
                {
                    float candidateAbs = Mathf.Abs(workingMatrix[row * dimension + pivotIndex]);
                    if (candidateAbs > pivotAbs)
                    {
                        pivotAbs = candidateAbs;
                        pivotRow = row;
                    }
                }

                if (pivotAbs < 1e-8f)
                {
                    return false;
                }

                if (pivotRow != pivotIndex)
                {
                    SwapRows(workingMatrix, workingVector, dimension, pivotIndex, pivotRow);
                }

                float pivotValue = workingMatrix[pivotIndex * dimension + pivotIndex];

                for (int row = pivotIndex + 1; row < dimension; row++)
                {
                    float factor = workingMatrix[row * dimension + pivotIndex] / pivotValue;
                    if (Mathf.Abs(factor) < 1e-8f)
                    {
                        continue;
                    }

                    workingMatrix[row * dimension + pivotIndex] = 0f;
                    for (int col = pivotIndex + 1; col < dimension; col++)
                    {
                        workingMatrix[row * dimension + col] -= factor * workingMatrix[pivotIndex * dimension + col];
                    }

                    workingVector[row] -= factor * workingVector[pivotIndex];
                }
            }

            for (int row = dimension - 1; row >= 0; row--)
            {
                float value = workingVector[row];
                for (int col = row + 1; col < dimension; col++)
                {
                    value -= workingMatrix[row * dimension + col] * solution[col];
                }

                float diagonal = workingMatrix[row * dimension + row];
                if (Mathf.Abs(diagonal) < 1e-8f)
                {
                    return false;
                }

                solution[row] = value / diagonal;
            }

            return true;
        }

        private static void SwapRows(float[] matrix, float[] vector, int dimension, int a, int b)
        {
            for (int col = 0; col < dimension; col++)
            {
                (matrix[a * dimension + col], matrix[b * dimension + col]) = (matrix[b * dimension + col], matrix[a * dimension + col]);
            }

            (vector[a], vector[b]) = (vector[b], vector[a]);
        }
    }
}
