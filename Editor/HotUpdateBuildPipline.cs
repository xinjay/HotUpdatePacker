using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HotUpdatePacker.Editor.Settings;
using HotUpdatePacker.Runtime;
using UnityEngine;
using HybridCLR;
using HybridCLR.Editor;
using HybridCLR.Editor.AOT;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.HotUpdate;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Meta;
using Obfuz;
using Obfuz.Settings;
using Obfuz4HybridCLR;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HotUpdatePacker.Editor
{
    [Flags]
    public enum HotUpdatePackFlag
    {
        None = 0,
        Dev = 1,
        Full = 1 << 1,
        FastBuild = 1 << 2,
        Dev_Full = Dev | Full
    }


    public static class HotUpdateBuildPipline
    {
        /// <summary>
        /// 编译热更程序集
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetGroup"></param>
        /// <param name="developmentBuild"></param>
        /// <param name="full"></param>
        /// <param name="autoBackup"></param>
        /// <param name="throwIfMetaMissing"></param>
        public static void HotUpdateCompile(BuildTarget target, HotUpdatePackFlag flag = HotUpdatePackFlag.None)
        {
            var full = flag.HasTarget(HotUpdatePackFlag.Full);
            var developmentBuild = flag.HasTarget(HotUpdatePackFlag.Dev);
            var fastBuild = flag.HasTarget(HotUpdatePackFlag.FastBuild);
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            Debug.LogWarning($"----------------HotUpdateCompile:FullCompile:{full}----------------");
            EnvironmentCheck();
            EditorUtility.DisplayProgressBar("Compiling", "HybridHotUpdateAssemblies", 0.1f);

            TimeWatch.TimeStart();
            DoHybridHotUpdateCompile(target, developmentBuild);
            TimeWatch.TimeStamp("HybridHotUpdateAssemblies");

            EditorUtility.DisplayProgressBar("Compiling", "CustomHotUpdateAssemblies", 0.2f);

            DoCustomCompile(target, targetGroup, developmentBuild);
            TimeWatch.TimeStamp("CustomHotUpdateAssemblies");

            EditorUtility.DisplayProgressBar("Compiling", "ObfuseAssemblies", 0.3f);
            ObfuseAssemblies(target);
            TimeWatch.TimeStamp("ObfuseAssemblies");
            //先执行上次build的aot元数据裁剪校验
            EditorUtility.DisplayProgressBar("Compiling", "AOTMetaMissingCheck", 0.4f);
            var success = AOTMetaMissingCheck(target, !full) && fastBuild;
            TimeWatch.TimeStamp("AOTMetaMissingCheck");

            //完全编译并且和上次aot元数据裁剪校验没通过时，才执行完整编译
            if (!success && full)
            {
                EditorUtility.DisplayProgressBar("Compiling", "MetaGenerate", 0.5f);
                DoHybridMetaGenerate(target);
                TimeWatch.TimeStamp("DoHybridMetaGenerate");
                EditorUtility.DisplayProgressBar("Compiling", "BackupAOTAssemblies", 0.6f);
                BackupAOTAssemblies(target);
                TimeWatch.TimeStamp("BackupAOTAssemblies");
            }

            StripAOTAssemblyMetadata(target);
            TimeWatch.TimeStamp("StripAOTAssemblyMetadata");
            EditorUtility.DisplayProgressBar("Compiling", "StripAOTAssemblyMetadata", 0.7f);

            EditorUtility.DisplayProgressBar("Compiling", "CopyHotUpdateAssemblies", 0.8f);
            CopyHotUpdateAssemblies(target);
            TimeWatch.TimeStamp("CopyHotUpdateAssemblies");
            EditorUtility.DisplayProgressBar("Compiling", "RefreshHotUpdateSettings", 0.9f);
            RefreshHotUpdateSettings();
            TimeWatch.TimeStamp("RefreshHotUpdateSettings");
            EditorUtility.DisplayProgressBar("Compiling", "HotUpdateCompile Complete", 1);
            Debug.LogWarning("----------------HotUpdateCompile Complete----------------");
            EditorUtility.ClearProgressBar();
            TimeWatch.TimeEnd("HotUpdateCompile");
        }

        private static void EnvironmentCheck()
        {
            var installer = new InstallerController();
            //没有安装时或者版本不兼容时，自动安装
            if (!installer.HasInstalledHybridCLR() ||
                installer.PackageVersion != installer.InstalledLibil2cppVersion)
            {
                var dir = "HybridCLRData/il2cpp_plus_repo/libil2cpp";
                installer.InstallFromLocal(dir);
                Debug.LogWarning("Auto install HybridCLR");
            }
        }

        /// <summary>
        /// Hybrid元数据、桥接函数等生成流程（完整打包时）
        /// </summary>
        public static void DoHybridMetaGenerate(BuildTarget target)
        {
            Debug.LogWarning("----------------DoHybridAOTMetaGenerate----------------");
            Il2CppDefGeneratorCommand.GenerateIl2CppDef();
            ObfuscateUtil.GeneratePolymorphicCodes($"{SettingsUtil.LocalIl2CppDir}/libil2cpp");
            // 这几个生成依赖HotUpdateDlls
            LinkGeneratorCommand.GenerateLinkXml(target);
            GenerateObfuzLinkXml(target);
            // 生成裁剪后的aot dll
            StripAOTDllCommand.GenerateStripedAOTDlls(target);
            // 桥接函数生成依赖于AOT dll，必须保证已经build过，生成AOT dll
            // MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper(target);
            AOTReferenceGeneratorCommand.GenerateAOTGenericReference(target);
            var obfuscatedHotUpdateDllPath = PrebuildCommandExt.GetObfuscatedHotUpdateAssemblyOutputPath(target);
            PrebuildCommandExt.GenerateMethodBridgeAndReversePInvokeWrapper(target, obfuscatedHotUpdateDllPath);
            Debug.LogWarning("----------------DoHybridAOTMetaGenerate Complete----------------");
        }

        /// <summary>
        /// 生成Obfuz后的linkxml
        /// </summary>
        /// <param name="target"></param>
        public static void GenerateObfuzLinkXml(BuildTarget target)
        {
            var obfuzSettings = ObfuzSettings.Instance;
            var assemblySearchDirs = new List<string> { SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target) };
            var builder = ObfuscatorBuilder.FromObfuzSettings(obfuzSettings, target, true);
            builder.InsertTopPriorityAssemblySearchPaths(assemblySearchDirs);
            var obfuz = builder.Build();
            obfuz.Run();
            var hotfixAssemblies = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;
            var analyzer =
                new HybridCLR.Editor.Link.Analyzer(
                    new PathAssemblyResolver(obfuzSettings.GetObfuscatedAssemblyOutputPath(target)));
            var refTypes = analyzer.CollectRefs(hotfixAssemblies);
            // HyridCLR中 LinkXmlWritter不是public的，在其他程序集无法访问，只能通过反射操作
            var linkXmlWriter = typeof(SettingsUtil).Assembly.GetType("HybridCLR.Editor.Link.LinkXmlWriter");
            var writeMethod = linkXmlWriter.GetMethod("Write", BindingFlags.Public | BindingFlags.Instance);
            var instance = Activator.CreateInstance(linkXmlWriter);
            var linkXmlOutputPath = HotUpdateBuildSettings.Instance.ObfuzLinkPath;
            writeMethod.Invoke(instance, new object[] { linkXmlOutputPath, refTypes });
            Debug.Log($"[GenerateLinkXmlForObfuscatedAssembly] output:{linkXmlOutputPath}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Hybrid热更编译流程
        /// </summary>
        public static void DoHybridHotUpdateCompile(BuildTarget target, bool developmentBuild)
        {
            Debug.LogWarning("----------------DoHybridHotUpdateCompile----------------");
            CompileDllCommand.CompileDll(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), target,
                developmentBuild);
            Debug.LogWarning("----------------DoHybridHotUpdateCompile Complete----------------");
        }

        /// <summary>
        /// 自定义程序集编译流程
        /// </summary>
        public static void DoCustomCompile(BuildTarget target, BuildTargetGroup targetGroup,
            bool developmentBuild)
        {
            Debug.LogWarning("----------------DoCustomCompile----------------");
            AssemblyBuilderManager.ClearBuilders();
            var hotUpdateItems = HotUpdateBuildSettings.Instance.HotUpdateItems;
            foreach (var ass in hotUpdateItems)
            {
                var param = AssemblyBuilderParam.GetBuildParam(ass, target);
                param.target = target;
                param.targetGroup = targetGroup;
                param.developmentBuild = developmentBuild;
                Debug.Log($"Build->{ass.assemblyName}");
                //编译中
                while (EditorApplication.isCompiling)
                {
                    Thread.Sleep(1000);
                }

                AssemblyBuilderManager.Build(param);
            }

            //编译中
            while (EditorApplication.isCompiling)
            {
                Thread.Sleep(1000);
            }

            Debug.LogWarning("----------------DoCustomCompile Complete----------------");
        }

        public static void ObfuseAssemblies(BuildTarget target)
        {
            Debug.LogWarning("----------------ObfuseAssemblies----------------");
            var obfuscatedHotUpdateDllPath = PrebuildCommandExt.GetObfuscatedHotUpdateAssemblyOutputPath(target);
            ObfuscateUtil.ObfuscateHotUpdateAssemblies(target, obfuscatedHotUpdateDllPath);
            Debug.LogWarning("----------------ObfuseAssemblies Complete----------------");
        }

        /// <summary>
        /// 裁剪AOT补充元数据程序集
        /// </summary>
        public static void StripAOTAssemblyMetadata(BuildTarget target)
        {
            Debug.LogWarning("----------------StripAOTAssemblyMetadata----------------");
            var srcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            //var srcDir = PrebuildCommandExt.GetObfuscatedHotUpdateAssemblyOutputPath(target);
            var aotmetas = GenerateAOTGenericReference(target);
            var dstDir = HotUpdateBuildSettings.Instance.AOTMetaDllDir;
            BuilderUtil.CreateDir(dstDir, true);
            foreach (var src in Directory.GetFiles(srcDir, "*.dll"))
            {
                var dllName = Path.GetFileNameWithoutExtension(src);
                if (!aotmetas.Contains(dllName))
                    continue;
                var temp = $"Library/{dllName}.dll";
                var dstFile = $"{dstDir}/{dllName}.bytes";
                if (File.Exists(temp))
                    File.Delete(temp);
                AOTAssemblyMetadataStripper.Strip(src, temp);
                //生成AOT多态dll
                ObfuscateUtil.GeneratePolymorphicDll(temp, dstFile);
            }

            Debug.LogWarning("----------------StripAOTAssemblyMetadata Complete----------------");
        }

        /// <summary>
        /// AOT元数据缺失检测
        /// </summary>
        /// <param name="withBackup">是否和备份AOT数据检测</param>
        /// <param name="target"></param>
        /// <param name="throwIfMetaMissing"></param>
        /// <returns></returns>
        public static bool AOTMetaMissingCheck(BuildTarget target, bool throwIfMetaMissing = false)
        {
            var result = true;
            Debug.LogWarning("----------------AOTMetaMissingCheck----------------");
            var aotDir = HotUpdateBuildSettings.Instance.GetAOTDllBackupPath(target);
            var msg = "";
            try
            {
                if (Directory.Exists(aotDir))
                {
                    var checker =
                        new MissingMetadataChecker(aotDir, SettingsUtil.HotUpdateAssemblyNamesIncludePreserved);
                    //var hotUpdateDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
                    var hotUpdateDir = PrebuildCommandExt.GetObfuscatedHotUpdateAssemblyOutputPath(target);
                    var noMetaMissing = true;
                    foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
                    {
                        var dllPath = $"{hotUpdateDir}/{dll}";
                        var notAnyMissing = checker.Check(dllPath);
                        noMetaMissing &= notAnyMissing;
                    }

                    result = noMetaMissing;
                    if (!noMetaMissing)
                    {
                        msg = "！！！！AOT MetaData Missing！！！！";
                    }
                }
                else
                {
                    msg = $"Can't find AOTDir:{aotDir}";
                    result = false;
                }
            }
            catch (Exception e)
            {
                result = false;
                msg = e.ToString();
            }


            if (!result)
            {
                if (throwIfMetaMissing)
                    throw new Exception(msg);
                Debug.LogError(msg);
            }

            Debug.LogWarning("----------------AOTMetaMissingCheck Complete----------------");
            return result;
        }

        /// <summary>
        /// HotUpdate程序集拷贝
        /// </summary>
        public static void CopyHotUpdateAssemblies(BuildTarget target)
        {
            Debug.LogWarning("----------------CopyHotUpdateAssemblies----------------");
            var hotUpdateDir = HotUpdateBuildSettings.Instance.HotUpdateDllDir;
            BuilderUtil.CreateDir(hotUpdateDir, true);
            //var hotUpdateDllOutput = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var hotUpdateDllOutput = PrebuildCommandExt.GetObfuscatedHotUpdateAssemblyOutputPath(target);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                var dllName = Path.GetFileNameWithoutExtension(dll);
                var dllPath = $"{hotUpdateDllOutput}/{dll}";
                var destPath = $"{hotUpdateDir}/{dllName}.bytes";
                //生成Hotupdate多态dll
                ObfuscateUtil.GeneratePolymorphicDll(dllPath, destPath);
            }

            Debug.LogWarning("----------------CopyHotUpdateAssemblies Complete----------------");
        }

        /// <summary>
        /// AOT程序集拷贝到备份路径用于元数据缺失检查
        /// </summary>
        public static void BackupAOTAssemblies(BuildTarget target, bool showDialog = false)
        {
            if (showDialog && !EditorUtility.DisplayDialog("Warning",
                    "This operation will overwrite the backed-up AOT assemblies. Please remember to incorporate them into version management in a timely manner.",
                    "Confirm", "Cancel"))
                return;
            Debug.LogWarning("----------------BackupAOTAssemblies----------------");
            var srcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            var dstDir = HotUpdateBuildSettings.Instance.GetAOTDllBackupPath(target);
            BuilderUtil.CreateDir(dstDir, true);
            foreach (var src in Directory.GetFiles(srcDir, "*.dll"))
            {
                var dllName = Path.GetFileName(src);
                var dstFile = $"{dstDir}/{dllName}";
                File.Copy(src, dstFile, true);
            }

            Debug.LogWarning("----------------BackupAOTAssemblies Complete----------------");
        }

        public static void Commit(BuildTarget target, string appfullpath, bool showDialog = false)
        {
            if (showDialog && !EditorUtility.DisplayDialog("Warning",
                    "Will you commit AOT Assemblies' modifications to version control system?",
                    "Confirm", "Cancel"))
                return;
            Debug.LogWarning("----------------Commit to version control system----------------");
            var dstDir = HotUpdateBuildSettings.Instance.GetAOTDllBackupPath(target);
            var prefix = HotUpdateBuildSettings.Instance.commitPrefix;
            VersionControlSystem.Commit(prefix, dstDir, appfullpath);
            Debug.LogWarning("----------------Commit Complete----------------");
        }

        /// <summary>
        /// 热更程序集配置更新
        /// </summary>
        public static void RefreshHotUpdateSettings()
        {
            Debug.LogWarning("----------------RefreshHotUpdateSettings----------------");
            var settingFile = HotUpdateBuildSettings.Instance.HotUpdateSettingsFile;
            var _settings = new HotUpdateSettings();
            _settings.metaSettings = Directory.GetFiles(HotUpdateBuildSettings.Instance.AOTMetaDllDir, "*.bytes")
                .Select(item =>
                    new MetaDataSetting
                        { aotDllName = Path.GetFileNameWithoutExtension(item), mode = HomologousImageMode.Consistent })
                .ToArray();
            _settings.hotUpdateDllNames = SettingsUtil.HotUpdateAssemblyFilesExcludePreserved
                .Select(Path.GetFileNameWithoutExtension).ToArray();
            var json = JsonUtility.ToJson(_settings);
            File.WriteAllText(settingFile, json);
            AssetDatabase.ImportAsset(settingFile);
            Debug.LogWarning("----------------RefreshHotUpdateSettings Complete----------------");
        }

        private static List<string> GenerateAOTGenericReference(BuildTarget target)
        {
            var gs = SettingsUtil.HybridCLRSettings;
            var hotUpdateDllNames = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;
            var collector = new AssemblyReferenceDeepCollector(
                MetaUtil.CreateHotUpdateAndAOTAssemblyResolver(target, hotUpdateDllNames), hotUpdateDllNames);
            var analyzer = new Analyzer(new Analyzer.Options
            {
                MaxIterationCount = Math.Min(20, gs.maxGenericReferenceIteration),
                Collector = collector,
            });
            analyzer.Run();
            var types = analyzer.AotGenericTypes.ToList();
            var methods = analyzer.AotGenericMethods.ToList();
            var modules = new HashSet<dnlib.DotNet.ModuleDef>(
                types.Select(t => t.Type.Module).Concat(methods.Select(m => m.Method.Module))).ToList();
            modules.Sort((a, b) => a.Name.CompareTo(b.Name));
            var result = new List<string>();
            foreach (var module in modules)
            {
                var dll = (string)module.Name;
                var nameIndex = dll.IndexOf('.');
                var name = dll.Substring(0, nameIndex);
                result.Add(name);
            }

            return result;
        }
    }
}