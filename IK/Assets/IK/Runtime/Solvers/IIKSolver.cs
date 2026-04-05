using GelerIK.Runtime.Model;

namespace GelerIK.Runtime.Solvers
{
    /// <summary>
    /// IK 求解器统一接口。
    /// 所有求解器都应当接收同样的请求结构，并返回统一的求解结果。
    /// </summary>
    public interface IIKSolver
    {
        /// <summary>
        /// 当前求解器名称，方便调试与实验对比。
        /// </summary>
        string SolverName { get; }

        /// <summary>
        /// 根据输入的定义、状态和目标执行一次 IK 求解。
        /// </summary>
        IKSolveResult Solve(IKSolveRequest request);
    }
}
