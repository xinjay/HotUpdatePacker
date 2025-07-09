using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using UnityEditor;

namespace HotUpdatePacker.Editor
{
    [Flags]
    public enum HotUpdatePackFlag
    {
        None = 0,
        Dev = 1,
        Full = 1 << 1,
        AutoBackup = 1 << 2,
        ThrowIfMetaMissing = 1 << 3,
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
        public static async Task
            HotUpdateCompile(BuildTarget target, HotUpdatePackFlag flag = HotUpdatePackFlag.None)
        {
            var full = flag.HasTarget(HotUpdatePackFlag.Full);
            var developmentBuild = flag.HasTarget(HotUpdatePackFlag.Dev);
            var throwIfMetaMissing = flag.HasTarget(HotUpdatePackFlag.ThrowIfMetaMissing);
            var autoBackup = flag.HasTarget(HotUpdatePackFlag.AutoBackup);
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);

            Debug.LogWarning($"----------------HotUpdateCompile:FullCompile:{full}----------------");
            EnvironmentCheck();
            EditorUtility.DisplayProgressBar("Compiling", "HybridHotUpdateAssemblies", 0.1f);
            DoHybridHotUpdateCompile(target, developmentBuild);
            EditorUtility.DisplayProgressBar("Compiling", "CustomHotUpdateAssemblies", 0.2f);
            DoCustomCompile(target, targetGroup, developmentBuild);
            if (full)
            {
                EditorUtility.DisplayProgressBar("Compiling", "MetaGenerate", 0.3f);
                DoHybridMetaGenerate(target);
                EditorUtility.DisplayProgressBar("Compiling", "StripAOT", 0.4f);
                StripAOTAssemblyMetadata(target);
                if (autoBackup)
                {
                    EditorUtility.DisplayProgressBar("Compiling", "BackupAOTAssemblies", 0.5f);
                    BackupAOTAssemblies(target);
                }
            }

            EditorUtility.DisplayProgressBar("Compiling", "AOTMetaMissingCheck(Last Build)", 0.6f);
            AOTMetaMissingCheck(false, target);
            EditorUtility.DisplayProgressBar("Compiling", "AOTMetaMissingCheck(Last Backup)", 0.7f);
            AOTMetaMissingCheck(true, target, throwIfMetaMissing);
            EditorUtility.DisplayProgressBar("Compiling", "CopyHotUpdateAssemblies", 0.8f);
            CopyHotUpdateAssemblies(target);
            EditorUtility.DisplayProgressBar("Compiling", "RefreshHotUpdateSettings", 0.9f);
            RefreshHotUpdateSettings();
            EditorUtility.DisplayProgressBar("Compiling", "HotUpdateCompile Complete", 1);
            Debug.LogWarning("----------------HotUpdateCompile Complete----------------");
            EditorUtility.ClearProgressBar();
        }

        private static void EnvironmentCheck()
        {
            var installer = new InstallerController();
            if (!installer.HasInstalledHybridCLR())
            {
                //没有安装时，自动安装
                installer.InstallDefaultHybridCLR();
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
            // 这几个生成依赖HotUpdateDlls
            LinkGeneratorCommand.GenerateLinkXml(target);
            // 生成裁剪后的aot dll
            StripAOTDllCommand.GenerateStripedAOTDlls(target);
            // 桥接函数生成依赖于AOT dll，必须保证已经build过，生成AOT dll
            MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper(target);
            AOTReferenceGeneratorCommand.GenerateAOTGenericReference(target);
            Debug.LogWarning("----------------DoHybridAOTMetaGenerate Complete----------------");
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

        /// <summary>
        /// 裁剪AOT补充元数据程序集
        /// </summary>
        public static void StripAOTAssemblyMetadata(BuildTarget target)
        {
            Debug.LogWarning("----------------StripAOTAssemblyMetadata----------------");
            var srcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            var aotmetas = GenerateAOTGenericReference(target);
            var dstDir = HotUpdateBuildSettings.Instance.AOTMetaDllDir;
            BuilderUtil.CreateDir(dstDir, true);
            foreach (var src in Directory.GetFiles(srcDir, "*.dll"))
            {
                var dllName = Path.GetFileNameWithoutExtension(src);
                if (!aotmetas.Contains(dllName))
                    continue;
                var dstFile = $"{dstDir}/{dllName}.bytes";
                AOTAssemblyMetadataStripper.Strip(src, dstFile);
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
        public static bool AOTMetaMissingCheck(bool withBackup, BuildTarget target, bool throwIfMetaMissing = false)
        {
            var result = true;
            Debug.LogWarning("----------------AOTMetaMissingCheck----------------");
            var aotDir = withBackup
                ? HotUpdateBuildSettings.Instance.GetAOTDllBackupPath(target)
                : SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            if (!Directory.Exists(aotDir))
            {
                Debug.LogWarning($"Can't find AOTDir:{aotDir}");
                return !withBackup;
            }

            var checker = new MissingMetadataChecker(aotDir, SettingsUtil.HotUpdateAssemblyNamesIncludePreserved);
            var hotUpdateDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var noMetaMissing = true;
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                var dllPath = $"{hotUpdateDir}/{dll}";
                var notAnyMissing = checker.Check(dllPath);
                noMetaMissing &= notAnyMissing;
            }

            if (!noMetaMissing)
            {
                var mgs = withBackup
                    ? "！！！！[Check With BackUp]AOT元数据缺失，需要重新完整编译，或排查热更代码中所使用了的已裁剪掉的AOT模块,切记此时不可推送C#热更！！！！"
                    : "！！！！[Check With Last Build] AOT元数据缺失，需要重新完整编译，或排查热更代码中所使用了的已裁剪掉的AOT模块！！！！";
                if (throwIfMetaMissing)
                    throw new Exception(mgs);
                Debug.LogError(mgs);
                result = false;
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
            var hotUpdateDllOutput = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                var dllName = Path.GetFileNameWithoutExtension(dll);
                var dllPath = $"{hotUpdateDllOutput}/{dll}";
                var destPath = $"{hotUpdateDir}/{dllName}.bytes";
                File.Copy(dllPath, destPath, true);
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
        
        public static void Commit(BuildTarget target, string otherMsg, bool showDialog = false)
        {
            if (showDialog && !EditorUtility.DisplayDialog("Warning",
                    "Will you commit AOT Assemblies' modifications to version control system?",
                    "Confirm", "Cancel"))
                return;
            Debug.LogWarning("----------------Commit to version control system----------------");
            var dstDir = HotUpdateBuildSettings.Instance.GetAOTDllBackupPath(target);
            var prefix = HotUpdateBuildSettings.Instance.commitPrefix;
            VersionControlSystem.Commit(prefix, dstDir, otherMsg);
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