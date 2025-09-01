# Root Motion 系统使用指南

## 概述

`MyTestController3` 现在支持完整的 Root Motion 动画驱动移动系统，可以结合代码控制和动画驱动的移动，提供更自然和精确的角色移动。

## 业界方案参考

### 1. KCC + Root Motion 方案
- **原理**: 使用 `OnAnimatorMove()` 收集动画的 `deltaPosition` 和 `deltaRotation`
- **优势**: 与 KinematicCharacterController 完美集成，支持复杂的物理交互
- **适用**: 需要精确物理控制的3D游戏

### 2. Animancer Root Motion 方案
- **原理**: 利用 Animancer 的 `deltaPosition` 和 `deltaRotation` 属性
- **优势**: 与 Animancer 动画系统无缝集成，支持复杂的动画状态管理
- **适用**: 使用 Animancer 的动画系统

### 3. 混合方案
- **原理**: 结合代码控制的移动和动画驱动的移动
- **优势**: 灵活性高，可以根据不同状态选择不同的移动方式
- **适用**: 需要动态调整移动行为的游戏

## 功能特性

### 核心功能
- ✅ **Root Motion 移动**: 支持动画驱动的移动
- ✅ **Root Motion 旋转**: 支持动画驱动的旋转
- ✅ **混合移动**: 代码控制 + 动画驱动的混合移动
- ✅ **状态感知**: 根据角色状态自动调整移动方式
- ✅ **倍率控制**: 可调节 Root Motion 的强度
- ✅ **实时切换**: 运行时动态启用/禁用 Root Motion

### 参数配置

#### Root Motion 参数
```csharp
[Header("Root Motion参数")]
public bool UseRootMotion = true;             // 是否使用Root Motion
public bool UseRootMotionForRotation = true;  // 是否使用Root Motion进行旋转
public float RootMotionMultiplier = 1f;       // Root Motion倍率
public float RootMotionRotationMultiplier = 1f; // Root Motion旋转倍率
public bool BlendRootMotionWithCode = true;   // 是否混合Root Motion和代码控制
public float RootMotionBlendWeight = 0.8f;    // Root Motion混合权重
```

## 使用方法

### 1. 基本设置

#### 在 Inspector 中配置
1. 选择角色对象，找到 `MyTestController3` 组件
2. 在 "Root Motion参数" 部分配置相关参数
3. 确保动画片段包含 Root Motion 数据

#### 代码配置
```csharp
// 启用 Root Motion
characterController.SetUseRootMotion(true);

// 设置倍率
characterController.SetRootMotionMultiplier(1.2f);

// 设置混合权重
characterController.SetRootMotionBlendWeight(0.8f);
```

### 2. 动画设置

#### Unity Animator 设置
1. 选择动画片段
2. 在 Inspector 中启用 "Loop Pose" 和 "Root Transform Position (Y)"
3. 根据需要调整 "Root Transform Rotation (Y)"

#### Animancer 设置
```csharp
// 确保 Animancer 组件启用 Root Motion
animancerComponent.applyRootMotion = true;
```

### 3. 状态特定配置

系统会根据角色状态自动调整 Root Motion 行为：

```csharp
// 着陆状态：完全使用 Root Motion
if (CurrentCharacterState == CharacterState.Landing)
{
    blendWeight = 1f;
}

// 跳跃状态：完全使用代码控制
if (CurrentCharacterState == CharacterState.Jumping)
{
    blendWeight = 0f;
}
```

## 高级用法

### 1. 动态调整混合权重

```csharp
// 根据动画状态调整混合权重
switch (currentAnimationState)
{
    case AnimationState.Jump_Land:
        characterController.SetRootMotionBlendWeight(1f);
        break;
    case AnimationState.Walk_Forward:
        characterController.SetRootMotionBlendWeight(0.8f);
        break;
    default:
        characterController.SetRootMotionBlendWeight(0.5f);
        break;
}
```

### 2. 获取 Root Motion 数据

```csharp
// 获取当前 Root Motion 增量
Vector3 rootMotionDelta = characterController.GetRootMotionDelta();

// 检查是否有 Root Motion
if (rootMotionDelta.magnitude > 0.01f)
{
    Debug.Log($"Root Motion: {rootMotionDelta}");
}
```

### 3. 强制应用 Root Motion

```csharp
// 强制应用内置 Root Motion
characterController.ForceApplyRootMotion(true);
```

## 调试和优化

### 1. 启用调试信息

```csharp
// 在 Inspector 中启用动画调试
_EnableAnimationDebug = true;
```

### 2. 使用 RootMotionExample 脚本

将 `RootMotionExample.cs` 脚本添加到场景中，可以：
- 实时监控 Root Motion 数据
- 动态调整参数
- 在 Scene 视图中可视化 Root Motion 向量

### 3. 性能优化建议

1. **避免频繁调用**: 不要在每帧都调用设置方法
2. **缓存状态**: 缓存当前动画状态，避免重复计算
3. **合理使用混合**: 不要过度使用混合，可能导致性能问题

## 常见问题

### Q: Root Motion 不工作？
A: 检查以下几点：
- 确保动画片段包含 Root Motion 数据
- 确保 `UseRootMotion` 已启用
- 确保 Animancer 组件的 `applyRootMotion` 已启用

### Q: 移动看起来不自然？
A: 尝试调整以下参数：
- 降低 `RootMotionBlendWeight` 值
- 调整 `RootMotionMultiplier` 倍率
- 检查动画片段的 Root Motion 设置

### Q: 性能问题？
A: 优化建议：
- 减少混合权重的动态调整频率
- 使用状态机来管理 Root Motion 设置
- 考虑在特定状态下禁用 Root Motion

## 最佳实践

1. **渐进式实现**: 先实现基本的 Root Motion，再添加混合功能
2. **状态驱动**: 根据角色状态自动调整 Root Motion 参数
3. **测试覆盖**: 在不同地形和条件下测试 Root Motion 行为
4. **性能监控**: 监控 Root Motion 对性能的影响
5. **用户反馈**: 根据玩家反馈调整 Root Motion 参数

## 扩展功能

### 1. 自定义 Root Motion 处理器

```csharp
public class CustomRootMotionHandler : MonoBehaviour
{
    public Vector3 ProcessRootMotion(Vector3 originalMotion)
    {
        // 自定义处理逻辑
        return originalMotion * customMultiplier;
    }
}
```

### 2. 动画事件集成

```csharp
// 在动画事件中调整 Root Motion
public void OnAnimationEvent(string eventName)
{
    if (eventName == "StartRootMotion")
    {
        characterController.SetUseRootMotion(true);
    }
    else if (eventName == "StopRootMotion")
    {
        characterController.SetUseRootMotion(false);
    }
}
```

这个 Root Motion 系统提供了灵活且强大的动画驱动移动解决方案，可以根据项目需求进行定制和扩展。
