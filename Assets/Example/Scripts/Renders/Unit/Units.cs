using Conquer.Unit;
using FModa.InputSystem;
using UnityEngine;

public class Units : MonoBehaviour
{
    public UnitData unitData;

    //单位模块
    private IUnitManager unitManager;
    private IInputManager inputManager;

    void Start()
    {
        //确保单位数据不为空
        if(unitData == null)
        {
            unitData = new UnitData()
            {
                maxHp = 100,
                hp = 100,
                moveSpeed = 2,
            };
        }

        //获取单位模块
        unitManager = GF.GetModule<UnitManager>();
        inputManager = GF.GetModule<InputManager>();
        unitManager.Register(unitData); //注册单位
    }


    void Update()
    {
        if(inputManager.KeyHold("MoveUp"))
        {
            transform.Translate(unitManager.Move(unitData.Id));
        }

        if(inputManager.KeyDown("UnitDeductionOfBlood"))
        {
            unitManager.TakeDamage(unitData.Id);
        }
    }

    void OnDestroy()
    {
        unitManager.UnRegister(unitData.Id);
    }
}
