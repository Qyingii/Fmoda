using System;

// Model：拥有数据，通过 event 主动通知外部变化
// 不引用任何 View / Controller / UnityEngine.UI
public class CounterModel : ModelBase
{
    public event Action<int> OnCountChanged;

    private int _count;
    public int Count
    {
        get => _count;
        private set
        {
            _count = value;
            OnCountChanged?.Invoke(_count);
        }
    }

    public void Increment() => Count++;
    public void Decrement() => Count--;
    public void Reset()     => Count = 0;
}
