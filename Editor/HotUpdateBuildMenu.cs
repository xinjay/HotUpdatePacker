using UnityEditor;

namespace HotUpdatePacker.Editor
{
    public static class HotUpdateBuildMenu
    {
        [MenuItem("Build/HotUpdate/Settings...", priority = 1)]
        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/HybridCLR Settings/HotUpdateBuild Settings");
        }

        [MenuItem("Build/HotUpdate/BuildCompile", priority = 21)]
        private static void BuildComplile()
        {
            HotUpdateBuildPipline.HotUpdateCompile(EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("Build/HotUpdate/BuildComplileFull", priority = 22)]
        private static void BuildComplileFull()
        {
            HotUpdateBuildPipline.HotUpdateCompile(EditorUserBuildSettings.activeBuildTarget,
                HotUpdatePackFlag.Full);
        }


        [MenuItem("Build/HotUpdate/BuildCompile(Dev)", priority = 33)]
        private static void BuildComplile_dev()
        {
            HotUpdateBuildPipline.HotUpdateCompile(EditorUserBuildSettings.activeBuildTarget,
                HotUpdatePackFlag.Dev);
        }


        [MenuItem("Build/HotUpdate/BuildComplileFull(Dev)", priority = 34)]
        private static void BuildComplileFull_dev()
        {
            HotUpdateBuildPipline.HotUpdateCompile(EditorUserBuildSettings.activeBuildTarget,
                HotUpdatePackFlag.Dev_Full);
        }


        [MenuItem("Build/HotUpdate/AOTMetaMissingCheck", priority = 64)]
        private static void AOTMetaMissingCheck()
        {
            _ = HotUpdateBuildPipline.AOTMetaMissingCheck(EditorUserBuildSettings.activeBuildTarget, false);
        }


        [MenuItem("Build/HotUpdate/BackupAOTAssemblies", priority = 65)]
        private static void BackupAOTAssemblies()
        {
            HotUpdateBuildPipline.BackupAOTAssemblies(EditorUserBuildSettings.activeBuildTarget, true);
        }
    }
}