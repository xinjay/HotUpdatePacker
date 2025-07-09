using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotUpdatePacker.Editor.Settings
{
    public class HotUpdateBuildSettingsEditor : SettingsProvider
    {
        private SerializedObject _serializedObject;
        private SerializedProperty _commitPrefix;
        private SerializedProperty _versionControl;
        private SerializedProperty _hotUpdateSettingsFile;
        private SerializedProperty _AOTDllBackupDir;
        private SerializedProperty _AOTMetaDllDir;
        private SerializedProperty _HotUpdateDllDir;
        private SerializedProperty _HotUpdateItems;

        public HotUpdateBuildSettingsEditor() : base("Project/HybridCLR Settings/HotUpdateBuild Settings",
            SettingsScope.Project)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            InitGUI();
        }

        private void InitGUI()
        {
            var setting = HotUpdateBuildSettings.LoadOrCreate();
            _serializedObject?.Dispose();
            _serializedObject = new SerializedObject(setting);
            _commitPrefix = _serializedObject.FindProperty("commitPrefix");
            _versionControl = _serializedObject.FindProperty("versionControl");
            _hotUpdateSettingsFile = _serializedObject.FindProperty("HotUpdateSettingsFile");
            _AOTDllBackupDir = _serializedObject.FindProperty("AOTDllBackupDir");
            _AOTMetaDllDir = _serializedObject.FindProperty("AOTMetaDllDir");
            _HotUpdateDllDir = _serializedObject.FindProperty("HotUpdateDllDir");
            _HotUpdateItems = _serializedObject.FindProperty("HotUpdateItems");
        }

        public override void OnGUI(string searchContext)
        {
            if (_serializedObject == null || !_serializedObject.targetObject)
            {
                InitGUI();
            }

            _serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_commitPrefix);
            EditorGUILayout.PropertyField(_versionControl);
            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(_hotUpdateSettingsFile);
            EditorGUILayout.PropertyField(_AOTDllBackupDir);
            EditorGUILayout.PropertyField(_AOTMetaDllDir);
            EditorGUILayout.PropertyField(_HotUpdateDllDir);
            EditorGUILayout.PropertyField(_HotUpdateItems);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedObject.ApplyModifiedProperties();
                HotUpdateBuildSettings.Save(true);
            }
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            HotUpdateBuildSettings.Save();
        }

        static HotUpdateBuildSettingsEditor s_provider;

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            if (s_provider == null)
            {
                s_provider = new HotUpdateBuildSettingsEditor();
            }

            return s_provider;
        }
    }
}