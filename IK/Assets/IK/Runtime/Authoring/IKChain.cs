using System.Collections.Generic;
using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using GelerIK.Runtime.Solvers;
using UnityEngine;

namespace GelerIK.Runtime.Authoring
{
    [ExecuteAlways]
    public class IKChain : MonoBehaviour
    {
        private enum SolverType
        {
            None,
            CCD
        }

        [Header("Chain")]
        [SerializeField] private IKBone rootBone;
        [SerializeField] private IKBone endBone;
        [SerializeField] private Transform target;
        [SerializeField] private Transform poleTarget;

        [Header("Build")]
        [SerializeField] private bool autoCollectBones = true;
        [SerializeField] private bool rebuildOnValidate = true;
        [SerializeField] private bool syncStateFromTransformsOnUpdate = true;
        [SerializeField] private bool pushStateToTransformsAfterFk = true;

        [Header("Solver")]
        [SerializeField] private SolverType solverType = SolverType.None;
        [SerializeField] private bool solveInUpdate;
        [SerializeField] private int maxIterations = 16;
        [SerializeField] private float positionTolerance = 0.001f;
        [SerializeField] private float stepScale = 1f;

        [Header("Debug")]
        [SerializeField] private bool drawChain = true;
        [SerializeField] private Color chainColor = new(0.3f, 0.9f, 0.95f, 1f);

        [SerializeField] private List<IKBone> bones = new();
        [SerializeField] private ChainDefinition definition;
        [SerializeField] private ChainState state;

        public IKBone RootBone => rootBone;
        public IKBone EndBone => endBone;
        public Transform Target => target;
        public Transform PoleTarget => poleTarget;
        public IReadOnlyList<IKBone> Bones => bones;
        public ChainDefinition Definition => definition;
        public ChainState State => state;

        private void Reset()
        {
            TryAutoAssignBones();
            RebuildChain();
        }

        private void OnValidate()
        {
            if (!rebuildOnValidate)
            {
                return;
            }

            RebuildChain();
        }

        private void Update()
        {
            if (!syncStateFromTransformsOnUpdate)
            {
                if (solveInUpdate)
                {
                    SolveIK();
                }

                return;
            }

            if (definition == null || !definition.IsValid)
            {
                return;
            }

            PullFromTransforms();

            if (solveInUpdate)
            {
                SolveIK();
            }
        }

        [ContextMenu("Rebuild Chain")]
        public void RebuildChain()
        {
            if (autoCollectBones)
            {
                CollectBonesFromEndpoints();
            }

            if (!ValidateChain(false))
            {
                definition = null;
                state = null;
                return;
            }

            definition = CreateDefinition();
            state = CreateInitialState();
        }

        [ContextMenu("Collect Bones From Endpoints")]
        public void CollectBonesFromEndpoints()
        {
            bones.Clear();

            if (rootBone == null || endBone == null)
            {
                return;
            }

            List<IKBone> reverseChain = new();
            IKBone current = endBone;

            while (current != null)
            {
                reverseChain.Add(current);

                if (current == rootBone)
                {
                    break;
                }

                current = current.ParentBone;
            }

            if (reverseChain.Count == 0 || reverseChain[^1] != rootBone)
            {
                bones.Clear();
                return;
            }

            for (int i = reverseChain.Count - 1; i >= 0; i--)
            {
                bones.Add(reverseChain[i]);
            }
        }

        public ChainDefinition CreateDefinition()
        {
            if (!ValidateChain(false))
            {
                return null;
            }

            JointDefinition[] joints = new JointDefinition[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                int parentIndex = i == 0 ? -1 : i - 1;
                joints[i] = bones[i].CreateDefinition(i, parentIndex);
            }

            return new ChainDefinition
            {
                name = gameObject.name,
                root = rootBone.transform,
                endEffector = endBone.transform,
                joints = joints
            };
        }

        public ChainState CreateInitialState()
        {
            if (!ValidateChain(false))
            {
                return null;
            }

            ChainState chainState = new();
            chainState.EnsureSize(bones.Count);
            chainState.rootWorldPosition = rootBone.transform.position;
            chainState.rootWorldRotation = rootBone.transform.rotation;

            for (int i = 0; i < bones.Count; i++)
            {
                Transform boneTransform = bones[i].transform;
                chainState.joints[i] = new JointState(
                    boneTransform.localRotation,
                    boneTransform.position,
                    boneTransform.rotation);
            }

            if (definition != null && definition.IsValid)
            {
                ForwardKinematics.Evaluate(definition, chainState);
            }
            else
            {
                chainState.endEffectorPosition = endBone.transform.position;
                chainState.endEffectorRotation = endBone.transform.rotation;
            }

            return chainState;
        }

        public void SyncStateFromTransforms()
        {
            PullFromTransforms();
        }

        public void ApplyStateToTransforms()
        {
            PushToTransforms();
        }

        /// <summary>
        /// 从场景中的 Transform 拉取当前姿态，写入运行时 state。
        /// </summary>
        public void PullFromTransforms()
        {
            if (!ValidateChain(false))
            {
                return;
            }

            if (state == null)
            {
                state = new ChainState();
            }

            state.EnsureSize(bones.Count);
            state.rootWorldPosition = rootBone.transform.position;
            state.rootWorldRotation = rootBone.transform.rotation;

            for (int i = 0; i < bones.Count; i++)
            {
                Transform boneTransform = bones[i].transform;
                state.joints[i] = new JointState(
                    boneTransform.localRotation,
                    boneTransform.position,
                    boneTransform.rotation);
            }

            if (definition != null && definition.IsValid)
            {
                ForwardKinematics.Evaluate(definition, state);
            }
            else
            {
                state.endEffectorPosition = endBone.transform.position;
                state.endEffectorRotation = endBone.transform.rotation;
            }
        }

        /// <summary>
        /// 将运行时 state 中的姿态回写到场景 Transform。
        /// 这个接口是运行时“数据 -> 场景”的统一入口。
        /// </summary>
        public void PushToTransforms()
        {
            if (state == null || state.joints == null || state.joints.Length != bones.Count)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                Transform boneTransform = bones[i].transform;
                JointState jointState = state.joints[i];

                boneTransform.localRotation = jointState.localRotation;
                boneTransform.position = jointState.worldPosition;
                boneTransform.rotation = jointState.worldRotation;
            }
        }

        /// <summary>
        /// 使用当前 definition 和 state 执行一次 FK 求值。
        /// 这个接口负责“根据局部状态刷新整条链的世界空间缓存”。
        /// </summary>
        public bool EvaluateForwardKinematics()
        {
            if (definition == null || state == null)
            {
                return false;
            }

            bool success = ForwardKinematics.Evaluate(definition, state);

            if (success && pushStateToTransformsAfterFk)
            {
                PushToTransforms();
            }

            return success;
        }

        /// <summary>
        /// 构建求解请求并调用当前选中的 IK 求解器。
        /// 目前先接入 CCD，后续可以在这里扩展 FABRIK、Jacobian 等。
        /// </summary>
        [ContextMenu("Solve IK Once")]
        public IKSolveResult SolveIK()
        {
            if (!ValidateChain(false) || target == null)
            {
                return null;
            }

            PullFromTransforms();

            IIKSolver solver = CreateSolver();
            if (solver == null)
            {
                return null;
            }

            IKSolveRequest request = new IKSolveRequest
            {
                definition = definition,
                state = state,
                targetPosition = target.position,
                targetRotation = target.rotation,
                solvePosition = true,
                solveRotation = false,
                maxIterations = maxIterations,
                positionTolerance = positionTolerance,
                stepScale = stepScale
            };

            IKSolveResult result = solver.Solve(request);
            PushToTransforms();
            return result;
        }

        public bool ValidateChain(bool logWarnings = true)
        {
            if (rootBone == null || endBone == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("IKChain requires both rootBone and endBone.", this);
                }

                return false;
            }

            if (bones == null || bones.Count == 0)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("IKChain has no collected bones.", this);
                }

                return false;
            }

            if (bones[0] != rootBone || bones[^1] != endBone)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("IKChain bone list does not match root/end endpoints.", this);
                }

                return false;
            }

            for (int i = 1; i < bones.Count; i++)
            {
                if (bones[i] == null)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning($"IKChain contains a null bone at index {i}.", this);
                    }

                    return false;
                }

                if (bones[i].ParentBone != bones[i - 1])
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"IKChain bone '{bones[i].name}' does not point to the expected parent bone.",
                            this);
                    }

                    return false;
                }
            }

            return true;
        }

        private void TryAutoAssignBones()
        {
            if (rootBone == null)
            {
                rootBone = GetComponentInChildren<IKBone>();
            }

            if (endBone == null)
            {
                IKBone[] childBones = GetComponentsInChildren<IKBone>();
                if (childBones.Length > 0)
                {
                    endBone = childBones[^1];
                }
            }
        }

        private IIKSolver CreateSolver()
        {
            return solverType switch
            {
                SolverType.CCD => new CCDSolver(),
                _ => null
            };
        }

        private void OnDrawGizmos()
        {
            if (!drawChain || bones == null || bones.Count < 2)
            {
                return;
            }

            Gizmos.color = chainColor;

            for (int i = 0; i < bones.Count - 1; i++)
            {
                if (bones[i] == null || bones[i + 1] == null)
                {
                    continue;
                }

                Gizmos.DrawLine(bones[i].transform.position, bones[i + 1].transform.position);
            }

            if (target != null)
            {
                Gizmos.DrawWireSphere(target.position, 0.05f);
            }

            if (poleTarget != null)
            {
                Gizmos.DrawWireCube(poleTarget.position, Vector3.one * 0.08f);
            }
        }
    }
}
