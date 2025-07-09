using System.IO;
using UnityEditor;
namespace HotUpdatePacker.Editor
{
    public class ReflectorGenerator
    {
        private const string content = @"#if REFLECTOR || UNITY_EDITOR
using System;
using HotUpdatePacker.Runtime;

namespace Reflector
{
    public static class AssemblyReflector
    {
        private static void OnHotUpdateAssembliesLoaded()
        {
            InitHotUpdateAssemblies();
            RegistCustomAssemblyTypes();
        }

        /// <summary>
        /// 热更程序集加载后的一些初始化操作
        /// </summary>
        private static void InitHotUpdateAssemblies()
        {
        }

        /// <summary>
        /// 向AOT中注册反射类型
        /// </summary>
        private static void RegistCustomAssemblyTypes()
        {
        }

        #region Utils

        /// <summary>
        /// 直接注册常用反射类型，避免AOT中再次反射
        /// </summary>
        /// <param name=""type""></param>
        private static void RegistType(Type type)
        {
            var fullName = type.FullName;
            HotUpdateAOT.RegisteRefelectionType(fullName, type);
        }

        #endregion
    }
}
#endif";

        [InitializeOnLoadMethod]
        public static void GenerateReflector()
        {
            var guids = AssetDatabase.FindAssets("AssemblyReflector t:Script");
            var assemblyInfoPath = "";
            if (guids.Length == 0)
            {
                var path = "Assets/HotUpdatePackerGenerate/Reflector";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                assemblyInfoPath = $"{path}/AssemblyReflector.cs";
                File.WriteAllText(assemblyInfoPath, content);
                AssetDatabase.ImportAsset(assemblyInfoPath);
            }
        }
    }
}