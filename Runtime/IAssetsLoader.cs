using System;

namespace HotUpdatePacker.Runtime
{
    public interface IAssetsLoader
    {
        void LoadAsset<T>(string fileName, Action<T> callback, bool unload) where T : class;
        void LoadBytes(string fileName, Action<byte[]> callback);
        void LoadText(string fileName, Action<string> callback);
    }
}