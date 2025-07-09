using System;
using System.IO.Compression;

namespace HotUpdatePacker.Editor
{
    public class VersionControl_Local : IVersionControl
    {
        public void Commit(string workingCopyPath, string commitMessage, params object[] param)
        {
            try
            {
                var localFile = $"{param[0]}_aot.zip";
                ZipFile.CreateFromDirectory(workingCopyPath, localFile);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error: {ex.Message}");
                throw;
            }
        }
    }
}