using System;
using System.IO.Compression;

namespace HotUpdatePacker.Editor
{
    public class VersionControl_Local : IVersionControl
    {
        public void Commit(string workingCopyPath, string commitMessage, string appfullpath)
        {
            try
            {
                var localFile = $"{appfullpath}_aot.zip";
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