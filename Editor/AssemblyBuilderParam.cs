using System.Collections.Generic;
using HotUpdatePacker.Editor.Settings;
using UnityEditor;

namespace HotUpdatePacker.Editor
{
    public class AssemblyBuilderParam
    {
        public string assemblyName;
        public string destPath;
        public string compileDir;
        public string[] defines;
        public string[] otherReferences;
        public BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        public BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        public bool developmentBuild;
        public bool generateXml;
        public string Mvid;

        public static AssemblyBuilderParam GetBuildParam(HotUpdateItem cfg, BuildTarget target)
        {
            var assemblyName = cfg.assemblyName;
            var otherReference = new List<string>();
            if (cfg.depend_ass != null)
            {
                foreach (var ass in cfg.GetDependAssemblyNames())
                {
                    if (string.IsNullOrEmpty(ass))
                        continue;
                    var assPath = BuilderUtil.GetTempAssemblySavePath(ass, target);
                    otherReference.Add(assPath);
                }
            }

            var dlls = cfg.GetDependAssemblies();
            foreach (var dll in dlls)
            {
                if (string.IsNullOrEmpty(dll))
                    continue;
                otherReference.Add(dll);
            }

            var result = new AssemblyBuilderParam
            {
                assemblyName = assemblyName,
                destPath = HotUpdateBuildSettings.Instance.HotUpdateDllDir,
                compileDir = cfg.compileDir,
                Mvid = cfg.Mvid,
                defines = cfg.GetDefines(),
                otherReferences = otherReference.ToArray(),
                target = target
            };
            return result;
        }
    }
}