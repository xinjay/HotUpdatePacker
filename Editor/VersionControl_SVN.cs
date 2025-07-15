using System;

namespace HotUpdatePacker.Editor
{
    public class VersionControl_SVN : IVersionControl
    {
        private const string svnPath = "svn";

        public void Commit(string workingCopyPath, string commitMessage, string appfullpath)
        {
            try
            {
                // 执行 svn status
                var statusOutput = BuilderUtil.RunCommand(svnPath, $"status", workingCopyPath);
                UnityEngine.Debug.Log("SVN Status:\n" + statusOutput);

                // 处理新增和删除
                var statusLines = statusOutput.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in statusLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var status = line[0];
                    var path = line.Substring(8).Trim(); // 提取文件路径

                    if (status == '?') // 新增文件
                    {
                        BuilderUtil.RunCommand(svnPath, $"add \"{path}\"", workingCopyPath);
                        UnityEngine.Debug.Log($"Added: {path}");
                    }
                    else if (status == '!') // 删除文件
                    {
                        BuilderUtil.RunCommand(svnPath, $"delete \"{path}\"", workingCopyPath);
                        UnityEngine.Debug.Log($"Deleted: {path}");
                    }
                }

                // 执行提交
                var commitResult =
                    BuilderUtil.RunCommand(svnPath, $"commit -m \"{commitMessage}\"", workingCopyPath);
                UnityEngine.Debug.Log("Commit Result:\n" + commitResult);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error: {ex.Message}");
                throw;
            }
        }
    }
}