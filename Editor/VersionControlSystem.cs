using System.Collections;
using System.Collections.Generic;
using HotUpdatePacker.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace HotUpdatePacker.Editor
{
    public enum VersionControlType
    {
        Local,
        Svn,
        Git
    }

    public static class VersionControlSystem
    {
        private static Dictionary<VersionControlType, IVersionControl> versionControlMap = new()
        {
            { VersionControlType.Local, new VersionControl_Local() },
            { VersionControlType.Svn, new VersionControl_SVN() },
            { VersionControlType.Git, new VersionControl_Git() }
        };

        /// <summary>
        /// 提交变更
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="workingCopyPath"></param>
        /// <param name="appfullpath"></param>
        public static void Commit(string prefix, string workingCopyPath, string appfullpath = "")
        {
            var commitMessage = $"{prefix} aot dll auto backup[{appfullpath}]";
            var type = HotUpdateBuildSettings.Instance.versionControl;
            versionControlMap[type].Commit(workingCopyPath, commitMessage, appfullpath);
        }
    }
}