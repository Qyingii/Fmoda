using FModa.Pool;
using UnityEngine;

public class PoolUnit : Poolable
{
    //存活3s后自动死亡
    [SerializeField]private float lifeTime = 3f;
    [SerializeField]public float lifeTimer = 0f;

    void Update()
    {
        if (lifeTimer > lifeTime)
        {
            GF.Despawn(gameObject);
        }
        else
        {
            lifeTimer += Time.deltaTime;
        }
    }

    //回收时要重置
    public override void ActionOnRelease(GameObject go)
    {
        base.ActionOnRelease(go);

        go.GetComponent<PoolUnit>().lifeTimer = 0f;
    }
}
