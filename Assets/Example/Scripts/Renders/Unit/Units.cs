using Conquer.Unit;
using FModa.InputSystem;
using UnityEngine;

public class Units : MonoBehaviour
{
    [SerializeField]private long unitId;
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
        unitId = unitManager.Register(unitData); //注册单位
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
