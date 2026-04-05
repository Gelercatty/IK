using GelerIK.Runtime.Model;
using UnityEngine;

namespace GelerIK.Runtime.Core
{
    /// <summary>
    /// IK 系统共用的正向运动学工具类。
    /// 当前统一约定：骨架结构真值来自 localBindOffset，末端执行器是链最后一个关节本身。
    /// </summary>
    public static class ForwardKinematics
    {
        /// <summary>
        /// 从根节点到末端依次计算整条骨骼链的世界姿态。
        /// 这个方法会更新每个关节的世界位置、世界旋转，以及末端执行器缓存。
        /// </summary>
        public static bool Evaluate(ChainDefinition definition, ChainState state)
        {
            if (!IsValid(definition, state))
            {
                return false;
            }

            state.EnsureSize(definition.JointCount);

            for (int i = 0; i < definition.JointCount; i++)
            {
                JointDefinition jointDefinition = definition.joints[i];
                JointState jointState = state.joints[i];

                if (jointDefinition.parentIndex < 0)
                {
                    jointState.worldPosition = state.rootWorldPosition;
                    jointState.worldRotation = state.rootWorldRotation * jointState.localRotation;
                }
                else
                {
                    JointState parentState = state.joints[jointDefinition.parentIndex];
                    jointState.worldPosition =
                        parentState.worldPosition + parentState.worldRotation * jointDefinition.localBindOffset;
                    jointState.worldRotation = parentState.worldRotation * jointState.localRotation;
                }

                state.joints[i] = jointState;
            }

            UpdateEndEffector(definition, state);
            return true;
        }

        /// <summary>
        /// 根据当前场景中的 Transform 或定义里的参考姿态，构建一份初始运行时状态。
        /// 在填好局部旋转后，会立刻执行一次 FK，保证世界空间缓存立即可用。
        /// </summary>
        public static bool InitializeFromDefinition(ChainDefinition definition, ChainState state)
        {
            if (definition == null || definition.joints == null || state == null)
            {
                return false;
            }

            state.EnsureSize(definition.JointCount);

            if (definition.root != null)
            {
                state.rootWorldPosition = definition.root.position;
                state.rootWorldRotation = definition.root.rotation;
            }

            for (int i = 0; i < definition.JointCount; i++)
            {
                JointDefinition jointDefinition = definition.joints[i];
                Quaternion localRotation = jointDefinition.restLocalRotation;

                if (jointDefinition.transform != null)
                {
                    localRotation = jointDefinition.transform.localRotation;
                }

                state.joints[i] = new JointState(localRotation, Vector3.zero, Quaternion.identity);
            }

            return Evaluate(definition, state);
        }

        /// <summary>
        /// 计算单根骨骼骨端在世界空间中的位置。
        /// 在当前设计里，骨骼长度由 localBindOffset 的模长给出。
        /// 这个方法主要用于显示或调试，不再参与末端执行器定义。
        /// </summary>
        public static Vector3 GetBoneEndPosition(JointDefinition jointDefinition, JointState jointState)
        {
            return jointState.worldPosition + jointState.worldRotation * (Vector3.forward * jointDefinition.BoneLength);
        }

        /// <summary>
        /// 在所有关节世界姿态计算完成后，刷新末端执行器缓存。
        /// 当前统一约定末端执行器就是链最后一个关节的位置，而不是额外延伸的骨端。
        /// </summary>
        private static void UpdateEndEffector(ChainDefinition definition, ChainState state)
        {
            int lastIndex = definition.JointCount - 1;
            JointState lastState = state.joints[lastIndex];

            state.endEffectorPosition = lastState.worldPosition;
            state.endEffectorRotation = lastState.worldRotation;
        }

        /// <summary>
        /// 检查当前 definition 和 state 是否可用于 FK 计算。
        /// 同时会确保 state 内部的关节缓存数组长度正确。
        /// </summary>
        private static bool IsValid(ChainDefinition definition, ChainState state)
        {
            if (definition == null || state == null)
            {
                return false;
            }

            if (definition.joints == null || definition.JointCount == 0)
            {
                return false;
            }

            if (state.joints == null || state.joints.Length != definition.JointCount)
            {
                state.EnsureSize(definition.JointCount);
            }

            return true;
        }
    }
}
