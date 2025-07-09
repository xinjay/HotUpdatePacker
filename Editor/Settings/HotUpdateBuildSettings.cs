using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HotUpdatePacker.Editor.Settings
{
    [System.Serializable]
    public class HotUpdateItem
    {
        public string assemblyName;
        public string compileDir;
        [Tooltip("Separated by\"|\"")] public string defines;
        [Tooltip("Separated by\"|\"")] public string depend_ass;
        [Tooltip("Separated by\"|\"")] public string depend_dll;
        public string[] GetDefines() => defines.Split('|');
        public string[] GetDependAssemblyNames() => depend_ass.Split('|');
        public string[] GetDependAssemblies() => depend_dll.Split('|');
    }

    [CreateAssetMenu(menuName = "CreateHotUpdateBuildSettings")]
    public class HotUpdateBuildSettings : ScriptableObject
    {
        //public bool autoCopyAOT = false;
        public string commitPrefix = "[XXXX]";
        public VersionControlType versionControl;
        public string HotUpdateSettingsFile = "Assets/Res/Assembly//HotUpdateSettings.bytes";
        public string AOTDllBackupDir = "HotUpdateData/AOTBackup";
        public string AOTMetaDllDir = "Assets/Assembly/AOTMeta";
        public string HotUpdateDllDir = "Assets/Assembly/HotUpdate";
        public HotUpdateItem[] HotUpdateItems;

        private const string buildSettingFile = "ProjectSettings/HotUpdateBuildSettings.asset";
        private static HotUpdateBuildSettings s_Instance;

        public static HotUpdateBuildSettings Instance
        {
            get
            {
                if (!s_Instance)
                {
                    LoadOrCreate();
                }

                return s_Instance;
            }
        }

        public static HotUpdateBuildSettings LoadOrCreate()
        {
            var filePath = buildSettingFile;
            var objs = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            s_Instance = objs.Length > 0
                ? (HotUpdateBuildSettings)objs[0]
                : (s_Instance ?? CreateInstance<HotUpdateBuildSettings>());
            return s_Instance;
        }

        public static void Save(bool dirty = false)
        {
            if (!s_Instance)
            {
                return;
            }

            if (dirty)
            {
                s_Instance.AutoGenerate();
            }

            var filePath = buildSettingFile;
            var directoryName = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directoryName);
            var obj = new Object[1] { s_Instance };
            InternalEditorUtility.SaveToSerializedFileAndForget(obj, filePath, true);
        }

        public string GetAOTDllBackupPath(BuildTarget target)
        {
            var parent = Directory.GetParent(Application.dataPath).ToString();
            return $"{parent}/{AOTDllBackupDir}/{target}";
        }

        private IList<HotUpdateItem> lastAssItems;

        private void AutoGenerate()
        {
            var assNames = HotUpdateItems.Select(item => item.assemblyName).ToList();
            AssemblyInfoPacker.PackAssemblyInfos(assNames);
            AddToHybridSettings(HotUpdateItems);
            lastAssItems = HotUpdateItems;
        }


        private void AddToHybridSettings(IList<HotUpdateItem> assemblies)
        {
            var inst = HybridCLRSettings.Instance;
            var hotass = inst.hotUpdateAssemblies.ToList();
            var tempList = new List<string>();
            var microRemoveList = new List<string>();
            var microAddList = new List<string>();
            var dirty = false;
            //先移除
            if (lastAssItems != null)
            {
                foreach (var item in lastAssItems)
                {
                    if (assemblies.Contains(item))
                        continue;
                    for (var index = hotass.Count - 1; index >= 0; index--)
                    {
                        if (hotass[index] != item.assemblyName)
                        {
                            microRemoveList.Add(item.defines);
                            continue;
                        }

                        hotass.RemoveAt(index);
                        dirty = true;
                        break;
                    }
                }
            }

            //再添加
            foreach (var ass in assemblies)
            {

                if (hotass.Contains(ass.assemblyName))
                {
                    continue;
                }
                tempList.Add(ass.assemblyName);
                dirty = true;
            }

            if (dirty)
            {
                hotass.AddRange(tempList);
                inst.hotUpdateAssemblies = hotass.ToArray();
                EditorUtility.SetDirty(inst);
                HybridCLRSettings.Save();
            }
        }
    }
}