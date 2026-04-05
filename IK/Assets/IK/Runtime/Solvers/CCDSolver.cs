using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// CCD（Cyclic Coordinate Descent）求解器骨架。
    /// 第一版先只处理位置目标，后续再逐步补充限位、阻尼和姿态目标。
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

                // TODO: 从链末端向根节点回溯，每次选择一个关节进行旋转修正。
                // TODO: 计算“当前末端方向”和“目标方向”之间的夹角与旋转轴。
                // TODO: 将这次旋转增量乘到 request.state.joints[jointIndex].localRotation 上。
                // TODO: 支持 request.stepScale，让每轮修正不要一次走满。
                // TODO: 后续在这里接入关节限位、锁定和权重。
                break;
            }

            result.finalRotationErrorDegrees = 0f;
            return result;
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
