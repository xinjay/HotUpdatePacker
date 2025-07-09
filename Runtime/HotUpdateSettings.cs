using HybridCLR;

namespace HotUpdatePacker.Runtime
{
    [System.Serializable]
    public class MetaDataSetting
    {
        public string aotDllName;
        public HomologousImageMode mode;
    }

    [System.Serializable]
    public class HotUpdateSettings
    {
        public MetaDataSetting[] metaSettings;
        public string[] hotUpdateDllNames;
    }
}