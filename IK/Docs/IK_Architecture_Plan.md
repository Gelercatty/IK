# IK 学习型项目架构规划

## 1. 目标

这个项目的目标不是直接做一个“最强 IK 插件”，而是做一个适合学习、实验、对比论文方法的 Unity 工程。

因此设计原则是：

- 论文核心自己实现。
- 能直接复用 Unity 的地方就复用，不重复造基础轮子。
- 先保证结构清晰，再逐步补算法。
- 先做“位置 IK”，再扩展到“位置 + 朝向 IK”。
- 先做单链实验，再做多链、多目标和约束增强。

## 2. 哪些复用，哪些自己实现

### 2.1 直接复用 Unity

- `Transform`
  用来承载场景中的骨骼层级、可视化结果、与 Inspector 交互。
- `Vector3` / `Quaternion` / `Matrix4x4`
  用来表达位姿与矩阵，不单独再写一套基础数学类型。
- Gizmos / Handles
  用来画关节轴、目标点、链路、Jacobian 列方向等调试信息。
- `MonoBehaviour` + 序列化字段
  用来做 authoring 和实验入口。
- Unity Test Framework
  用来做 FK、约束、收敛性测试。

### 2.2 论文核心自己实现

- 骨骼链抽取与运行时数据结构。
- FK 层级累乘。
- Jacobian 的构建。
- `Two-Bone`、`CCD`、`FABRIK`、`Jacobian Transpose`、`DLS` 求解器。
- 关节限位、权重、锁定、终止条件。
- 实验统计和算法对比。

### 2.3 暂时不要直接依赖的现成方案

- `Animation Rigging` 包
  可以以后拿来做对照，但不应作为主求解器实现基础。
- 第三方 IK 插件
  可以参考思路，但不应替代论文核心实现。

## 3. 核心设计原则

### 3.1 `Transform` 不是求解时的真值

求解过程中不要每次迭代都直接改 Unity 场景里的 `Transform`，否则会带来：

- 调试困难。
- 数据回滚困难。
- 多求解器对比困难。
- 父子层级同步和约束处理变得混乱。

建议：

- 运行时维护一份纯 C# 的 `ChainState`。
- 每次迭代只修改 `ChainState`。
- 求解结束后统一把结果写回 `Transform`。

这条是整个项目最重要的结构约束之一。

### 3.2 理论上以矩阵表述，运行时以“位置 + 旋转”存储

论文表达适合用齐次矩阵。

但在 Unity 运行时，更推荐：

- 状态存储：`Vector3` + `Quaternion`
- 需要时派生：`Matrix4x4`

原因：

- 更贴近 Unity API。
- 更容易处理局部旋转与全局旋转。
- 不容易把缩放一起混进来。

建议整个项目假设骨骼链为“无缩放 / 等比缩放”，不要把非均匀缩放纳入第一版实验。

## 4. 推荐目录结构

建议不要把所有脚本都堆在 `Assets/IK` 根目录下，最好尽早分层。

```text
Assets/
  IK/
    Runtime/
      Authoring/
      Core/
      Model/
      Solvers/
      Constraints/
      Utils/
      Debugging/
    Editor/
    Tests/
      EditMode/
      PlayMode/
    Demo/
      Scripts/
      Prefabs/
      Scenes/
Docs/
  IK_Architecture_Plan.md
```

建议职责如下：

- `Runtime/Authoring`
  `MonoBehaviour` 组件，负责把场景骨骼配置成可求解链。
- `Runtime/Core`
  FK、链更新、状态同步等核心流程。
- `Runtime/Model`
  纯数据结构，不依赖场景行为。
- `Runtime/Solvers`
  各类求解器实现。
- `Runtime/Constraints`
  关节限位、锁定、权重、pole vector 等。
- `Runtime/Debugging`
  Gizmo、统计信息、实验可视化。
- `Editor`
  自动采集骨骼链、Inspector 校验、按钮工具。
- `Tests`
  单元测试与收敛性测试。
- `Demo`
  实验场景、预制体、控制脚本。

## 5. 推荐数据分层

当前的 `BoneData` 同时承担了：

- 场景绑定
- 配置
- 关节自由度
- 约束
- 参考姿态

第一版能用，但后续会越来越难维护。建议拆成下面四层。

### 5.1 Authoring 层

用于挂在 GameObject 上，面向 Inspector。

建议类型：

- `IKChainAuthoring : MonoBehaviour`
- `IKJointAuthoring : MonoBehaviour`
- `IKTargetAuthoring : MonoBehaviour`

职责：

- 绑定根骨骼、末端骨骼、目标物体。
- 记录每个关节的轴定义、DOF、限位、权重。
- 在运行开始时生成 `ChainDefinition`。

### 5.2 Definition 层

这是“静态定义”，一旦建链后通常不频繁变化。

建议类型：

- `JointDefinition`
- `ChainDefinition`
- `EffectorDefinition`

建议字段：

`JointDefinition`

- `int index`
- `int parentIndex`
- `string name`
- `Transform transform`
- `Vector3 localBindOffset`
- `Quaternion restLocalRotation`
- `JointDof dof`
- `AxisDefinition[] axes`
- `JointLimit limit`
- `float weight`
- `bool locked`

这里的重点是：

- `localBindOffset` 表示骨骼在父节点局部空间中的固定偏移。
- `restLocalRotation` 表示参考姿态。
- `axes` 表示这个关节在局部空间下允许转动的轴。

### 5.3 State 层

这是“运行时真值”，求解器只读写它。

建议类型：

- `JointState`
- `ChainState`

建议字段：

`JointState`

- `Quaternion localRotation`
- `Vector3 worldPosition`
- `Quaternion worldRotation`

`ChainState`

- `JointState[] joints`
- `Vector3 endEffectorPosition`
- `Quaternion endEffectorRotation`

注意：

- `worldPosition` / `worldRotation` 是 FK 更新后的缓存结果。
- 不要把 `Transform` 放进 `State`，这样求解器才干净。

### 5.4 Request / Result 层

用于统一求解器接口。

建议类型：

- `IKSolveRequest`
- `IKSolveResult`

`IKSolveRequest` 可包含：

- 当前 `ChainDefinition`
- 当前 `ChainState`
- 目标位置
- 目标朝向
- 最大迭代次数
- 误差阈值
- 时间步长或步长系数

`IKSolveResult` 可包含：

- 是否收敛
- 迭代次数
- 最终误差
- 每次迭代误差历史

## 6. 推荐求解器接口

建议统一成一套接口，方便实验横向比较。

```text
IIKSolver
  Solve(definition, state, target, settings) -> result
```

建议至少统一以下信息：

- 输入是否只处理位置。
- 是否支持朝向目标。
- 是否支持关节限位。
- 是否支持 pole vector。
- 是否需要预处理缓存。

建议求解器实现分成：

- `TwoBoneSolver`
- `CCDSolver`
- `FABRIKSolver`
- `JacobianTransposeSolver`
- `JacobianDampedLeastSquaresSolver`

如果后面要做对照，还可以补：

- `JacobianPseudoInverseSolver`

但第一版不建议先上 SVD 伪逆，原因是：

- 实现量大。
- 数值稳定性调参更复杂。
- 你的论文主线里 DLS 更适合作为工程默认实现。

## 7. FK 与矩阵层建议

### 7.1 FK 作为所有求解器共享基础

无论是 `CCD`、`FABRIK` 还是 `Jacobian`，都应该依赖统一的 FK 基础层。

建议抽出：

- `ForwardKinematics`
- `ChainPoseUtility`

FK 更新流程：

1. 根节点由根世界位姿初始化。
2. 每个子节点根据父节点世界旋转与自身局部偏移求世界位置。
3. 每个子节点根据父节点世界旋转和局部旋转求世界旋转。
4. 更新末端执行器世界位姿。

### 7.2 Jacobian 构建单独做模块

不要把 Jacobian 构建逻辑直接写在某一个 solver 里。

建议单独模块：

- `JacobianBuilder`

职责：

- 读取 `ChainDefinition + ChainState`
- 输出位置 Jacobian
- 后续可扩展输出姿态 Jacobian

这样你后面写：

- Transpose
- Pseudo-Inverse
- DLS

时都能复用同一套列构建逻辑。

## 8. 关节参数化建议

### 8.1 不要把欧拉角作为唯一内部真值

Inspector 可以展示 XYZ limit，但运行时不要把“欧拉角三元组”作为唯一求解状态。

建议：

- 状态层以 `Quaternion localRotation` 为主。
- Jacobian 增量层以“绕局部轴的小转角”表示。

也就是：

- 配置层是“这个关节允许绕哪些轴转”
- 迭代层是“这次每个自由轴转多少”
- 状态层最终合成为四元数

这是比较符合论文里 `q` 的自由度表示，又不会被 Unity 欧拉角细节绑死的方式。

### 8.2 推荐用“参考姿态 + 相对限位”

限位最好相对 `restLocalRotation` 定义，而不是直接相对当前局部旋转定义。

这样做的好处：

- 不同链初始化更稳定。
- 实验结果更可重复。
- 更符合“关节在参考姿态附近活动”的理解。

## 9. Unity 行为层建议

建议最终场景里有一个统一入口组件，例如：

- `IKChainController : MonoBehaviour`

职责：

- 从 Authoring 构建定义。
- 初始化运行时状态。
- 在 `LateUpdate` 中执行求解。
- 将结果回写到场景骨骼。
- 输出调试信息。

为什么优先 `LateUpdate`：

- 如果场景里还有普通动画，IK 一般放在动画更新之后。
- 更接近实际角色动画流程。

## 10. 实验路径建议

建议按下面顺序做，而不是一开始就把所有求解器混在一起写。

### 阶段 A：骨骼链与 FK 跑通

目标：

- 手动创建一个 2~4 骨骼链。
- 建立 `ChainDefinition` 和 `ChainState`。
- 让 FK 可以正确把局部旋转传播到末端位置。

验收标准：

- 改某个关节局部旋转，末端位置变化正确。
- 可以在 Gizmo 中看到每个关节的轴和末端位置。

### 阶段 B：解析 `Two-Bone`

目标：

- 先做最稳定、最容易验证的基线解法。
- 支持 target + pole vector。

价值：

- 便于验证骨骼链定义正确。
- 便于验证关节回写逻辑正确。

### 阶段 C：迭代法 `CCD`

目标：

- 熟悉“误差、迭代、终止条件”的基础流程。

建议先做：

- 仅位置目标
- 无朝向
- 限位先做最简单 clamp

### 阶段 D：`FABRIK`

目标：

- 对比 CCD 的收敛速度、平滑度与实现差异。

注意：

- FABRIK 核心是位置链更新。
- 最后要从位置结果重建关节旋转。

### 阶段 E：Jacobian Transpose

目标：

- 先建立 Jacobian 构造与梯度下降直觉。
- 这是 Jacobian 系方法的最小闭环。

### 阶段 F：Jacobian DLS

目标：

- 作为论文核心实验的主要数值法实现。

建议记录：

- 误差曲线
- 迭代次数
- 奇异位形附近行为
- 阻尼系数变化影响

## 11. 建议的命名规范

### 11.1 命名空间

统一使用：

- `GelerIK.Runtime.*`
- `GelerIK.Editor.*`
- `GelerIK.Tests.*`

### 11.2 类型命名

- 数据定义：`JointDefinition`, `ChainDefinition`
- 运行时状态：`JointState`, `ChainState`
- 配置：`IKSolverSettings`
- 行为组件：`IKChainController`
- 工具类：`ForwardKinematics`, `JacobianBuilder`

### 11.3 脚本命名规则

- 一个文件一个主类型。
- 文件名和主类型名一致。
- 不要再使用语义过宽的名字，比如单独的 `BoneData`。

## 12. 推荐先做的约束规范

第一版只做下面这些即可：

- `locked`
- `weight`
- 单轴或多轴旋转开关
- 简单角度限位

先不要急着做：

- 多末端执行器耦合
- 二级目标优化
- 零空间姿态优化
- 自适应阻尼
- 带碰撞或带地面约束的 IK

## 13. 建议补的调试工具

这个项目很适合“强可视化”，因为 IK 最怕黑盒调参。

建议尽早加：

- 关节轴 Gizmo
- 链段连线
- 目标点与当前末端点
- 每帧误差数值
- 迭代次数显示
- 被限位的关节高亮

如果后面做 Jacobian，建议再加：

- 每列 Jacobian 的方向可视化
- 奇异位形提示

## 14. 对当前代码的直接建议

当前文件：

- `Assets/IK/BoneData.cs`
- `Assets/IK/IKBone.cs`

建议不要继续围绕当前 `BoneData` 直接堆功能，而是把它视为一次早期草稿。

更推荐的调整方向：

- `BoneData` 重命名为更明确的 Authoring 或 Definition 类型。
- `IKBone` 不要只作为空壳骨骼组件，除非你明确要让每个骨骼都挂一个脚本。
- 更推荐一个“链级别控制器”管理整条骨骼链，而不是每根骨骼都带复杂行为。

换句话说：

- 骨骼节点负责“被引用”
- 链控制器负责“求解”
- 纯数据对象负责“描述”

## 15. 推荐的第一批最小类集合

如果按最小闭环来做，第一批只需要这些类型：

- `IKChainAuthoring`
- `JointDefinition`
- `ChainDefinition`
- `JointState`
- `ChainState`
- `ForwardKinematics`
- `IKChainController`
- `TwoBoneSolver`
- `CCDSolver`
- `IKSolveResult`

等这批稳定后，再加：

- `FABRIKSolver`
- `JacobianBuilder`
- `JacobianTransposeSolver`
- `JacobianDampedLeastSquaresSolver`
- `JointLimitUtility`

## 16. 最推荐的实现顺序

1. 先重构数据层，让定义、状态、场景绑定分开。
2. 写统一 FK。
3. 做一个可视化实验场景。
4. 先做 `Two-Bone` 验证链结构。
5. 再做 `CCD` 和 `FABRIK`。
6. 最后进入 `Jacobian` 系方法。

如果你按这个顺序推进，后面论文里的矩阵内容会自然落到：

- FK 模块
- JacobianBuilder
- DLS 求解器

而不是散落在各个 MonoBehaviour 里。

## 17. 当前阶段结论

现阶段最应该做的不是继续给 `BoneData` 加字段，而是先确定：

- 链级控制器
- 定义层
- 状态层
- FK 基础层
- 统一求解器接口

只要这五件事立住，后面你写任何一个求解器都不会推倒重来。
