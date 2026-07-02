using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ECMVC.Net
{
    [SerializeField]
    public class TcpData
    {
        public string dataType = "Data";//数据类型,有Data,Event
        public string dataName = "";//数据名称
        public string dataContent = "";//数据内容
    }
}
