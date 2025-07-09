using System;
using System.Reflection;
using System.Threading.Tasks;
using HybridCLR;
using UnityEngine;

namespace HotUpdatePacker.Runtime
{
    public static class HoteUpdateAssemblyLoader
    {
        private static bool initialized = false;
        private static HotUpdateSettings _hotUpdateSettings;
        private static IAssetsLoader resLoader;

        /// <summary>
        /// 初始化程序集加载器（热更新资源下载后调用）
        /// </summary>
        /// <param name="loader"></param>
        public static async ValueTask Init(IAssetsLoader loader)
        {
            if (initialized)
                return;
            initialized = true;
            HotUpdateAOT.Clear();
            resLoader = loader;
            await LoadHotUpdateSettings();
            await LoadAOTMetaData();
            await LoadHotUpdateAssemblies();
            ReflectCallReflector();
        }

        /// <summary>
        /// 加载程序集热更配置
        /// </summary>
        private static async ValueTask LoadHotUpdateSettings()
        {
            var waiter = new AsyncAwaiter<string>();
            resLoader.LoadText(HotUpdateAOTDefines.HotUpdateSettingsName, waiter.SetResult);
            var json = await waiter;
            _hotUpdateSettings = JsonUtility.FromJson<HotUpdateSettings>(json);
        }

        /// <summary>
        /// 加载AOT补充元数据
        /// </summary>
        private static async ValueTask LoadAOTMetaData()
        {
            foreach (var meta in _hotUpdateSettings.metaSettings)
            {
                var dll = meta.aotDllName;
                var mode = meta.mode;
                var waiter = new AsyncAwaiter<byte[]>();
                resLoader.LoadBytes(dll, waiter.SetResult);
                var bytes = await waiter;
                try
                {
                    try
                    {
                        RuntimeApi.LoadMetadataForAOTAssembly(bytes, mode);
                    }
                    catch (Exception e)
                    {
                        switch (mode)
                        {
                            case HomologousImageMode.Consistent: //Consistent 模式补充元数据失败，则尝试用SuperSet模式补充
                                RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
                                break;
                            default:
                                throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"{dll}->{e.Message}");
                }
            }
        }

        /// <summary>
        /// 加载热更程序集
        /// </summary>
        private static async ValueTask LoadHotUpdateAssemblies()
        {
            foreach (var dll in _hotUpdateSettings.hotUpdateDllNames)
            {
                var waiter = new AsyncAwaiter<byte[]>();
                resLoader.LoadBytes(dll, waiter.SetResult);
                var bytes = await waiter;
                var ass = Assembly.Load(bytes);
                HotUpdateAOT.RegisteReflectionAssembly(dll, ass);
            }
        }

        /// <summary>
        /// 反射调用热更程序集加载后的初始化操作
        /// </summary>
        private static void ReflectCallReflector()
        {
            HotUpdateAOT.ReflectCallMethod(HotUpdateAOTDefines.ReflectorFullName, HotUpdateAOTDefines.ReflectCall,
                BindingFlags.Static | BindingFlags.NonPublic, null, null);
        }
    }
}