using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class LoadAB
{
    private string ABPath;
    private string url;
    private string platformFolder;

    public LoadAB( string url)
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

    //加载AB包
    public IEnumerator LoadABAsync(string abName, System.Action<object[]> callback,System.Action errorCallback, bool isAndroid = false,bool isLocal = false)
    {
        if(string.IsNullOrEmpty(platformFolder))
        {
            Debug.LogError("无法获取到当前平台文件夹");
            yield break;
        }

        if (isLocal)
        {
            ABPath = Path.Combine(Application.streamingAssetsPath, platformFolder);
        }
        else
        {
            ABPath = Path.Combine(Application.persistentDataPath, platformFolder);
        }

        if (!isAndroid)
        {
            // 确保目录存在
            if (!Directory.Exists(ABPath))
                Directory.CreateDirectory(ABPath);

            ABPath = Path.Combine(ABPath, abName);

            Debug.Log("正在加载：" + abName + " 路径： " + ABPath);

            AssetBundleCreateRequest createRequest = AssetBundle.LoadFromFileAsync(ABPath);
            yield return createRequest;
            AssetBundle bundle = createRequest.assetBundle;
            if (bundle == null)
            {
                Debug.Log("无法加载AB包: " + abName);
                errorCallback();
                yield break;
            }

            Debug.Log("加载成功：" + abName);
            //获取资源
            var resources = bundle.LoadAllAssets();

            //回调
            callback(resources);
        }
        else
        {
            // 确保目录存在
            if (!Directory.Exists(ABPath))
                Directory.CreateDirectory(ABPath);

            ABPath = Path.Combine(ABPath, abName);
            ABPath = "file://" + ABPath;
            Debug.Log("正在加载：" + abName + " 路径： " + ABPath);

            // Android 需要特殊处理路径（UnityWebRequest 会自动处理）
            //此方法将返回 WebRequestAsyncOperation 对象。在协程内部生成 WebRequestAsyncOperation 将导致协程暂停，
            //直到 UnityWebRequest 遇到系统错误或结束通信为止。
            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(ABPath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"AB包加载失败: {request.error}");
                    yield break;
                }
                else
                {
                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
                    // 使用 bundle 加载资源
                    Debug.Log("加载成功：" + abName);

                    var resources = bundle.LoadAllAssets();

                    //回调
                    callback(resources);
                }
            }
        }
    }

    //下载AB包
    public IEnumerator DownloadABAsync(string abName)
    {
        if (string.IsNullOrEmpty(platformFolder))
        {
            Debug.LogError("无法获取到当前平台文件夹");
            yield break;
        }

        // 根据平台选择存储目录（使用 persistentDataPath 而非 streamingAssetsPath）
        string saveDir = Path.Combine(Application.persistentDataPath, platformFolder);

        // 确保目录存在
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }

        // 本地保存路径
        string savePath = Path.Combine(saveDir, abName);

        // 远程URL（确保 url 是有效的服务器地址，如 "http://example.com/ABs/"）
        string remoteUrl = Path.Combine(url, abName).Replace("\\", "/"); // 替换为 URL 兼容的分隔符

        // 创建下载请求
        UnityWebRequest request = new UnityWebRequest(remoteUrl)
        {
            downloadHandler = new DownloadHandlerFile(savePath),
            certificateHandler = new CertificateHandlerIgnore(), // 忽略证书验证
        };

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"AB包下载失败: {request.error}");
        }
        else
        {
            Debug.Log($"AB包已保存到: {savePath}");
            if (File.Exists(savePath))
            {
                Debug.Log($"文件大小: {new FileInfo(savePath).Length} bytes");
            }
        }
    }
}
