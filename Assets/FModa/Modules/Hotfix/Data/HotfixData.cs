using System;
using System.Collections.Generic;
using UnityEngine;

namespace FModa.Hotfix
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

    /// <summary> version.json 数据结构（与 Editor 打包工具保持同步）</summary>
    [Serializable]
    public class VersionData
    {
        public string version;
        public string downloadUrl;
        public string updateContent;
        public string hotfixVersion;
        public string hotfixUpdateContent;
    }
}
