using Conquer.Unit;
using FModa.InputSystem;
using UnityEngine;

public class Units : MonoBehaviour
{

    /// <summary>
    /// 单位ID
    /// </summary>
    public long unitId=-1;

    //单位模块
    private IUnitManager unitManager;
    private IInputManager inputManager;

    void Start()
    {
        unitManager = GF.GetModule<UnitManager>();
        inputManager = GF.GetModule<InputManager>();
        //对已经注册的单位不再次注册
        if (unitId!=-1) return;

        //确保单位数据不为空
        //这是仅仅用于初始化，不保存数据，数据都保存在数据中心
        var unitData = new UnitData()
        {
            maxHp = 100,
            hp = 100,
            moveSpeed = 3,
        };

        //获取单位模块
        unitId=unitManager.Register(unitData); //注册单位
    }


    void Update()
    {
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
        unitManager.UnRegister(unitId);
    }
}
