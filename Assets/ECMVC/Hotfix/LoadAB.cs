using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

namespace ECMVC.Hotfix
{
    public class LoadAB
    {
        private string ABPath;
        private string url;
        private string platformFolder;

        public LoadAB(string url)
        {
            this.url = url;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            platformFolder = "AssetBundles_Windows";
#elif UNITY_ANDROID
        platformFolder = "AssetBundles_Android";
#elif UNITY_IPHONE
        platformFolder = "AssetBundles_IOS";
#endif
        }

        /// <summary> 加载 AB 包，返回包内所有资源（失败返回 null）</summary>
        public async UniTask<object[]> LoadABAsync(string abName, bool isAndroid = false, bool isLocal = false)
        {
            if (string.IsNullOrEmpty(platformFolder))
            {
                Debug.LogError("无法获取到当前平台文件夹");
                return null;
            }

            if (isLocal)
                ABPath = Path.Combine(Application.streamingAssetsPath, platformFolder);
            else
                ABPath = Path.Combine(Application.persistentDataPath, platformFolder);

            if (!isAndroid)
            {
                if (!Directory.Exists(ABPath))
                    Directory.CreateDirectory(ABPath);

                ABPath = Path.Combine(ABPath, abName);
                Debug.Log("正在加载：" + abName + " 路径： " + ABPath);

                AssetBundleCreateRequest createRequest = AssetBundle.LoadFromFileAsync(ABPath);
                await createRequest.ToUniTask();
                AssetBundle bundle = createRequest.assetBundle;

                if (bundle == null)
                {
                    Debug.LogError("无法加载AB包: " + abName);
                    return null;
                }

                Debug.Log("加载成功：" + abName);
                return bundle.LoadAllAssets();
            }
            else
            {
                if (!Directory.Exists(ABPath))
                    Directory.CreateDirectory(ABPath);

                ABPath = Path.Combine(ABPath, abName);
                string androidPath = "file://" + ABPath;
                Debug.Log("正在加载：" + abName + " 路径： " + androidPath);

                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(androidPath))
                {
                    await request.SendWebRequest().ToUniTask();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"AB包加载失败: {request.error}");
                        return null;
                    }

                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
                    Debug.Log("加载成功：" + abName);

                    return bundle.LoadAllAssets();
                }
            }
        }

        /// <summary> 从远程服务器下载 AB 包到本地 </summary>
        public async UniTask DownloadABAsync(string abName)
        {
            if (string.IsNullOrEmpty(platformFolder))
            {
                Debug.LogError("无法获取到当前平台文件夹");
                return;
            }

            string saveDir = Path.Combine(Application.persistentDataPath, platformFolder);

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            string savePath = Path.Combine(saveDir, abName);
            string remoteUrl = Path.Combine(url, abName).Replace("\\", "/");

            using (UnityWebRequest request = new UnityWebRequest(remoteUrl)
            {
                downloadHandler = new DownloadHandlerFile(savePath),
                certificateHandler = new CertificateHandlerIgnore(),
            })
            {
                await request.SendWebRequest().ToUniTask();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"AB包下载失败: {request.error}");
                }
                else
                {
                    Debug.Log($"AB包已保存到: {savePath}");
                    if (File.Exists(savePath))
                        Debug.Log($"文件大小: {new FileInfo(savePath).Length} bytes");
                }
            }
        }


    }
}