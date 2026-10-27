---
name: fmoda-view-ui
description: 在 FModa/ECMVC Unity 项目中编写渲染器（Render）、UI 或对象池对象时使用。涵盖 UIBase 的 OnBind/OnUnbind/OnRefresh 生命周期、UIManager 的打开/关闭/复用/子 UI 规则、渲染器注册与注销数据的方式、Poolable 与 GF.Spawn/Despawn 的正确用法，以及视图层不得直接访问数据中心、不得用 Destroy 回收池对象等硬性约束。
---

# 渲染器 / UI / 对象池设计规范

视图层（Render、UI）的职责只有一个：**把模块的状态显示出来，把用户的输入转交给模块。** 它不做业务判断，不直接读写数据中心，不决定数据该不该存在。

```
输入 → Render/UI → Module（做决定） → DataCenter（存状态）
                        ↓ 广播业务事件
                     Render/UI（刷新显示）
```

---

## 1. 渲染器（Render）

渲染器是普通的 `MonoBehaviour`，每帧 `Update`，不走模块的逻辑帧 Tick。它负责动画、Transform、输入轮询这类「表现层」工作。

### 标准骨架

```csharp
using Conquer.Unit;
using UnityEngine;

public class Units : MonoBehaviour
{
    [SerializeField] private long unitId;
    public UnitData unitData;

    private IUnitManager unitManager;

    private void Start()
    {
        // 1. 数据为空时给默认值（示例做法；真实项目通常由生成方注入）
        unitData ??= new UnitData { maxHp = 100, hp = 100, moveSpeed = 2 };

        // 2. 获取模块：必须用具体类型
        unitManager = GF.GetModule<UnitManager>();

        // 3. 把数据注册进模块，拿回 id
        unitId = unitManager.Register(unitData);
    }

    private void Update()
    {
        // 4. 输入 → 模块，不在 Render 里改数据
        if (Input.GetKey(KeyCode.W))
            transform.Translate(unitManager.Move(unitId));

        if (Input.GetKeyDown(KeyCode.Q))
            unitManager.TakeDamage(unitId);
    }

    private void OnDestroy()
    {
        // 5. 生命周期对称：注册过就注销
        unitManager.UnRegister(unitId);
    }
}
```

### 规则

- **只获取模块，不创建模块。** `GF.GetModule<UnitManager>()`；不要在 Render 里 `CreateModule`。
- **输入只做转发。** Render 可以轮询输入，但状态变化必须调用模块方法；不要在 Render 里直接改 `UnitData.hp`。
- **不直接访问 `DataManager`。** 需要数据就通过模块的查询方法。
- **View 的生命周期要成对。** `Start` 里注册，`OnDestroy` 里注销；订阅事件则 `OnEnable` 订阅、`OnDisable` 退订，或与模块生命周期严格对应。
- **数据可能被外部删除。** `DataCenter.GetData(id)` 找不到时返回 `null` 并打警告。Render 如果继续用这个 id 调模块，模块内部可能空引用。模块应提供「是否还存在」的查询或删除事件；在拿到明确的删除通知前，Render 必须对返回值做空判断。
- **不要假设 `Start` 一定在 GF 初始化之后。** 场景中的 GF 与 Render 都是 `Awake` 阶段创建，`Start` 相对安全，但更稳妥的是在真正使用前取模块，并对 `null` 做处理。
- **需要池化时继承 `Poolable`**，用 `GF.Spawn / GF.Despawn`，不要 `Instantiate / Destroy`。

### 事件订阅型 Render

如果数据会被模块删除，Render 应订阅模块的业务事件（不是 DataCenter 的事件）：

```csharp
private void OnEnable()
{
    unitManager = GF.GetModule<UnitManager>();
    unitManager.OnUnitRemoved += HandleUnitRemoved; // 命名方法
}

private void OnDisable()
{
    unitManager.OnUnitRemoved -= HandleUnitRemoved;
}

private void HandleUnitRemoved(long id)
{
    if (id == unitId) GF.Despawn(gameObject);
}
```

> 注意：当前 `DataCenter<T>` 没有 `OnDataRemoved` 事件。如果模块需要通知删除，必须在模块接口上自己定义事件，并在模块的删除方法里广播。

---

## 2. UI（UIBase）

### 标准骨架

```csharp
using Conquer.Unit;
using TMPro;
using UnityEngine;

public class UnitHpPanel : UIBase
{
    [SerializeField] private TextMeshProUGUI _text;

    private IUnitManager unitManager;

    protected override void OnBind()
    {
        // 1. 取模块
        unitManager = GF.GetModule<UnitManager>();

        // 2. 订阅模块业务事件（命名方法）
        unitManager.OnHpChanged += RefreshAll;
    }

    protected override void OnUnbind()
    {
        // 3. 严格逆操作
        unitManager.OnHpChanged -= RefreshAll;
    }

    protected override void OnRefresh()
    {
        // 4. 绑定后做一次初始同步
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (_text == null) return;

        var all = unitManager.GetAllUnitData();
        // 只读展示，不修改数据
    }
}
```

### `UIBase` 的生命周期（由基类驱动，不要自己重复实现）

| 时机 | 基类行为 | 你要做什么 |
|---|---|---|
| `Start` | 非独立 UI 向 `UIManager.Register` 注册，分配 `id` | 不用管 |
| `OnEnable` | `OnBind()` → 标记已绑定 → `OnRefresh()` | 订阅事件、绑定按钮 |
| `OnDisable` | `OnUnbind()` → 清除绑定标记 | 退订事件、解绑按钮 |
| `OnDestroy` | 若仍绑定则 `OnUnbind()` | 不用重复写 |

- `_isBound` 保证 `OnBind / OnUnbind` 成对，不需要自己防重。
- **不要在 `OnBind` 里做耗时初始化**；它会在每次面板重新启用时执行。
- **不要在 `OnUnbind` 里 `Destroy` 自己**；关闭由 `UIManager` 负责。
- `OnRefresh` 是可选的，默认空实现；用于把模块当前值同步到控件。

### 按钮绑定

```csharp
[SerializeField] private Button closeButton;

protected override void OnBind()
{
    closeButton.onClick.AddListener(Close);   // Close 是 UIBase 的虚方法
}

protected override void OnUnbind()
{
    closeButton.onClick.RemoveListener(Close);
}
```

- 用**命名方法**，不要用 lambda：`AddListener(() => ...)` 之后无法用 `RemoveListener` 匹配。
- 按钮可以调用模块方法，但不要在按钮回调里直接改数据中心。
- 纯展示、无输入的 UI 可以不接 Controller/模块方法，只订阅事件。

### 不要做的事

- 不要在 UI 里 `DataManager.XXX`。视图与数据之间必须有模块这一层。
- 不要在 UI 里 `new` 模块或 `CreateModule`。
- 不要在 UI 里写业务规则，例如「血量小于 0 就删除数据」；这属于模块。
- 不要在 `Update` 里轮询业务状态。示例 `UnitHpPanel.Update` 只用于输入转发；更推荐用按钮或输入模块的事件订阅。

---

## 3. UI 管理（UIManager）

### 场景要求

- `UIManager` 要与 `GF` 放在**同一个 GameObject** 上（或至少同样 `DontDestroyOnLoad`），否则切场景时会被销毁。`GF.Awake()` 通过 `FindObjectOfType<UIManager>()` 查找它。
- UI 预制体放在 `Assets/Resources/UI/` 下；`OpenUI("BlankPanel")` 会加载 `Resources/UI/BlankPanel`。

### 打开 / 关闭 / 复用

```csharp
var ui = GF.ui.OpenUI("BlankPanel");     // 打开
GF.ui.CloseUI(ui.id);                    // 关闭
```

- `OpenUI` 先查「已关闭列表」：同名 UI 会被重新 `SetActive(true)` 并复用，不会重新实例化。
- 找不到才 `Resources.Load` 新实例。
- `CloseUI` 不会立刻销毁：UI 进入 `uiCloses`，超过 `cleanupTime`（默认 300s）才真正 `Destroy`。因此**关闭后不要继续持有引用**，也不要假设它已经销毁。
- UI 的 `id` 形如 `BlankPanel_1`，由 `UIManager` 分配；不要手改。
- `UIBase.Start()` 只在 `id` 不含 `_` 时向 `UIManager` 注册。动态创建的 UI 已经由 `CreateUI` 赋过带 `_` 的 id，所以不会重复注册；场景里直接摆的 UI 才会在 `Start` 自动注册。
- `independence = true` 的 UI 不通过 `UIManager` 注册和关闭，由调用方自己管理生命周期。

### 子 UI

```csharp
OpenSubUI("RedPanel");   // 在父 UI 内部打开
```

- 子 UI 会被 `SetParent(父 UI)`。
- 父 UI 关闭时，`UIBase.Disable()` 会级联关闭所有子 UI。
- 子 UI 被清理后，父 UI 通过 `OnUIClear` 清理字典里的空引用。
- 不要绕过 `OpenSubUI` 手动 `Instantiate` 子 UI。

### 关闭时到底发生了什么

`CloseUI(id)` 的顺序是：

1. 从 `uis` 移除；
2. `ui.Disable()`：关闭子 UI，并把 Transform 重置回预制体的位置/缩放/旋转；
3. `ui.ResetUI()`：子类可重写，清理业务状态；
4. `SetActive(false)`；
5. 加入 `uiCloses`，等待超过 `cleanupTime` 后销毁。

如果 UI 有需要复位的业务状态（选中项、输入框、滚动位置），重写 `ResetUI()`，不要依赖 `OnDisable`。

---

## 4. 对象池

### 使用方式

```csharp
// 生成
var go = GF.Spawn(prefab, position, rotation, parent);

// 回收
GF.Despawn(go);
```

`GF.Spawn` 会检查预制体上有没有 `Poolable`：

- 有 → 走对象池 `GF.pool.GetObject(prefab)`；
- 没有 → 直接 `Instantiate`。

`GF.Despawn` 同理：有 `Poolable` 就回池，没有就 `Destroy`。

### `Poolable` 子类

```csharp
using FModa.Pool;
using UnityEngine;

public class PoolUnit : Poolable
{
    [SerializeField] private float lifeTime = 3f;
    public float lifeTimer;

    private void Update()
    {
        if (lifeTimer > lifeTime)
            GF.Despawn(gameObject);
        else
            lifeTimer += Time.deltaTime;
    }

    public override void ActionOnRelease(GameObject go)
    {
        base.ActionOnRelease(go); // 必须调用：负责 SetActive(false)

        // 重置所有可变状态，否则下次复用时是脏的
        go.GetComponent<PoolUnit>().lifeTimer = 0f;
    }
}
```

### 规则

- **必须调用 `base.ActionOnRelease(go)`**，否则对象不会被禁用。
- **在 `ActionOnRelease` 里重置所有可变字段**：计时器、速度、目标、颜色、事件订阅、协程。池对象复用时不会重新执行 `Awake`。
- **不要 `Destroy` 池对象**；用 `GF.Despawn`。直接 `Destroy` 会让池里的记录失效，后续 `Release` 报错或产生重复对象。
- **不要用 `SetActive(false)` 代替 `Despawn`**；池不知道对象已经「死」了。
- `ActionOnGet` 可以用来做启用时的初始化，但不要依赖它重置状态——重置放在 `ActionOnRelease` 更可靠。
- 池以**源预制体**为 key。用预制体资产调用 `GF.Spawn`，不要用场景里的实例或池对象再作为 source，否则会建出多个池。
- `defaultCapacity` / `maxSize` 控制池大小；`collectionCheck` 打开时重复回收会报错，便于发现 bug。
- 对象池对象上的 `Update` 在对象被禁用时不会执行；如果逻辑依赖 `Update`，回收时一定要重置计时器。

---

## 5. 常见错误

| 错误 | 现象 | 正确做法 |
|---|---|---|
| UI/Render 直接 `DataManager.XXX` | 绕过模块，数据变化无人转发 | 通过模块方法和事件 |
| `OnBind` 订阅、`OnUnbind` 忘记退订 | 面板重新打开后重复回调/空引用 | 严格逆操作 |
| 用 lambda `AddListener` / 订阅事件 | 无法解绑 | 命名方法 |
| `Destroy(GF.Spawn(...))` | 池记录失效，后续回收报错 | `GF.Despawn` |
| 池对象 `ActionOnRelease` 不重置计时器 | 下次复用时立刻死亡/状态错乱 | 回收时重置所有可变状态 |
| 不调用 `base.ActionOnRelease` | 对象回收后仍然激活 | 第一行调用 base |
| 关闭 UI 后继续使用引用 | UI 可能已被销毁或复用 | 关闭后不持有引用 |
| 假设 `CloseUI` 立刻销毁 | 对象还在，`OnDestroy` 未触发 | 需要清理就重写 `ResetUI` |
| UI 预制体没放 `Resources/UI` | `OpenUI` 报找不到预制体 | 放到正确目录 |
| `UIManager` 与 `GF` 不在同一对象 | 切场景后 UI 管理器丢失 | 放同一 GameObject |
| Render 在 `Awake` 里取模块并使用 | GF 初始化顺序未定义，可能为 null | 在 `Start` 或使用前取，判空 |
| 用 `Input.GetKey` 与输入模块混用 | 两套输入配置/禁用逻辑不一致 | 统一走 `IInputManager` |

---

## 6. 自检清单

**Render**

- [ ] 继承 `MonoBehaviour`，每帧表现逻辑在 `Update`，业务规则不在 Render
- [ ] 模块用具体类型获取：`GF.GetModule<XxxManager>()`
- [ ] 注册/注销数据成对
- [ ] 事件订阅/退订成对，使用命名方法
- [ ] 不直接访问 `DataManager`
- [ ] 删除/空数据有判空或删除事件处理

**UI**

- [ ] 继承 `UIBase`，实现 `OnBind` / `OnUnbind`，按需重写 `OnRefresh`
- [ ] 预制体在 `Assets/Resources/UI/`
- [ ] 按钮监听成对增删，使用命名方法
- [ ] 只订阅模块事件，不订阅 DataCenter
- [ ] 关闭后不持有 UI 引用
- [ ] 需要复位的状态写在 `ResetUI`
- [ ] 独立 UI 设置 `independence`，并自行管理销毁

**对象池**

- [ ] 池对象继承 `Poolable`
- [ ] 用 `GF.Spawn` / `GF.Despawn`，不用 `Instantiate` / `Destroy`
- [ ] `ActionOnRelease` 调用 `base` 并重置所有可变状态
- [ ] 用预制体资产作为 `source`，不用实例
- [ ] 池大小和 `collectionCheck` 按需配置

**验证**

- [ ] Unity 编译通过
- [ ] Play `Assets/Example/Scenes/SampleScene.unity`：UI 可重复打开/关闭，池对象复用后状态正确
- [ ] 关闭 UI 后等待 `cleanupTime`，确认真正销毁且无残留回调

---

## 7. 参考文件

- 渲染器：`Assets/Example/Scripts/Renders/Unit/Units.cs`
- 池对象：`Assets/Example/Scripts/Renders/Unit/PoolUnit.cs`
- UI：`Assets/Example/Scripts/UI/UnitHpPanel.cs`、`BlankUI.cs`、`RedUI.cs`
- UI 基类：`Assets/FModa/Architecture/UI/UIBase.cs`
- UI 管理：`Assets/FModa/Architecture/UI/UIManager.cs`
- 对象池：`Assets/FModa/Architecture/Renders/Poolable.cs`、`PoolManager.cs`
