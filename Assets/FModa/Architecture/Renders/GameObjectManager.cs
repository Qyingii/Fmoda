using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FModa.GameObjectManager
{
    /// <summary>
    /// 提供在场景中的游戏对象获取和管理
    /// </summary>
    public class GameObjectManager:MonoBehaviour
    {
        [Header("预制体")]
        //请在下面创建游戏对象，可以用于快速获取预制体
        // 单位预制体
        public GameObject unitPrefab;

        //具有对象池的单位预制体
        public GameObject unitPoolPrefab;


        /// <summary>
        /// 场景游戏对象
        /// </summary>
        public Dictionary<string, GameObject> gameObjects = new Dictionary<string, GameObject>();

        void OnDestroy()
        {
            Clear();
        }

        /// <summary>
        /// 初始化订阅
        /// </summary>
        public void Init()
        {
            SceneManager.sceneLoaded += ScanScene;

            ScanScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            gameObjects.Clear();
            SceneManager.sceneLoaded -= ScanScene;
        }

        /// <summary>
        /// 扫描整个场景，获取场景游戏对象
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="mode"></param>
        public void ScanScene(Scene scene, LoadSceneMode mode)
        {
            //获取场景中的所有游戏对象(包括未激活的)
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            //筛选出有ISGO接口的游戏对象
            foreach (GameObject obj in allObjects)
            {
                ISGO sgo = obj.GetComponent<ISGO>();

                //未有ISGO接口或不属于这个场景的游戏对象不处理
                if (sgo == null || (!string.IsNullOrEmpty(sgo.SceneName) && sgo.SceneName != scene.name))
                {
                    continue;
                }

                //将游戏对象添加到字典中
                gameObjects[sgo.SgoId] = obj;
            }
        }
    }

}
