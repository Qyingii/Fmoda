using TMPro;
using UnityEngine;

namespace FModa.Multilingual.Example
{
    /// <summary>
    /// 把 TMP 文本绑定到多语言 key 的示例组件。
    ///
    /// 视图只依赖模块：语言变化时重新向模块要一次文本，
    /// 不直接访问 DataManager.Multilingual。
    /// </summary>
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;

        [Tooltip("语言包里的文本键，例如 ui.start")]
        [SerializeField] private string key;

        private IMultilingualManager multilingual;

        private void Awake()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void Start()
        {
            // GF 可能在 OnEnable 之后才初始化完成，这里再尝试一次。
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        private void Bind()
        {
            if (multilingual != null || GF.Instance == null)
                return;

            multilingual = GF.GetModule<MultilingualManager>();
            if (multilingual != null)
                multilingual.OnLanguageChanged += HandleLanguageChanged;
        }

        private void Unbind()
        {
            if (multilingual != null)
                multilingual.OnLanguageChanged -= HandleLanguageChanged;

            multilingual = null;
        }

        private void HandleLanguageChanged(string oldCode, string newCode)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (target == null || multilingual == null)
                return;

            target.text = multilingual.GetText(key);
        }
    }
}
