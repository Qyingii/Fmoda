using UnityEngine;
using UnityEngine.UI;

public class RedUI : UIBase
{
    [SerializeField] private Button closeButton;
    protected override void OnBind()
    {
        closeButton.onClick.AddListener(Close);

    }

    protected override void OnUnbind()
    {
        closeButton.onClick.RemoveListener(Close);
    }
}
