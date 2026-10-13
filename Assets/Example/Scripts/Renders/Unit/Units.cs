using System.Collections;
using System.Collections.Generic;
using Conquer.Unit;
using UnityEngine;

public class Units : MonoBehaviour
{
    [SerializeField]private int id;

    //单位模块
    private IUnitManager unitManager;

    void Start()
    {

        //获取单位模块
        unitManager = GF.GetModule<UnitManager>();
    }


    void Update()
    {
        if(Input.GetKey(KeyCode.W))
        {
            unitManager.Move(gameObject,id);
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            unitManager.EndFlow();
        }

        if(Input.GetKeyDown(KeyCode.Q))
        {
            unitManager.TakeDamage(id);
        }
    }

    //设置编号
    public void SetId(int id)
    {
        this.id = id;
    }
}
