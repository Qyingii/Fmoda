using UnityEngine;

namespace FModa.GameObjectManager
{
    public class Sgo : MonoBehaviour, ISGO
    {
        /// <summary>
        /// 跨场景游戏对象id,用于被加载到指定的字段中，该值由用户手动填写，请确保每个场景内每个游戏对象唯一
        /// </summary>
        [Tooltip("跨场景游戏对象id,用于被加载到指定的字段中，该值由用户手动填写，请确保每个场景内每个游戏对象唯一")][SerializeField] private string sgoid;

        /// <summary>
        /// 场景名称，用于标记该游戏对象应该在哪个场景中被加载，不写则默认加载
        /// </summary>
        [Tooltip("场景名称，用于标记该游戏对象应该在哪个场景中被加载，不写则默认加载")][SerializeField] private string sceneName;
        public string SgoId => sgoid;

        public string SceneName => sceneName;
    }
}

