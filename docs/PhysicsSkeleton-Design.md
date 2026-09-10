# PhysicsSkeleton 详细设计

> 状态：设计稿（v2），供三个 AI 并行实现使用。
> 参考实现：`external/HL1RagdollMod-goldsrc/gsphysics`（Bullet 版本）。
> 目标引擎：Stride.BepuPhysics。

## 1. 目标与范围

本文档定义 GoldSrc 客户端物理中 **PhysicsSkeleton**（物理骨架）的完整设计，以及它与 `PhysicsController`、`HalfLifeBehavior`、`GoldsrcContentManager` 的对接方式。

实现将拆分为三部分并行进行：

1. **PhysicsSkeleton 核心 + PhysicsController 对接**（本文档主体），暂不含 `PreStudioModelRenderer` / `StudioModelRenderer`。
2. **GoldsrcContentManager — brush model 物理资源加载**。
3. **GoldsrcContentManager — studio model 物理资源加载（.gpd 解析）**。

---

## 2. 现有架构回顾

### 2.1 游戏系统执行顺序（GoldsrcClientGame）

| 顺序 | 系统 | UpdateOrder | 职责 |
|------|------|-------------|------|
| 1 | ClientSceneManagementSystem | -1000 | 实体 PVS 进出，创建/移除 Stride Entity，worldspawn 特殊处理 |
| 2 | GoldsrcTransformSyncSystem | -500 | Goldsrc→Stride Transform 同步（Update 阶段）；Stride→Goldsrc（Draw 阶段） |
| 3 | **PrePhysicsPoseSync（新增）** | **-200** | **Kinematic 实体物理姿态同步：SetPose** |
| 4 | GoldsrcSceneSystem | -100 | SceneInstance.Update，运行所有 Processor |
| 5 | PhysicsGameSystem | -49 | **Bepu 物理步进 + 动态体 Transform 回写** |
| 6 | GoldsrcScriptSystem | 0 | 运行 ScriptComponentBase.Update() |
| 7 | LateUpdateScriptSystem | int.MaxValue | 运行 ILateUpdate.LateUpdate() |

### 2.2 已实现的部分

- **ClientSceneManagementSystem**：在 `HUD_AddEntity` 标记可见实体，System Update 内增删 Stride Entity，通过 `SceneEntityLifecycleSystem` 订阅 `EntityAdded/Removed` 调用 `IEnterExitCallable.OnEnter/OnExit`。
- **HalfLifeBehavior**：实现 `IEnterExitCallable`，`OnEnter` 时创建 `PhysicsController`，`OnExit` 时 Disable。
- **PhysicsController**（当前为占位实现，需重写）。
- **GoldsrcTransformSyncSystem**：通过 `GoldsrcTransformLinkComponent.Authority` 决定同步方向（Goldsrc/Stride）。

### 2.3 关键时序约束

- **Kinematic 体的 SetPose 必须在物理步进（PhysicsGameSystem, -49）之前完成**，否则碰撞用上一帧的姿态。由新增的 `PrePhysicsPoseSync`（order -200）负责。
- **Dynamic 体（布娃娃临时实体）的根姿态提取必须在物理步进之后**，放在 `LateUpdate`（order int.MaxValue）中。

---

## 3. 物理对象类型

| 类型 | 来源 | 运动类型 | 说明 |
|------|------|----------|------|
| Static | worldspawn entity | Static | 整张地图的静态碰撞（brush mesh），使用 `StaticComponent` |
| Kinematic | entity type 为 Normal、Player 的实体 | Kinematic | 由 GoldSrc 动画驱动，物理仅做碰撞 |
| Dynamic | 布娃娃临时实体 | Dynamic | 死亡时创建，由物理模拟驱动，作为客户端 effect emit |

> **不再采用运动类型切换方案。** 角色死亡时创建独立的布娃娃临时实体（Dynamic），原 Kinematic 实体的物理骨架 detach 并切换为 NullPhysicsSkeleton。复活时 reattach。详见 §8。

---

## 4. 实体层级结构

### 4.1 普通实体（Normal / Player）

```
* Entity@<index>("<modelname>")              // 根实体
    - Component: PhysicsController           // Kinematic，不跟 Prefab，运行时重建
    - Component: HalfLifeBehavior              // 半条命物理行为逻辑
    - Component: GoldsrcTransformLinkComponent
    - Component: PlayerInfoComponent          // 仅 player 实体，mock 数据
    * PhysicsBone_<boneIndex>                 // 物理骨骼实体
        - Component: BodyComponent            // Kinematic=true
        - Component: CompoundCollider         // 子碰撞体的 offset 已烘焙进 PositionLocal/RotationLocal
        - Component: BoneLink                 // transient，LoadSkeleton 后被消费
        - Component: BallSocketConstraintComponent (A=self, B=next bone)
```

### 4.2 worldspawn 实体

```
* Entity@0("worldspawn")
    - Component: GoldsrcTransformLinkComponent
    * PhysicsBone_0_worldspawn                 // 单个物理骨骼
        - Component: StaticComponent
        - Component: MeshCollider             // 从 BSP 三角形构建
        - Component: BoneLink { BoneIndex = 0 }
```

不挂 `PhysicsController`、`HalfLifeBehavior`。worldspawn 是纯静态碰撞体。

### 4.3 布娃娃临时实体

```
* Ragdoll@<index>("<modelname>")              // 临时实体，emit 为 effect
    - Component: PhysicsController            // Dynamic，新实例
    - Component: RagdollBehavior              // 轻量脚本，仅做 LateUpdate 根姿态提取
    * PhysicsBone_<boneIndex>                  // 从原实体克隆（EntityCloner.Clone）
        - Component: BodyComponent             // Kinematic=false (dynamic)
        - Component: CompoundCollider
        - Component: BoneLink
        - Component: <ConstraintComponents>
```

不挂 `HalfLifeBehavior`。与原实体无关联。

### 4.4 命名规范

| 实体 | 命名 |
|------|------|
| 根实体（普通） | `Entity@<index>("<modelname>")` |
| 根实体（worldspawn） | `Entity@0("worldspawn")` |
| 布娃娃临时实体 | `Ragdoll@<index>("<modelname>")` |
| 物理骨骼（studio） | `PhysicsBone_<boneIndex>` |
| 物理骨骼（brush） | `PhysicsBone_0_<modelName>` |

---

## 5. 核心类型

### 5.1 BoneLink 组件

```csharp
/// <summary>
/// 瞬时组件，挂在物理骨骼实体上，描述该物理骨骼与动画骨骼的映射关系。
/// PhysicsController.LoadSkeleton 读取后构建内部索引，之后不再访问。
/// </summary>
public sealed class BoneLink : EntityComponent
{
    /// <summary>
    /// 对应的 GoldSrc studio model 骨骼索引。brush model 固定为 0。
    /// </summary>
    public int BoneIndex { get; set; }

    /// <summary>
    /// 是否为 jiggle/addon 骨骼（由物理驱动而非动画驱动）。
    /// 对应 gsphysics 中 UserRigidbodyType::Addon。
    /// </summary>
    public bool IsAddon { get; set; }
}
```

> **RigidOffset 已移除。** 刚体相对于骨骼的偏移已烘焙进 `CompoundCollider` 子碰撞体的 `PositionLocal`/`RotationLocal`。body（entity）位置直接等于骨骼位置，简化 SetPose/GetPose。

### 5.2 PhysicsSkeleton 概念

**PhysicsSkeleton 不是一个独立的类**，而是「一组拥有物理刚体/约束组件的实体」的统称。它就是 `PhysicsController.PhysicsBones` 所引用的实体集合，由 Prefab 实例化产生。

布娃娃创建时通过 `EntityCloner.Clone` 克隆原实体的物理骨骼实体，而非重新加载 Prefab。

### 5.3 PhysicsController（重新设计）

`PhysicsController` 放在根实体上，不跟 Prefab。模型变化时，HalfLifeBehavior 销毁旧物理骨骼实体、加载新 Prefab、实例化后挂到根实体下，再调用 `PhysicsController.LoadSkeleton` 重建内部数据。

```csharp
using GoldsrcFramework.LinearMath;

public sealed class PhysicsController : EntityComponent
{
    // ── 公共状态 ──────────────────────────────────────────
    public PhysicsMotionType MotionType { get; set; } = PhysicsMotionType.Kinematic;
    public Entity? PivotPhysicsBone { get; private set; }
    public Matrix ModelRootOffset { get; private set; } = Matrix.Identity;

    // 模型标识（native 数据，用于校验模型是否变化）
    public IntPtr ModelPointer { get; private set; }   // 非 player：native model_t*
    public string? ModelName { get; private set; }      // player：模型名（来自 PlayerInfoComponent）
    public bool IsPlayer { get; private set; }
    public bool RagdollRigged { get; private set; }

    // ── 内部数据（由 LoadSkeleton 构建）────────────────────
    private readonly List<Entity> physicsBones = [];
    private readonly List<int> boneIndices = [];        // physicsBones[i] 对应的动画骨骼索引
    private readonly List<bool> isAddon = [];           // physicsBones[i] 是否为 addon
    private readonly List<ConstraintComponentBase> regularConstraints = [];
    private readonly List<ConstraintComponentBase> addonConstraints = [];

    // ── 备份（DetachSkeleton 时使用）──────────────────────
    private List<Entity>? detachedBones;

    public IReadOnlyList<Entity> PhysicsBones => physicsBones;
    public bool IsNullSkeleton => physicsBones.Count == 0;
}
```

#### 5.3.1 LoadSkeleton

```csharp
/// <summary>
/// 从已实例化的物理骨骼实体构建内部索引，并计算 PivotPhysicsBone / ModelRootOffset。
/// 调用前需将物理骨骼实体添加为根实体的子节点。
/// </summary>
public void LoadSkeleton(IEnumerable<Entity> physicsBoneEntities, IntPtr modelPointer, bool isPlayer)
public void LoadSkeleton(IEnumerable<Entity> physicsBoneEntities, string modelName, bool isPlayer)
```

步骤：

1. 清空内部列表。
2. 遍历 `physicsBoneEntities`，对每个实体读取 `BoneLink` 组件，填充 `physicsBones`、`boneIndices`、`isAddon`。
3. 收集所有约束组件（`BallSocket/Hinge/SwivelHinge`），按是否连接 addon 骨骼分为 `regularConstraints` 和 `addonConstraints`。
4. 计算 PivotPhysicsBone 与 ModelRootOffset（见 §6）。
5. 应用当前 MotionType：Kinematic 时所有非 addon 骨骼设为 `Kinematic=true`，addon 保持 `Kinematic=false`；Dynamic 时所有非 addon 骨骼设为 `Kinematic=false`。

> 若传入的实体集合为空，视为 **NullPhysicsSkeleton**：`physicsBones` 为空列表，`PivotPhysicsBone = null`，后续 `SetPose`/`GetPose` 均为 no-op。

#### 5.3.2 SetPose

```csharp
/// <param name="pose">GoldSrc studio model 骨骼的**世界变换**矩阵数组（Matrix3x4），索引为动画骨骼索引。
/// brush model 只使用 pose[0]（模型原点世界变换）。</param>
/// <param name="settingMask">可选，为 false 的骨骼跳过（用于禁用 jiggle 骨骼）。</param>
public void SetPose(ReadOnlySpan<Matrix3x4> pose, bool[]? settingMask = null)
```

对每个物理骨骼 i：

1. 若 `settingMask` 存在且该骨骼被禁用 → 跳过。
2. 将 `Matrix3x4` 转换为 Stride `Matrix`（转置 3x3 部分 + 移动 translation）。
3. 调用 `SetPhysicsBonePose(physicsBones[i], strideMatrix)`。

> **Matrix3x4 → Stride Matrix 转换**：GoldSrc `Matrix3x4` 是 column-vector 约定（translation 在 M14/M24/M34），Stride `Matrix` 是 row-vector 约定（translation 在 M41/M42/M43）。转换需转置 3x3 部分并移动 translation 位置。

#### 5.3.3 GetPose

```csharp
/// <param name="pose">输出数组（Matrix3x4），索引为动画骨骼索引。pose[0] 为模型原点世界变换。</param>
/// <param name="settingMask">可选，为 false 的骨骼跳过写入。</param>
public void GetPose(Span<Matrix3x4> pose, bool[]? settingMask = null)
```

1. 计算模型原点：若 `PivotPhysicsBone` 非空，将 `PivotPhysicsBone.Transform.WorldMatrix * ModelRootOffset` 转为 `Matrix3x4` 写入 `pose[0]`。
2. 对每个物理骨骼 i：将 `physicsBones[i].Transform.WorldMatrix` 转为 `Matrix3x4` 写入 `pose[boneIndices[i]]`。

> 无 RigidOffset，直接取骨骼 WorldMatrix，无需逆乘运算。

#### 5.3.4 SetPhysicsBonePose

```csharp
/// <summary>
/// 设置单个物理骨骼的姿态。Helper 函数，SetPose 内部也使用此函数。
/// </summary>
public void SetPhysicsBonePose(Entity physicsBone, Matrix pose)
{
    pose.Decompose(out var translation, out var rotation, out _);
    if (physicsBone.Get<BodyComponent>() is { } body)
        body.Teleport(translation, rotation);
    // StaticComponent 无需 Teleport（静态体不动）
}
```

> `Teleport` 内部会调用 `UpdateTransformationComponent`，自动将世界坐标转换为相对于父实体的局部坐标，并设置 `bone.Transform.Position/Rotation`。对于 kinematic=true 的 BodyComponent，BepuPhysics 不会自动从 Entity.Transform 同步，必须显式 Teleport。

#### 5.3.5 Enable / Disable

```csharp
public void Enable();   // 根据 MotionType 设置所有骨骼的 Kinematic 状态和约束启停
public void Disable();  // 将所有 BodyComponent 设为 Kinematic=true（休眠），禁用所有约束
```

`Disable` 不销毁物理骨骼实体（实体可能因离开 PVS 而被移除出场景，但保留以便重新进入时复用）。

#### 5.3.6 DetachSkeleton / ReattachSkeleton

```csharp
/// <summary>
/// 将物理骨骼实体从根实体和场景移除，切换为 NullPhysicsSkeleton。
/// 用于角色死亡时，原 Kinematic 实体的物理骨架退出物理世界。
/// </summary>
public void DetachSkeleton()
{
    // 备份
    detachedBones = [..physicsBones];

    // 从根实体移除子节点
    foreach (var bone in physicsBones)
        Entity.Transform.Children.Remove(bone.Transform);

    // 从场景移除（BepuPhysics 自动停止模拟）
    foreach (var bone in physicsBones)
        game.Remove(bone);  // 或通过 SceneInstance 移除

    // 切换为 NullPhysicsSkeleton
    physicsBones.Clear();
    boneIndices.Clear();
    isAddon.Clear();
    regularConstraints.Clear();
    addonConstraints.Clear();
    PivotPhysicsBone = null;

    RagdollRigged = true;
}

/// <summary>
/// 将备份的物理骨骼实体重新挂回根实体并加入场景，重建内部索引。
/// 用于角色复活时恢复物理骨架。
/// </summary>
public void ReattachSkeleton()
{
    if (detachedBones is null || detachedBones.Count == 0) return;

    foreach (var bone in detachedBones)
    {
        Entity.Transform.Children.Add(bone.Transform);
        game.Add(bone);
    }

    // 重建内部索引
    LoadSkeleton(detachedBones, ModelPointer, IsPlayer);  // 或 LoadSkeleton(detachedBones, ModelName, IsPlayer)

    detachedBones = null;
    RagdollRigged = false;
}
```

#### 5.3.7 模型校验

```csharp
public bool ValidateModel(IntPtr currentPointer)
{
    if (IsPlayer) return false;
    return ModelPointer == currentPointer;
}

public bool ValidateModel(string currentName)
{
    if (!IsPlayer) return false;
    return string.Equals(ModelName, currentName, StringComparison.OrdinalIgnoreCase);
}
```

- 非 player：native pointer 比较（O(1)，无托管对象开销）。
- player：字符串比较（来自 `PlayerInfoComponent`，mock 数据）。

### 5.4 PlayerInfoComponent

```csharp
/// <summary>
/// 临时组件，提供 player 实体的模型名等信息。数据先 mock，后续对接 native。
/// </summary>
public sealed class PlayerInfoComponent : EntityComponent
{
    public string ModelName { get; set; } = "";
}
```

由 `ClientSceneManagementSystem.CreateEntity` 在创建 player 实体时添加。

---

## 6. PivotPhysicsBone 与 ModelRootOffset

### 6.1 用途

布娃娃（Dynamic）模式下，物理驱动各骨骼姿态。需要从物理姿态反推模型原点（根实体 Transform）。取一个锚点骨骼（Pivot），利用其在 T-pose 时相对于模型原点的偏移，运行时反算模型原点。

### 6.2 计算（LoadSkeleton 中）

1. 从 `physicsBones` 中找到 `boneIndices[i] == 0`（根骨骼）的物理骨骼。若没有，取 `boneIndices` 最小的。
2. 该实体即为 `PivotPhysicsBone`。
3. 此时根实体处于初始姿态（T-pose，根实体 Transform 为 Identity 或其初始值），物理骨骼实体的 `Transform.LocalMatrix` 在 Prefab 实例化时已被设置为 T-pose 下的值。
4. `ModelRootOffset = Matrix.Invert(PivotPhysicsBone.Transform.WorldMatrix)`。
   - 即 T-pose 时：`modelOrigin = PivotBoneWorld * ModelRootOffset = PivotBoneWorld * PivotBoneWorld⁻¹ = Identity`，成立。

> 无 RigidOffset，`PivotPhysicsBone.Transform.WorldMatrix` 直接就是骨骼世界变换。

### 6.3 运行时使用（Dynamic 模式，LateUpdate）

```
pivotWorld = PivotPhysicsBone.Transform.WorldMatrix  // 物理步进后已被 SyncActiveTransforms 更新
rootWorld  = pivotWorld * ModelRootOffset
设置根实体 Transform = rootWorld
对所有物理骨骼 re-teleport（修正局部坐标，见 §7.2）
```

---

## 7. 姿态更新时序

### 7.1 Kinematic 模式（普通实体）

```
GoldsrcTransformSyncSystem.Update (-500)
    → cl_entity_t → 根实体 Transform
PrePhysicsPoseSync (-200)
    → 遍历所有 Kinematic 实体的 HalfLifeBehavior
    → brush:  SetPose([rootEntity.WorldMatrix 转 Matrix3x4])
    → studio: pose = PreStudioModelRenderer.SetupBones(entity); SetPose(pose)
    → SetPose 内部：Matrix3x4 → Stride Matrix → SetPhysicsBonePose → body.Teleport
PhysicsGameSystem (-49)
    → 物理步进，kinematic 体参与碰撞
```

> `PrePhysicsPoseSync` 是新增 GameSystem，遍历场景中所有 `PhysicsController`（MotionType == Kinematic && !RagdollRigged && !IsNullSkeleton），调用其 `SetPose`。pose 来源（brush: 根实体世界变换；studio: SetupBones）由调用方决定，本部分暂不实现 StudioModelRenderer 相关，brush 模式的 pose 可直接从根实体 Transform 转换。

### 7.2 Dynamic 模式（布娃娃临时实体）

```
PhysicsGameSystem (-49)
    → 物理步进（布娃娃 bones 参与模拟）
    → SyncActiveTransforms：bone body.Pose → bone.Transform.Local
GoldsrcScriptSystem (0)
    → （布娃娃临时实体无 HalfLifeBehavior，无 Update）
LateUpdateScriptSystem (int.MaxValue)
    → RagdollBehavior.LateUpdate:
        1. pivotWorld = PivotPhysicsBone.Transform.WorldMatrix
        2. rootWorld = pivotWorld * ModelRootOffset
        3. 设置布娃娃根实体 Transform = rootWorld
        4. 对每个物理骨骼：SetPhysicsBonePose(bone, bone.Transform.WorldMatrix)
           （re-teleport：用新的 rootWorld 重新计算局部 Transform，使 bone.WorldMatrix == body.Pose）
```

步骤 4 的 re-teleport 是必须的：修改根实体 Transform 后，子实体的 WorldMatrix 会被 TransformProcessor 重新计算（`root * local`），偏离物理体姿态。re-teleport 修正局部坐标。Teleport 对物理体是 no-op（姿态相同），仅修正实体局部 Transform。

### 7.3 GetPose 的使用时机（StudioModelRenderer）

StudioModelRenderer 绘制 studio model 时，对 Dynamic 实体通过 `PhysicsController.GetPose(pose)` 获取骨骼世界变换（`Matrix3x4`），替换原本由动画计算的骨骼变换。此时 `TransformProcessor` 已更新 WorldMatrix，且 LateUpdate 的 re-teleport 已完成。

### 7.4 RagdollRigged 状态下的原实体

```
PrePhysicsPoseSync (-200)
    → PhysicsController.IsNullSkeleton == true → SetPose 直接返回（no-op）
PhysicsGameSystem (-49)
    → NullPhysicsSkeleton，无物理体参与
GoldsrcScriptSystem (0)
    → HalfLifeBehavior.Update: RagdollRigged == true，不做物理操作
LateUpdateScriptSystem (int.MaxValue)
    → HalfLifeBehavior.LateUpdate: RagdollRigged == true，不做物理操作
```

---

## 8. 布娃娃机制

### 8.1 设计概述

不采用运动类型切换。角色死亡时：
1. 创建独立的布娃娃临时实体（Dynamic），通过 `EntityCloner.Clone` 克隆原实体的物理骨骼。
2. 原实体的 `PhysicsController.DetachSkeleton()`：物理骨骼移出物理世界，切换为 NullPhysicsSkeleton，标记 `RagdollRigged = true`。
3. 布娃娃临时实体 emit 为客户端 effect，与原实体无关联。

复活时：
1. 原实体的 `PhysicsController.ReattachSkeleton()`：恢复备份的物理骨骼，重建索引，`RagdollRigged = false`。
2. 布娃娃临时实体不管，自有生命周期。

### 8.2 RagdollHelper

```csharp
public static class RagdollHelper
{
    /// <summary>
    /// 为实体创建布娃娃临时实体。
    /// 克隆原实体物理骨骼，创建 Dynamic PhysicsController，初始化姿态，加入场景。
    /// 然后对原实体 DetachSkeleton。
    /// </summary>
    public static void CreateRagdollFor(Entity originalEntity, ReadOnlySpan<Matrix3x4> initialPose)
    {
        var physController = originalEntity.Get<PhysicsController>();
        if (physController is null || physController.IsNullSkeleton) return;

        // 1. 克隆物理骨骼
        var clonedBones = new List<Entity>();
        foreach (var bone in physController.PhysicsBones)
        {
            var cloned = EntityCloner.Clone(bone);  // 深克隆，含所有组件
            clonedBones.Add(cloned);
        }

        // 2. 创建临时实体
        int entindex = originalEntity.Get<ClEntityComponent>().EntityIndex;
        var ragdollEntity = new Entity($"Ragdoll@{entindex}");
        foreach (var bone in clonedBones)
            ragdollEntity.Transform.Children.Add(bone.Transform);

        // 3. 创建 Dynamic PhysicsController
        var ragdollPhys = new PhysicsController
        {
            MotionType = PhysicsMotionType.Dynamic
        };
        ragdollEntity.Components.Add(ragdollPhys);
        ragdollPhys.LoadSkeleton(clonedBones, physController.ModelPointer, physController.IsPlayer);

        // 4. 初始化姿态（从当前动画帧）
        ragdollPhys.SetPose(initialPose);

        // 5. 添加轻量行为（仅 LateUpdate 根姿态提取）
        ragdollEntity.Components.Add(new RagdollBehavior());

        // 6. 加入场景（emit 为 effect）
        game.Add(ragdollEntity);

        // 7. 原实体 detach
        physController.DetachSkeleton();
    }
}
```

### 8.3 RagdollBehavior

```csharp
/// <summary>
/// 轻量脚本，仅做 LateUpdate 根姿态提取。挂在布娃娃临时实体上。
/// </summary>
public sealed class RagdollBehavior : ScriptComponentBase, ILateUpdate
{
    private PhysicsController? phys;

    public override void Start()
    {
        phys = Entity.Get<PhysicsController>();
    }

    public void LateUpdate()
    {
        if (phys is null || phys.IsNullSkeleton || phys.PivotPhysicsBone is null) return;
        phys.ApplyRootTransformFromPivot(Entity);
    }
}
```

`PhysicsController.ApplyRootTransformFromPivot(Entity rootEntity)`：

```csharp
public void ApplyRootTransformFromPivot(Entity rootEntity)
{
    if (PivotPhysicsBone is null) return;

    // 从 Pivot 骨骼世界变换反推根实体变换
    var pivotWorld = PivotPhysicsBone.Transform.WorldMatrix;
    var rootWorld = Matrix.Multiply(pivotWorld, ModelRootOffset);
    rootEntity.Transform.WorldMatrix = rootWorld;

    // re-teleport 所有骨骼（修正局部坐标）
    foreach (var bone in physicsBones)
    {
        SetPhysicsBonePose(bone, bone.Transform.WorldMatrix);
    }
}
```

### 8.4 死亡 / 复活检测

由 `HalfLifeBehavior.Update` 负责检测（具体死亡序列判断逻辑由脚本层实现，此处仅定义接口）：

```csharp
// HalfLifeBehavior.Update
if (IsPlayingDeathSequence && !PhysicsController.RagdollRigged)
{
    // 获取当前动画姿态作为布娃娃初始姿态
    // var initialPose = PreStudioModelRenderer.SetupBones(Entity);
    RagdollHelper.CreateRagdollFor(Entity, initialPose);
}
else if (!IsPlayingDeathSequence && PhysicsController.RagdollRigged)
{
    PhysicsController.ReattachSkeleton();
}
```

---

## 9. NullPhysicsSkeleton

当模型没有对应的物理数据（如缺失 .gpd 文件）时，`GoldsrcContentManager.IsExist` 返回 false。HalfLifeBehavior 不加载 Prefab，对 `PhysicsController` 调用 `LoadSkeleton(empty)`。

- `physicsBones` 为空列表
- `PivotPhysicsBone = null`
- `SetPose`/`GetPose` 直接返回
- `Enable`/`Disable`/`DetachSkeleton`/`ReattachSkeleton` 为 no-op
- 实体仍正常渲染（由 GoldSrc 原生渲染管线处理），只是没有物理碰撞

---

## 10. 约束（Constraints）

### 10.1 .gpd 约束类型 → Stride 映射

| gsphysics 约束类型 | Bepu 约束 | Stride.BepuPhysics 组件 |
|---------------------|-----------|------------------------|
| spherical (POINT2POINT) | BallSocket | `BallSocketConstraintComponent` |
| hinge | Hinge | `HingeConstraintComponent` |
| cone (CONETWIST) | SwivelHinge | `SwivelHingeConstraintComponent` |

> 首版用 `SwivelHingeConstraintComponent`（无角度限位）。`SwivelHingeConstraintComponent` 本身不带角度限位（仅提供 swivel + hinge 自由度和 spring 设置）。若后续需要限位，可额外挂载 `SwingLimitConstraintComponent` 和 `TwistLimitConstraintComponent`。

### 10.2 约束组件放置

约束组件挂在物理骨骼实体上（通常是骨骼 A 的实体）。通过 `A`/`B` 属性引用两个 `BodyComponent`。Prefab 构建时设置好引用。

### 10.3 约束的 LocalOffset

gsphysics 中约束有 `LocalInA`/`LocalInB`（约束点在各自刚体局部空间的位置）。Stride 的 `BallSocketConstraintComponent.LocalOffsetA/LocalOffsetB`、`HingeConstraintComponent.LocalOffsetA/LocalOffsetB + LocalHingeAxisA/B` 对应。

### 10.4 Addon 约束识别

约束连接的两个骨骼中，若任一为 addon 骨骼（jiggle），则该约束归入 `addonConstraints`，在 kinematic 模式下保持启用（让 jiggle 骨骼能被动画骨骼拖拽摆动）。

---

## 11. GoldsrcContentManager 接口

```csharp
public interface IContentManager
{
    /// <summary>
    /// 检查物理资源是否存在（如 .gpd 是否存在且校验通过）。
    /// </summary>
    bool IsExist(string key);

    /// <summary>
    /// 加载强类型资源。T 可为 Model（视觉模型）或 Prefab（物理骨架模板）。
    /// 调用前应先 IsExist 确认存在。
    /// </summary>
    T? Load<T>(string key) where T : class;

    /// <summary>
    /// 地图加载时预缓存所有 brush model 的物理 Prefab。
    /// </summary>
    void PreloadBrushModels(/* native map data */);
}
```

使用模式：

```csharp
if (contentManager.IsExist(key))
{
    var prefab = contentManager.Load<Prefab>(key);
    // 实例化
}
```

### 11.1 物理 Prefab 的 key 规则

| Key 格式 | 含义 | 加载方式 |
|----------|------|----------|
| `"*<index>"` | brush model，按 model index | 从 BSP 三角形构建 MeshCollider Prefab |
| `"models/xxx.mdl"` | studio model，按模型路径 | 将 `.mdl` 替换为 `.gpd`，解析为 Prefab |

### 11.2 模型标识管理

- **非 player 实体**：用 native `model_t*`（IntPtr）管理。HalfLifeBehavior 从 `cl_entity_t` 获取当前 model pointer，与 `PhysicsController.ModelPointer` 比较。
- **player 实体**：用模型名（string）管理。从 `PlayerInfoComponent.ModelName` 获取，与 `PhysicsController.ModelName` 比较（忽略大小写）。因为 player model 是按名字加载的，modelindex 不可靠。

---

## 12. Prefab 结构

### 12.1 Studio Model Prefab（由 .gpd 解析构建）

每个物理骨骼实体：
- `Transform.LocalMatrix` = 该骨骼 T-pose 世界变换（在根实体为 Identity 时，Local == World）
  - RigidOffset 已移除，offset 烘焙进 CompoundCollider 子碰撞体的 `PositionLocal`/`RotationLocal`
- `BodyComponent`，`Kinematic = true`（默认）
- `CompoundCollider`，包含一个或多个子碰撞体（Box/Capsule/Sphere），每个子碰撞体有 `PositionLocal`/`RotationLocal`
  - 子碰撞体 offset = gpd `CollideOffset` + shape 自身 offset（组合变换）
- `BoneLink { BoneIndex, IsAddon }`

约束：
- 挂在骨骼实体上，设置 `A`/`B` 引用和 LocalOffset/Axis。

### 12.2 Brush Model Prefab（由 BSP 数据构建）

单个物理骨骼实体：
- `Transform.LocalMatrix = Identity`（brush model 的碰撞体就在模型原点）
- `StaticComponent`（worldspawn）或 `BodyComponent` + `Kinematic=true`（其他 brush，如门）
  - 除 worldspawn 外，其他 brush model 均为 Kinematic
- `MeshCollider`（从 BSP 三角形构建）或 `ConvexHullCollider`
- `BoneLink { BoneIndex = 0, IsAddon = false }`

无约束。

### 12.3 碰撞体尺寸

gsphysics 的 .gpd 中碰撞体尺寸以 GoldSrc 单位存储。Stride.BepuPhysics 使用相同的单位（本项目 GoldSrc 与 Stride 尺度一致，无需缩放）。

- `BoxCollider.Size` = gpd `halfextent * 2`
- `CapsuleCollider.Radius` = gpd `radius`；`Length` = gpd `height - 2 * radius`
- `SphereCollider.Radius` = gpd `radius`

> **已确认**：Stride.BepuPhysics 的 `Capsule(Radius, Length)` 中 Length 是**圆柱段长度**（不含两端半球），与 `GeometricPrimitive.Capsule.New(length, radius)` 一致。gsphysics 中 `btCapsuleShapeX(r, h - 2*r)` 同样用总高减去两端半径。因此 `Length = gpd.height - 2 * gpd.radius`。

### 12.4 Mass

暂时统一默认值（如 mass=1）。所有 `BodyComponent` 的质量相同。后续可在 .gpd 中支持每骨骼质量。

---

## 13. HalfLifeBehavior 集成

```csharp
public sealed class HalfLifeBehavior : ScriptComponentBase, IEnterExitCallable, ILateUpdate
{
    public PhysicsController? PhysicsController { get; private set; }
    public bool IsPlayer { get; set; }
    public bool IsPlayingDeathSequence { get; set; }

    public void OnEnter()
    {
        PhysicsController ??= new PhysicsController();
        Entity.Components.Add(PhysicsController);
        EnsureSkeletonLoaded();
        PhysicsController.Enable();
    }

    public void OnExit() => PhysicsController?.Disable();

    public override void Update(GameTime gameTime)
    {
        if (PhysicsController is null) return;

        // RagdollRigged 状态下不做模型校验和物理操作
        if (PhysicsController.RagdollRigged)
        {
            // 检测复活
            if (!IsPlayingDeathSequence)
                PhysicsController.ReattachSkeleton();
            return;
        }

        EnsureSkeletonLoaded();

        // 死亡 → 布娃娃
        if (IsPlayingDeathSequence && !PhysicsController.RagdollRigged)
        {
            // var initialPose = PreStudioModelRenderer.SetupBones(Entity);
            // RagdollHelper.CreateRagdollFor(Entity, initialPose);
        }
    }

    public void LateUpdate()
    {
        // Kinematic 实体不需要 LateUpdate（根姿态由 GoldsrcTransformSyncSystem 驱动）
        // RagdollRigged 状态下也不需要（NullPhysicsSkeleton）
    }

    private void EnsureSkeletonLoaded()
    {
        // 获取当前模型标识
        IntPtr currentPointer = /* 从 cl_entity_t 获取 */;
        if (IsPlayer)
        {
            string currentName = Entity.Get<PlayerInfoComponent>()?.ModelName ?? "";
            if (!PhysicsController.ValidateModel(currentName))
                ReloadSkeleton(modelKey: currentName, isPlayer: true);
        }
        else
        {
            if (!PhysicsController.ValidateModel(currentPointer))
                ReloadSkeleton(modelKey: /* 从 pointer 推导 */, isPlayer: false);
        }
    }

    private void ReloadSkeleton(string modelKey, bool isPlayer)
    {
        // 销毁旧物理骨骼实体
        foreach (var bone in PhysicsController.PhysicsBones)
            Entity.Transform.Children.Remove(bone.Transform);

        if (ContentManager.IsExist(modelKey))
        {
            var prefab = ContentManager.Load<Prefab>(modelKey);
            var bones = Instantiate(prefab);  // 实例化并挂到根实体
            PhysicsController.LoadSkeleton(bones, modelKey, isPlayer);
        }
        else
        {
            PhysicsController.LoadSkeleton([], modelKey, isPlayer);  // NullPhysicsSkeleton
        }
    }
}
```

---

## 14. worldspawn 处理

采用方案 B：在 `ClientSceneManagementSystem.ProcessEntityLifecycle` 中用 `hasWorldspawn` flag 一次性创建。

```csharp
// ClientSceneManagementSystem
private bool hasWorldspawn = false;

public void MarkEntityVisible(int entindex)
{
    if (entindex == 0) return;  // worldspawn 不参与 PVS
    thisFrameVisible.Add(entindex);
}

protected override void ProcessEntityLifecycle(GameTime gameTime)
{
    if (!hasWorldspawn)
    {
        var worldspawn = CreateWorldspawnEntity();
        activeEntities[0] = worldspawn;
        game.Add(worldspawn);
        hasWorldspawn = true;
    }

    // 原有 PVS 逻辑不变
    // worldspawn 不在 thisFrameVisible/lastFrameVisible 中，不参与增删
    ...
}

private Entity CreateWorldspawnEntity()
{
    var entity = new Entity("Entity@0(\"worldspawn\")");
    entity.Components.Add(new GoldsrcTransformLinkComponent());

    // 加载 worldspawn brush model Prefab（model index 1）
    var prefab = ContentManager.Load<Prefab>("*1");
    if (prefab != null)
    {
        var bone = Instantiate(prefab);
        entity.Transform.Children.Add(bone.Transform);
    }

    return entity;
}
```

worldspawn 不挂 `PhysicsController`、`HalfLifeBehavior`，不进入 `thisFrameVisible`/`lastFrameVisible` 两个 HashSet，但存在于 `activeEntities` 中。

---

## 15. PrePhysicsPoseSync 系统

新增 GameSystem，负责在物理步进前同步 Kinematic 实体的物理姿态。

```csharp
public sealed class PrePhysicsPoseSync : GameSystemBase
{
    public PrePhysicsPoseSync(IServiceRegistry services) : base(services)
    {
        UpdateOrder = -200;  // 在 GoldsrcTransformSyncSystem(-500) 之后，PhysicsGameSystem(-49) 之前
    }

    public override void Update(GameTime time)
    {
        // 遍历场景中所有 PhysicsController
        foreach (var phys in GetPhysicsControllers())
        {
            if (phys.MotionType != PhysicsMotionType.Kinematic) continue;
            if (phys.RagdollRigged) continue;
            if (phys.IsNullSkeleton) continue;

            // brush: 从根实体世界变换构造 pose
            // studio: pose = PreStudioModelRenderer.SetupBones(entity)
            // phys.SetPose(pose);
            //
            // 注：pose 来源由具体实现决定，本部分暂只处理 brush model
        }
    }
}
```

---

## 16. 实现拆分

### 第 1 部分：PhysicsSkeleton 核心 + PhysicsController 对接

**文件**：
- `src/GoldsrcFramework.Ecs/BoneLink.cs`（新增）
- `src/GoldsrcFramework.Ecs/PhysicsController.cs`（重写）
- `src/GoldsrcFramework.Ecs/HalfLifeBehavior.cs`（修改）
- `src/GoldsrcFramework.Ecs/RagdollHelper.cs`（修改）
- `src/GoldsrcFramework.Ecs/RagdollBehavior.cs`（新增）
- `src/GoldsrcFramework.Ecs/PlayerInfoComponent.cs`（新增）
- `src/GoldsrcFramework.Ecs/PrePhysicsPoseSync.cs`（新增）
- `src/GoldsrcFramework.Ecs/ClientSceneManagementSystem.cs`（修改：worldspawn 处理）

**职责**：
- `BoneLink` 组件（`BoneIndex`、`IsAddon`，无 `RigidOffset`）。
- `PhysicsController`：`LoadSkeleton`、`SetPose(ReadOnlySpan<Matrix3x4>)`、`GetPose(Span<Matrix3x4>)`、`SetPhysicsBonePose(Entity, Matrix)`、`Enable`/`Disable`、`DetachSkeleton`/`ReattachSkeleton`、`ApplyRootTransformFromPivot`、`ValidateModel`（native pointer / string）、NullPhysicsSkeleton 处理、约束收集与启停。
- `HalfLifeBehavior`：模型变化检测与骨架重载、死亡/复活切换（via RagdollHelper / ReattachSkeleton）。
- `RagdollHelper`：`CreateRagdollFor`（静态，`EntityCloner.Clone` 克隆骨骼，创建临时 Dynamic 实体，初始化姿态，DetachSkeleton）。
- `RagdollBehavior`：轻量脚本，LateUpdate 根姿态提取。
- `PlayerInfoComponent`：mock 数据。
- `PrePhysicsPoseSync`：Kinematic SetPose 系统。
- `ClientSceneManagementSystem`：worldspawn `hasWorldspawn` flag 处理。

**不含**：`PreStudioModelRenderer`、`StudioModelRenderer`、pose 来源的具体实现（SetPose 的 pose 数组由调用方提供）。

### 第 2 部分：GoldsrcContentManager — brush model 物理资源

**职责**：
- 实现 `IContentManager` 的 brush model 分支（`IsExist`、`Load<Prefab>`、`PreloadBrushModels`）。
- 输入 `"*<index>"`，获取 `model_t*`，从 BSP 表面（`msurface_t` + `medge_t` + `mvertex_t`）提取三角形，构建 `MeshCollider`（或 `ConvexHullCollider`）。
- 构造单实体 Prefab：
  - worldspawn（model index 1）：`StaticComponent` + `MeshCollider` + `BoneLink{0}`
  - 其他 brush：`BodyComponent(Kinematic=true)` + `MeshCollider` + `BoneLink{0}`
- 命名：`PhysicsBone_0_<modelName>`
- 参考 gsphysics `PhysicsAssetsManager.LoadModelByIndex`（mod_brush 分支）。

### 第 3 部分：GoldsrcContentManager — studio model 物理资源（.gpd）

**职责**：
- 实现 .gpd 解析（XML 格式，根节点 `<goldsrc-physics-data>`）。
- 解析 `collision-shape-block` → `CompoundCollider`（Box/Capsule/Sphere/Compound），offset 烘焙进子碰撞体 `PositionLocal`/`RotationLocal`。
- 解析 `rigidbody-block` → 物理骨骼实体（BodyComponent + CompoundCollider + BoneLink）。
- 解析 `constraint-block` → 约束组件（BallSocket/Hinge/SwivelHinge），设置 A/B 引用和 LocalOffset/Axis。
- 校验 checksum（SHA1 of studiohdr_t），不匹配则 `IsExist` 返回 false。
- 计算每个物理骨骼的 T-pose 局部变换并设置到实体 Transform（需访问 studiohdr_t 的 bone 默认值和父子关系）。
- 命名：`PhysicsBone_<boneIndex>`
- Mass 统一默认值。
- 参考 gsphysics `ConstructionInfo.Parse` 和 `PhysicsAssetsManager.LoadModelByIndex`（mod_studio 分支）。

---

## 17. 性能考量

| 关注点 | 方案 |
|--------|------|
| 模型标识比较 | native pointer 比较（非 player），O(1)；player 用字符串 |
| Pose 数组 | `Span<Matrix3x4>`（struct，无托管对象分配） |
| BoneLink | 瞬时，LoadSkeleton 后可移除 |
| PhysicsBones | `List<Entity>`（struct handle，高效） |
| NullPhysicsSkeleton | 早返回，零开销 |
| Kinematic SetPose | ~15-20 骨骼 × ~32 实体 ≈ 480 Teleport/帧，可接受 |
| 无 RigidOffset | SetPose/GetPose 无矩阵乘法，直接 Teleport / 直接读 WorldMatrix |
| 约束启停 | `ConstraintComponentBase.Enabled` 标记，Bepu 内部处理 |
| 布娃娃克隆 | `EntityCloner.Clone` 仅在死亡时执行（低频） |

---

## 18. 已解决问题

| # | 问题 | 结论 |
|---|------|------|
| 1 | Kinematic SetPose 执行阶段 | 新增 `PrePhysicsPoseSync`（order -200） |
| 2 | Player model name 获取 | 新增 `PlayerInfoComponent`（mock 数据） |
| 3 | SwivelHinge 限位 | 首版无限位约束 |
| 4 | worldspawn 处理 | 方案 B：`hasWorldspawn` flag，不参与 PVS |
| 5 | Brush model Body vs Static | 除 worldspawn 外均为 Kinematic |
| 6 | 物理骨骼命名 | studio: `PhysicsBone_<boneIndex>`；brush: `PhysicsBone_0_<modelName>` |
| 7 | .gpd mass | 统一默认值 |
| 8 | RigidOffset | 移除，offset 烘焙进 CompoundCollider 子碰撞体 |
| 9 | SetPose/GetPose 签名 | 用 `GoldsrcFramework.LinearMath.Matrix3x4` 的 `Span` |
| 10 | SetPhysicsBonePose | 用 Stride `Matrix`，helper 函数 |
| 11 | 模型标识 | 非 player 用 native IntPtr；player 用字符串 |
| 12 | 布娃娃方案 | 临时实体替代 + DetachSkeleton/ReattachSkeleton |
| 13 | RagdollHelper | 静态，`EntityCloner.Clone` 克隆骨骼 |
| 14 | 布娃娃 PhysicsController | 新实例，Dynamic |
| 15 | 布娃娃渲染 | 临时实体渲染，emit 为 effect，与原实体无关联 |
| 16 | Stride Capsule Length | 圆柱段长度，`Length = height - 2*radius` |
