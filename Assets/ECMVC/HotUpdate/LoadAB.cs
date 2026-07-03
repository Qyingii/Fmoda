using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using XLua;
using Cysharp.Threading.Tasks;

namespace ECMVC.HotUpdate
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


        // ================================================================
        //  Lua 加载（静态工具）
        // ================================================================

        /// <summary> 初始化 Lua 环境：注册自定义加载器，加载 persistent/lua/ 下所有脚本 </summary>
        /// <param name="luaEnv">XLua 虚拟机实例</param>
        /// <param name="persistentPath">Application.persistentDataPath</param>
        public static void SetupLuaEnvironment(LuaEnv luaEnv, string persistentPath)
        {
            // ── 自定义加载器：将 require('foo.bar') 映射到 persistent/lua/foo/bar.lua ──
            luaEnv.AddLoader((ref string filepath) =>
            {
                string fileName = filepath.Replace('.', '/') + ".lua";
                string localPath = Path.Combine(persistentPath, "lua", fileName);
                return File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;
            });

            // ── 扫描并加载所有 .lua 文件 ──
            string luaDir = Path.Combine(persistentPath, "lua");
            if (!Directory.Exists(luaDir))
            {
                Debug.LogWarning($"Lua 目录不存在，跳过加载: {luaDir}");
                return;
            }

            var files = Directory.GetFiles(luaDir, "*.lua", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Debug.Log("Lua 目录为空，无文件加载");
                return;
            }

            int success = 0;
            int failed = 0;
            foreach (string fullPath in files)
            {
                // 将绝对路径转为模块名，如 "lua/sub/foo.lua" → "sub.foo"
                string relative = fullPath.Substring(luaDir.Length + 1)
                                          .Replace('\\', '.')
                                          .Replace('/', '.')
                                          .Replace(".lua", "");
                try
                {
                    luaEnv.DoString($"require('{relative}')");
                    success++;
                    Debug.Log($"[Lua] 加载: {relative}");
                }
                catch (System.Exception e)
                {
                    failed++;
                    Debug.LogError($"[Lua] 加载失败 {relative}: {e.Message}");
                }
            }

            Debug.Log($"[Lua] 环境初始化完成：成功 {success} 个，失败 {failed} 个");
        }
    }
}