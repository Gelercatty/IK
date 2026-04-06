using System.Collections.Generic;
using UnityEngine;

namespace GelerIK.Runtime.Core
{
    /// <summary>
    /// Lightweight singular-value analysis for a position Jacobian.
    /// Instead of running a full SVD on the 3xN Jacobian, this utility
    /// analyzes the 3x3 symmetric matrix J * J^T.
    /// </summary>
    public static class JacobianSvdUtility
    {
        public static Vector3 ComputeSingularValues(IList<Vector3> jacobianColumns)
        {
            if (jacobianColumns == null || jacobianColumns.Count == 0)
            {
                return Vector3.zero;
            }

            float m00 = 0f;
            float m01 = 0f;
            float m02 = 0f;
            float m11 = 0f;
            float m12 = 0f;
            float m22 = 0f;

            for (int i = 0; i < jacobianColumns.Count; i++)
            {
                Vector3 column = jacobianColumns[i];
                m00 += column.x * column.x;
                m01 += column.x * column.y;
                m02 += column.x * column.z;
                m11 += column.y * column.y;
                m12 += column.y * column.z;
                m22 += column.z * column.z;
            }

            Vector3 eigenvalues = ComputeSymmetricEigenvalues(
                m00,
                m01,
                m02,
                m11,
                m12,
                m22);

            Vector3 singularValues = new Vector3(
                Mathf.Sqrt(Mathf.Max(0f, eigenvalues.x)),
                Mathf.Sqrt(Mathf.Max(0f, eigenvalues.y)),
                Mathf.Sqrt(Mathf.Max(0f, eigenvalues.z)));

            SortDescending(ref singularValues);
            return singularValues;
        }

        public static float ComputeAdaptiveDamping(
            Vector3 singularValues,
            float minimumDamping,
            float singularityThreshold,
            float gain,
            float maximumDamping)
        {
            float sigmaMin = singularValues.z;
            float damping = minimumDamping;

            if (sigmaMin < singularityThreshold)
            {
                damping += gain * (singularityThreshold - sigmaMin);
            }

            return Mathf.Clamp(damping, minimumDamping, maximumDamping);
        }

        private static Vector3 ComputeSymmetricEigenvalues(
            float m00,
            float m01,
            float m02,
            float m11,
            float m12,
            float m22)
        {
            float p1 = m01 * m01 + m02 * m02 + m12 * m12;
            if (p1 < 1e-10f)
            {
                Vector3 diagonal = new Vector3(m00, m11, m22);
                SortDescending(ref diagonal);
                return diagonal;
            }

            float trace = m00 + m11 + m22;
            float q = trace / 3f;

            float b00 = m00 - q;
            float b11 = m11 - q;
            float b22 = m22 - q;

            float p2 = b00 * b00 + b11 * b11 + b22 * b22 + 2f * p1;
            float p = Mathf.Sqrt(Mathf.Max(p2 / 6f, 0f));
            if (p < 1e-10f)
            {
                return new Vector3(q, q, q);
            }

            float invP = 1f / p;
            float c00 = b00 * invP;
            float c01 = m01 * invP;
            float c02 = m02 * invP;
            float c11 = b11 * invP;
            float c12 = m12 * invP;
            float c22 = b22 * invP;

            float detC =
                c00 * (c11 * c22 - c12 * c12) -
                c01 * (c01 * c22 - c12 * c02) +
                c02 * (c01 * c12 - c11 * c02);

            float r = Mathf.Clamp(detC * 0.5f, -1f, 1f);
            float phi = Mathf.Acos(r) / 3f;

            float twoP = 2f * p;
            float eigen0 = q + twoP * Mathf.Cos(phi);
            float eigen2 = q + twoP * Mathf.Cos(phi + 2f * Mathf.PI / 3f);
            float eigen1 = 3f * q - eigen0 - eigen2;

            Vector3 eigenvalues = new Vector3(eigen0, eigen1, eigen2);
            SortDescending(ref eigenvalues);
            return eigenvalues;
        }

        private static void SortDescending(ref Vector3 values)
        {
            if (values.x < values.y)
            {
                (values.x, values.y) = (values.y, values.x);
            }

            if (values.y < values.z)
            {
                (values.y, values.z) = (values.z, values.y);
            }

            if (values.x < values.y)
            {
                (values.x, values.y) = (values.y, values.x);
            }
        }
    }
}
