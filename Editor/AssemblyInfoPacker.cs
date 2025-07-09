using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace HotUpdatePacker.Editor
{
    public class AssemblyInfoPacker
    {
        public static void PackAssemblyInfos(IList<string> assNames)
        {
            var guids = AssetDatabase.FindAssets("_AssemblyInfo_ t:Script");
            var assemblyInfoPath = "";
            if (guids.Length == 0)
            {
                var path = "Assets/HotUpdatePackerGenerate/Reflector";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                assemblyInfoPath = $"{path}/_AssemblyInfo_.cs";
                File.Create(assemblyInfoPath);
                AssetDatabase.ImportAsset(assemblyInfoPath);
            }
            else
            {
                assemblyInfoPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            var pattern = @"""((?:\\.|[^""\\])*)""";
            var visibles = BuilderUtil.GetMatchedContents(assemblyInfoPath, pattern);
            var isDirty = BuilderUtil.IsContentDirty(visibles, assNames);
            if (isDirty)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("//Don't modify this file as it was auto generated!");
                stringBuilder.AppendLine("using System.Runtime.CompilerServices;");
                foreach (var ass in assNames)
                {
                    stringBuilder.AppendLine($"[assembly:InternalsVisibleTo(\"{ass}\")]");
                }

                var content = stringBuilder.ToString();
                File.WriteAllText(assemblyInfoPath, content);
            }
        }
    }
}