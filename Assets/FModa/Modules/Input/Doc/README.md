# 输入系统使用文档
## 用途
- 用于统一管理输入事件
- 可以用来配置自定义按键、修饰按键等
- 基本和Unity的InputManager一致的用法
- 具体用法可以参考`Assets\Example\Scripts\UI\UnitHpPanel.cs`

## 使用方法
- 具体看`Assets\FModa\Modules\Input\Interface\IInputManager.cs`，这里包含所有可用方法
主要有两种使用方法：轮询和事件
### 轮询
- 轮询方法会返回一个布尔值，表示当前帧是否按下该键
```csharp
    private IInputManager inputManager;
    void Start()
    {
        //获取模块
        inputManager = GF.GetModule<InputManager>();
    }

    void Update()
    {
        //判断是否按住
        if(inputManager.KeyHold("MoveUp"))
        {
            transform.Translate(unitManager.Move(unitId));
        }

        //判断是否按下
        if(inputManager.KeyDown("UnitDeductionOfBlood"))
        {
            unitManager.TakeDamage(unitId);
        }

        //判断是否释放
        if(inputManager.KeyUp("UnitDeductionOfBlood"))
        {
            unitManager.TakeDamage(unitId);
        }
    }
```
### 事件
- 事件方法会在按下或释放该键时触发
```csharp
    private IInputManager inputManager;
    void Start()
    {
        //获取模块
        inputManager = GF.GetModule<InputManager>();
        //注册事件
        //当按下时触发
        inputManager.SubscribeStarted("MoveUp", () => { transform.Translate(unitManager.Move(unitId)); });
        //当释放时触发
        inputManager.SubscribePerformed("UnitDeductionOfBlood", () => { unitManager.TakeDamage(unitId); });
        //当抬起时触发，位于Performed之后
        inputManager.SubscribeCanceled("UnitDeductionOfBlood", () => { unitManager.TakeDamage(unitId); });
    }

    void OnDestroy()
    {
        //取消注册事件
        inputManager.UnSubscribeStarted("MoveUp", () => { transform.Translate(unitManager.Move(unitId)); });
        inputManager.UnSubscribePerformed("UnitDeductionOfBlood", () => { unitManager.TakeDamage(unitId); });
        inputManager.UnSubscribeCanceled("UnitDeductionOfBlood", () => { unitManager.TakeDamage(unitId); });
    }
```

## 注意事项
