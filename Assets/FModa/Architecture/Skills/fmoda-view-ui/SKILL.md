---
name: fmoda-view-ui
description: 在 FModa/ECMVC Unity 项目中编写渲染器（Render）、UI 或对象池对象时使用。UI 面板必须继承 UIBase（不能直接 MonoBehaviour）并通过 GF.ui.OpenUI 接入 UIManager；Render 用普通 MonoBehaviour，通过 GF.sgo（Sgo/ISGO + GameObjectManager）获取场景对象与预制体，需要频繁生成时使用 Poolable + GF.Spawn/Despawn。涵盖 UIBase 的 OnBind/OnUnbind/OnRefresh 生命周期、UIManager 的打开/关闭/复用/子 UI 规则、场景对象管理、对象池用法，以及视图层不得直接访问数据中心等硬性约束。
---

# 渲染器 / UI / 对象池设计规范

视图层（Render、UI）负责**显示模块状态、转发用户输入和实现表现**。它不做业务判断，不直接读写数据中心，也不承担可复用的跨模块、跨层组合逻辑。

```text
自身输入 → Render/UI → Module（做决定） → DataCenter（存状态）
                           ↓ 广播业务事件
                        Render/UI（刷新显示）

跨模块或跨层动作 → Render/UI / Flow → Bond → Module / Render / UI
```

- 视图自身的一次查询、移动、扣血等输入转发可以直接调用模块。
- 卡牌变单位、生成实体并注册数据、注入视图 id、联动另一个面板等组合动作放 Bond。
- 视图可以通过事件或 `GF.GetBond<T>()` 提出跨层动作请求，但不能直接访问 `DataManager`。
- **切换流程只能在 Flow 中实现**。视图和 Bond 只发事件或返回结果，不能调用 `GF.flow.SwitchFlow`。

---

## 0. 先选对基类（写代码前必看）

| 你要写的东西 | 必须继承 / 使用 | 入口 |
|---|---|---|
| 游戏内 UI 面板 | **`UIBase`**（不能直接 `MonoBehaviour`） | `GF.ui.OpenUI("PrefabName")` |
| 场景表现对象、单位、特效、输入轮询 | `MonoBehaviour` | 挂在预制体或场景对象上 |
| 需要频繁生成 / 回收的对象 | `Poolable` | `GF.Spawn / GF.Despawn` |
| 获取场景内对象 | `Sgo` 组件（或实现 `ISGO`） | `GF.sgo.gameObjects["SgoId"]` |
| 获取预制体引用 | `GameObjectManager` 的 Inspector 字段 | `GF.sgo.xxxPrefab` |
| 子 UI | `UIBase.OpenSubUI(...)` | 不要手动 `Instantiate` |

> ⚠️ **最容易犯的错：UI 直接继承 `MonoBehaviour`。**
> `UIManager.CreateUI` 用 `TryGetComponent<UIBase>` 取组件，取不到会直接报错并返回 `null`：
> 面板拿不到 `OnBind / OnUnbind / OnRefresh` 生命周期，也不会进入 `UIManager` 的打开 / 关闭 / 复用体系，
> 表现为「`OpenUI` 之后界面没出现或完全不受管理」。
>
> 同理：UI 不要 `Instantiate`、不要 `new`、不要 `AddComponent<UIBase>` 绕过预制体；一律走 `GF.ui.OpenUI`。

---

## 1. 渲染器（Render）

渲染器是普通的 `MonoBehaviour`，每帧 `Update`，不走模块的逻辑帧 Tick。它负责动画、Transform、输入轮询这类「表现层」工作。

### 创建一个 Render 的步骤

1. 新建普通 `MonoBehaviour`（**不要**继承 `UIBase`，UI 才用 `UIBase`）。
2. 需要模块：在 `Start()` 或真正使用前 `GF.GetModule<具体类型>()`；只调用模块方法，不碰 `DataCenter`。
3. 需要场景对象 / 预制体：走 `GF.sgo`（见下一节），不要 `GameObject.Find` / `FindObjectOfType`。
4. 需要频繁生成 / 回收：继承 `Poolable`，用 `GF.Spawn / GF.Despawn`。
5. 每帧表现放 `Update`；事件订阅用 `OnEnable / OnDisable` 成对。

### 获取场景对象和预制体（`GF.sgo`）

`GameObjectManager` 由 GF 在 `Awake` 里通过 `GetComponent<GameObjectManager>()` 获取，**必须和 GF 挂在同一个 GameObject 上**。

**预制体引用**：在 `GameObjectManager` 的 Inspector 字段（如 `unitPrefab`、`unitPoolPrefab`，也可以自行添加字段）里拖入预制体，然后：

```csharp
var go = GF.Spawn(GF.sgo.unitPrefab, position, Quaternion.identity);
```

**场景内对象**：给目标对象加 `Sgo` 组件（或让已有脚本实现 `ISGO`），手动填写 `SgoId`（同一场景内唯一），需要限定场景时填 `SceneName`，然后：

```csharp
var panelGo = GF.sgo.gameObjects["UnitHpPanel"];
var hpPanel = panelGo.GetComponent<UnitHpPanel>();
```

如果对象已经有自己的脚本，也可以直接实现 `ISGO`，省掉额外的 `Sgo` 组件：

```csharp
using FModa.GameObjectManager;
using UnityEngine;

public class SceneMarker : MonoBehaviour, ISGO
{
    public string SgoId => "UnitHpPanel"; // 同一场景内唯一
    public string SceneName => "";        // 留空 = 所有场景都收录
}
```

规则：

- `SgoId` 是手填的字符串，改场景 / 复制对象时要保证唯一；`SceneName` 留空表示所有场景都收录，填了就只在对应场景收录。
- `GameObjectManager` 在 `Init()` 和每次 `sceneLoaded` 时扫描场景，字典会随场景重建；切场景后不要缓存旧的 GameObject 引用。
- 找不到 key 时先检查 `SgoId` 拼写、`SceneName` 是否匹配，以及对象是否已经随场景卸载。
- **不要用 `GameObject.Find` / `FindObjectOfType` 做场景装配**：改名、复制、失活都会让查找失效；`GF.sgo` 是统一的场景对象入口。
- 预制体**内部**的子物体引用直接用 `[SerializeField]` 拖拽即可；只有跨对象、按 ID 获取场景对象，或需要全局预制体引用时才用 `GF.sgo`。
- 场景对象上的脚本如果继承 `UIBase`，仍然通过 `GF.ui.OpenUI` 管理，不要用 `GF.sgo` 直接操作它的生命周期。

### 标准骨架

```csharp
using Conquer.Unit;
using UnityEngine;

public class Units : MonoBehaviour
{
    // id 由 Bond 在注册数据后注入；-1 表示未注册。不参与 Inspector 序列化，
    // 避免预制体复制、场景复制时把 id 写坏。
    [SerializeField, HideInInspector] private long unitId = -1;

    private IUnitManager unitManager;

    /// <summary>Bond 在 Register 成功后调用，注入数据 id。</summary>
    public void BindUnit(long id)
    {
        if (id > 0) unitId = id;
    }

    private void Start()
    {
        // 自身输入与查询可直接获取模块：必须用具体类型
        unitManager = GF.GetModule<UnitManager>();
    }

    private void Update()
    {
        // id 失效（未注入 / 已被删除 / 数据被清空）时不再调用模块
        if (!unitManager.TryGetUnitData(unitId, out _)) return;

        // 输入 → 模块，不在 Render 里改数据
        if (Input.GetKey(KeyCode.W))
            transform.Translate(unitManager.Move(unitId));

        if (Input.GetKeyDown(KeyCode.Q))
            unitManager.TakeDamage(unitId);
    }

    private void OnDestroy()
    {
        // 注销前确认 id 仍有效（流程结束或数据 Clear 之后可能已经失效）
        if (unitId > 0 && unitManager != null && unitManager.TryGetUnitData(unitId, out _))
            unitManager.UnRegister(unitId);
    }
}
```

### 规则

- **只获取模块，不在视图显式创建模块。** 自身业务调用使用 `GF.GetModule<UnitManager>()`；不要在 Render / UI 里 `CreateModule`。
- **跨层组合交给 Bond。** 数据注册、对象生成、id 注入、其他视图刷新组成一个动作时，由 Bond 的 `SpawnUnit` 之类方法统一完成，Flow 只调用它。
- **数据注册优先由 Bond 负责。** Bond 调用模块 `Register` 成功后，通过 `BindUnit(id)` 注入视图；同一份数据不能有两个写入者。
- **静态场景单位的兜底也走 Bond。** 示例 `Units.cs` 在 `unitId == -1` 时调用 `UnitBond.CreateUnitData()`，不要重新在视图里复制一套注册逻辑。
- **id 只当不透明句柄。** 存储 id 是「数据中心 Code + 本地序号」的合成值；需要显示时用模块暴露的 `ToLocalId(id)`。
- **输入只做转发。** Render 可以轮询输入，但状态变化必须调用模块方法；不要在 Render 里直接改 `UnitData.hp`。
- **不直接访问 `DataManager`。** 需要数据就通过模块查询；涉及多个模块或视图时使用 Bond。
- **订阅/退订成对。** 事件在 `OnEnable` 订阅、`OnDisable` 退订，使用命名方法。
- **数据可能被外部删除。** 模块必须提供 `TryGetUnitData(id, out data)` 这类安全查询；Render 在调用模块前确认 id 有效。
- **不要假设 `Start` 一定在 GF 初始化之后。** 更稳妥的是在真正使用前取模块并判空。
- **需要池化时继承 `Poolable`**，用 `GF.Spawn / GF.Despawn`。如果生成还伴随数据注册或视图联动，把 `Spawn` 一并封装进 Bond。
- **日志走 `GF.Log / GF.LogWarning / GF.LogError`**，不要直接 `Debug.Log`。
- **不要为视图另造公用类型**：静态数据放 `GameConfig`，公用枚举放 `GameEnum`；纯静态工具放 `PM`，涉及模块或视图的公共动作放 Bond，不能放 PM。
- **不要在视图切换流程。** 通过 Bond 或自身事件通知 Flow，由 Flow 自己实现切换。

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

### 创建一个 UI 的步骤

1. 预制体根节点挂一个**继承 `UIBase`** 的脚本；预制体放 `Assets/Resources/UI/`，文件名就是 `OpenUI` 用的名字（如 `Resources/UI/UnitHpPanel.prefab` 对应 `OpenUI("UnitHpPanel")`）。
2. 实现 `OnBind()` / `OnUnbind()`（必须）；需要初始同步就重写 `OnRefresh()`，需要复位状态就重写 `ResetUI()`。
3. 打开：`GF.ui.OpenUI("UnitHpPanel")`；关闭：面板内调 `Close()`，外部调 `GF.ui.CloseUI(id)`。
4. 不要手动 `Instantiate`、不要手动 `Register`、不要自己 `AddComponent<UIBase>`；实例化、id 分配、复用、延迟销毁都由 `UIManager` 负责。
5. 不希望被 `UIManager` 管理时设置 `independence = true`，并自行管理生命周期。
6. 预制体必须真正带 `UIBase` 子类组件；`UIManager.CreateUI` 用 `TryGetComponent<UIBase>` 校验，缺失会报错并返回 `null`。

> 推荐做法：UI 只负责显示和转发输入；按钮点击调用模块方法，不直接改数据。

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

- **不要让 UI 直接继承 `MonoBehaviour`。** 必须继承 `UIBase`，否则 `UIManager` 找不到组件，`OpenUI` 报错返回 `null`，也没有 `OnBind / OnUnbind / OnRefresh` 生命周期。
- 不要在 UI 里 `DataManager.XXX`。视图与数据之间必须有模块这一层。
- 不要在 UI 里 `new` 模块或 `CreateModule`。
- 不要手动 `Instantiate` UI、手动 `Register` 或 `AddComponent<UIBase>` 绕过 `UIManager`。
- 不要在 UI 里写业务规则，例如「血量小于 0 就删除数据」；这属于模块。
- 不要在 `Update` 里轮询业务状态。示例 `UnitHpPanel.Update` 只用于输入转发；更推荐用按钮或输入模块的事件订阅。
- 不要用 `GameObject.Find` / `FindObjectOfType` 找场景对象；用 `Sgo` + `GF.sgo`。

---

## 3. UI 管理（UIManager）

### 场景要求

- `UIManager` 要与 `GF` 放在**同一个 GameObject** 上（或至少同样 `DontDestroyOnLoad`），否则切场景时会被销毁。`GF.Awake()` 通过 `FindObjectOfType<UIManager>()` 查找它。
- UI 预制体放在 `Assets/Resources/UI/` 下；`OpenUI("BlankPanel")` 会加载 `Resources/UI/BlankPanel`。
- `uiRoot` 没配、被销毁或不在启用的 Canvas 下时，`CreateUI` 会通过 `ResolveUIRoot()` 重新在场景里找 `UIRoot`，仍找不到就自动创建带 Canvas 的 `UIRoot(自动)`。这只是兜底，正式项目仍应显式配置，避免层级和排序不可控。

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

### 创建一个池对象的步骤

1. 新建脚本继承 `Poolable`，挂在预制体上。
2. 在 `ActionOnRelease` 里调用 `base.ActionOnRelease(go)`，并重置所有可变状态。
3. 在 `GameObjectManager` 的 Inspector 字段里登记这个预制体（如 `unitPoolPrefab`），或通过其他方式持有预制体资产。
4. 生成：`GF.Spawn(GF.sgo.unitPoolPrefab, position, rotation)`；回收：`GF.Despawn(go)`。
5. 不要 `Instantiate` / `Destroy` 池对象，也不要用场景实例当池的 source。

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
| **UI 脚本直接继承 `MonoBehaviour`** | `UIManager` 报「该UI没有UIBase组件」，`OpenUI` 返回 `null`，面板不受管理 | 必须继承 `UIBase`，通过 `GF.ui.OpenUI` 打开 |
| 手动 `Instantiate` UI / 自己 `Register` | 不在 `UIManager` 的打开/关闭/复用体系内 | 统一走 `GF.ui.OpenUI` |
| 用 `GameObject.Find` / `FindObjectOfType` 找场景对象 | 改名、复制、失活、换场景后查找失效 | 给对象挂 `Sgo`，用 `GF.sgo.gameObjects["SgoId"]` |
| 场景对象没挂 `Sgo` / `SgoId` 重复 | `GF.sgo` 取不到，或被同名对象覆盖 | 挂 `Sgo`，`SgoId` 场景内唯一 |
| `GameObjectManager` 不与 `GF` 同对象 | `GF.sgo` 为 null，GF 初始化空引用 | 挂到与 `GF` 同一个 GameObject |
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

- [ ] 继承 `MonoBehaviour`（不是 `UIBase`），每帧表现逻辑在 `Update`，业务规则不在 Render
- [ ] 模块用具体类型获取：`GF.GetModule<XxxManager>()`
- [ ] 场景对象 / 预制体走 `GF.sgo`；场景对象已挂 `Sgo`/`ISGO` 且 `SgoId` 唯一
- [ ] 没有用 `GameObject.Find` / `FindObjectOfType` 做场景装配
- [ ] 注册/注销数据成对
- [ ] 事件订阅/退订成对，使用命名方法
- [ ] 不直接访问 `DataManager`
- [ ] 删除/空数据有判空或删除事件处理

**UI**

- [ ] **继承 `UIBase`（不是 `MonoBehaviour`）**，实现 `OnBind` / `OnUnbind`，按需重写 `OnRefresh`
- [ ] 预制体在 `Assets/Resources/UI/`，通过 `GF.ui.OpenUI("PrefabName")` 打开
- [ ] 没有手动 `Instantiate`、手动 `Register` 或 `AddComponent<UIBase>`
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
- 场景对象管理：`Assets/FModa/Architecture/Renders/GameObjectManager.cs`、`Sgo.cs`、`ISGO.cs`
