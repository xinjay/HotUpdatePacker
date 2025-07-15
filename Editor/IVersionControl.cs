using UnityEngine;

namespace HotUpdatePacker.Editor
{
    public interface IVersionControl
    {
        void Commit(string workingCopyPath, string msg, string appfullpath);
    }
}