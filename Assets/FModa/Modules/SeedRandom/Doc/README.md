# SeedRandom 带种子的随机数模块

## 用途

提供两种随机能力：

1. **当前种子序列**：调用 `SetSeed` 后连续生成可复现的随机数；重新设置相同种子会从头得到相同序列。
2. **独立种子随机**：调用带 `seed` 参数的方法，相同参数始终得到相同结果，不读取或修改当前种子，也不会推进当前随机序列。

未调用 `SetSeed`，或调用 `ClearSeed()` 后，无独立种子参数的方法回退到 `UnityEngine.Random`。

所有区间均为**左闭右开 `[minInclusive, maxExclusive)`**，包括浮点型区间。

## 目录结构

```text
Assets/FModa/Modules/SeedRandom/
├── SeedRandomManager.cs
├── Interface/
│   └── ISeedRandomManager.cs
├── Data/
│   ├── SeedRandomData.cs
│   └── SeedRandomDataCenter.cs
└── Doc/
    └── README.md
```

种子状态通过 `DataManager.SeedRandom` 按需自动注册，CenterCode 由当前运行时的注册顺序分配。`System.Random` 的序列推进状态是模块内部实现，不存入数据中心。

## 获取模块

在 Flow / Module / Bond / Render / UI 中按职责获取具体类型：

```csharp
using FModa.SeedRandom;

var random = GF.GetModule<SeedRandomManager>();
```

Flow 可以直接获取 `SeedRandomManager` 并调用 `SetSeed` 等单模块 API；只有随机步骤同时协调多个模块或 Render / UI 时，才需要由 Bond 封装。

## 当前种子序列

```csharp
var random = GF.GetModule<SeedRandomManager>();

random.SetSeed(12345);

int value = random.NextInt(1, 10);       // [1, 10)
float ratio = random.NextFloat();        // [0, 1)
float speed = random.NextFloat(2f, 5f);  // [2, 5)

random.ClearSeed();                      // 后续回退到 UnityEngine.Random
```

重新设置相同种子会重置序列：

```csharp
random.SetSeed(42);
int a1 = random.NextInt(0, 100);
int a2 = random.NextInt(0, 100);

random.SetSeed(42);
int b1 = random.NextInt(0, 100);
int b2 = random.NextInt(0, 100);

// a1 == b1，a2 == b2
```

可查询当前状态：

```csharp
bool hasSeed = random.HasSeed;
int? seed = random.CurrentSeed; // 未设置时为 null
```

## 独立种子随机

```csharp
int value = random.NextInt(12345, 1, 10);
float speed = random.NextFloat(12345, 2f, 5f);
```

这两个方法每次都会用传入种子创建独立序列并取第一个结果，因此：

- 相同参数始终得到相同结果；
- 不需要先调用 `SetSeed`；
- 不改变 `HasSeed` / `CurrentSeed`；
- 不推进 `SetSeed` 创建的连续随机序列。

## API

| 成员 | 返回值 / 行为 |
|---|---|
| `bool HasSeed { get; }` | 是否设置了当前种子 |
| `int? CurrentSeed { get; }` | 当前种子；未设置时为 `null` |
| `SetSeed(int seed)` | 设置种子并重新开始可复现序列 |
| `ClearSeed()` | 清除种子，回退到 `UnityEngine.Random` |
| `NextInt()` | `[0, int.MaxValue)` |
| `NextInt(min, max)` | `[min, max)` 的整数；使用当前种子或普通随机 |
| `NextInt(seed, min, max)` | 使用独立种子生成 `[min, max)` 的整数 |
| `NextFloat()` | `[0, 1)` 的浮点数；使用当前种子或普通随机 |
| `NextFloat(min, max)` | `[min, max)` 的浮点数；使用当前种子或普通随机 |
| `NextFloat(seed, min, max)` | 使用独立种子生成 `[min, max)` 的浮点数 |

## 边界与异常

- `min < max`：正常返回 `[min, max)` 内的值。
- `min == max`：直接返回该边界值，不消耗当前随机序列。
- `min > max`：抛出 `ArgumentException`，不会自动交换边界。
- 浮点边界为 `NaN` 或正负 `Infinity`：抛出 `ArgumentException`。
- 浮点计算使用 `double` 插值，并在舍入碰撞时退回上界的前一个 `float`，避免返回 `maxExclusive`。
- 当两个边界是相邻的 `float` 时，区间内不存在第三个 `float`，结果可能始终等于下界。

## 生命周期与注意事项

- 模块是纯逻辑普通类，不需要挂到场景。
- `OnRemove()` 会清空当前种子状态；重新获取模块后需要重新调用 `SetSeed`。
- `DataManager.PreWarm()` 会替换数据中心实例，应在启动初期执行；不要在已经设置种子的运行阶段随意再次预热。
- 本模块面向游戏逻辑复现，不用于密码学安全随机数。
- `System.Random` 的具体序列与运行时实现有关；跨不同运行时版本做长期存档复现时，应固定运行环境。
- `DataManager` 存档只保存 `hasSeed / seed`，不保存连续序列已推进的位置。覆盖导入后如需从种子开头重新生成，由上层重新调用 `SetSeed`；已有种子状态时，Append 导入第二条状态会被拒绝。
- 模块实例及其连续随机序列不是线程安全 API，请在同一逻辑线程中使用。
