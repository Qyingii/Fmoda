using System.Collections.Generic;

[System.Serializable]
public class ModSetting
{
    public List<string> paths  = new List<string>();
    public List<bool>   isLoad = new List<bool>();
}
