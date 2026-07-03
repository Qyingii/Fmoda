using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ECMVC.HotUpdate
{

    [Serializable]
    public class FileListResponse
    {
        public bool success;
        public string error;
        public List<FileData> files;
    }

    [Serializable]
    public class FileData
    {
        public string name;
        public double size;
        public string md5;
        public double lastModified;
    }
}
