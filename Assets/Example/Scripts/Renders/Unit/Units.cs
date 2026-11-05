using Conquer.Unit;
using FModa.InputSystem;
using UnityEngine;

public class Units : MonoBehaviour
{
    /// <summary>
    /// 单位 id，由生成方（Flow）在注册数据后通过 BindUnit 注入。
    /// -1 表示尚未注册；不参与 Inspector 序列化，避免预制体/场景复制时被写坏。
    /// </summary>
    [SerializeField, HideInInspector] private long unitId = -1;

    //单位模块
    private IUnitManager unitManager;
    private IInputManager inputManager;

    /// <summary>
    /// 由生成方在 Register 成功后调用，注入数据 id。
    /// </summary>
    public void BindUnit(long id)
    {
        if (id <= 0)
        {
            Debug.LogWarning($"[Units] 收到无效的单位 Id: {id}，已忽略");
            return;
        }

        unitId = id;
    }

    void Start()
    {
        unitManager = GF.GetModule<UnitManager>();
        inputManager = GF.GetModule<InputManager>();

        // 兜底：场景里静态摆放、没有生成方注入 id 的单位，在这里自注册一次。
        // 由 Flow 注入的单位（unitId > 0）不会走到这里，因此不会重复注册。
        if (unitId == -1 && unitManager != null)
        {
            BindUnit(unitManager.Register(new UnitData
            {
                maxHp = 100,
                hp = 100,
                moveSpeed = 3,
            }));
        }
    }

    void Update()
    {
        if (unitManager == null || inputManager == null) return;

        // id 失效（未注入 / 已被删除 / EndFlow 清空）时不再调用模块，避免空引用与刷屏
        if (!unitManager.TryGetUnitData(unitId, out _)) return;

        if(inputManager.KeyHold("MoveUp"))
        {
            transform.Translate(unitManager.Move(unitId));
        }

        if(inputManager.KeyDown("UnitDeductionOfBlood"))
        {
            unitManager.TakeDamage(unitId);
        }
    }

    void OnDestroy()
    {
        // 数据可能已经被 EndFlow/Clear 清掉，先确认 id 仍然有效再注销，避免无意义警告
        if (unitId > 0 && unitManager != null && unitManager.TryGetUnitData(unitId, out _))
            unitManager.UnRegister(unitId);
    }
}
