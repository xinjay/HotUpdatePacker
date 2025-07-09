using System;

namespace HotUpdatePacker.Editor
{
    public class VersionControl_Git : IVersionControl
    {
        private const string gitPath = "git";

        public void Commit(string workingCopyPath, string msg, params object[] param)
        {
            var remoteName = "origin"; // 远程仓库名称，默认为origin
            var branchName = "main"; // 替换为你的目标分支名称
            try
            {
                // 执行 git status
                var statusOutput = BuilderUtil.RunCommand(gitPath, $"status --porcelain", workingCopyPath);
                UnityEngine.Debug.Log("Git Status:\n" + statusOutput);
                // 检查是否有变更需要提交
                if (string.IsNullOrWhiteSpace(statusOutput))
                {
                    UnityEngine.Debug.Log("No changes to commit or push.");
                    return;
                }

                // 添加所有变更（包括新增、修改、删除）
                BuilderUtil.RunCommand(gitPath, $"add . -A", workingCopyPath);
                UnityEngine.Debug.Log("All changes staged.");

                // 执行提交
                var commitResult = BuilderUtil.RunCommand(gitPath, $"commit -m \"{msg}\"", workingCopyPath);
                UnityEngine.Debug.Log("Commit Result:\n" + commitResult);

                // 推送到远程仓库
                var pushResult = BuilderUtil.RunCommand(gitPath, $"push {remoteName} {branchName}", workingCopyPath);
                UnityEngine.Debug.Log("Push Result:\n" + pushResult);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"Error: {ex.Message}");
                throw;
            }
        }
    }
}