using UnityEngine;
using UnityEngine.UI;

public class BlankUI : UIBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openSubButton;

    protected override void OnBind()
    {
        closeButton.onClick.AddListener(Close);
        openSubButton.onClick.AddListener(OpenRedSubUI);
    }

    protected override void OnUnbind()
    {
        closeButton.onClick.RemoveListener(Close);
        openSubButton.onClick.RemoveListener(OpenRedSubUI);
    }

    //打开子UI
    private void OpenRedSubUI()
    {
        OpenSubUI("RedPanel");
    }
}
