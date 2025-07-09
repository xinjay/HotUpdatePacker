using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HybridCLR.Editor;
using UnityEditor;
using UnityEditorInternal;

namespace HotUpdatePacker.Editor
{
    internal static class BuilderUtil
    {
        public static string[] GetAllScripts(string dir)
        {
            var scriptsList = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .TakeWhile(item => !item.Contains("/Editor/") && !item.Contains("\\Editor\\")).ToList(); //不含Editor脚本
            var guids = AssetDatabase.FindAssets("_AssemblyInfo_ t:Script");
            if (guids.Length != 0)
            {
                var assemblyInfoPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                scriptsList.Add(assemblyInfoPath);
            }

            var scripts = scriptsList.ToArray();
            return scripts;
        }

        public static string[] GetUnityEngineModuleAssembliesWithACsharp(string[] customReference, BuildTarget target)
        {
            var assemblyPath =
                Path.Combine(Path.GetDirectoryName(InternalEditorUtility.GetEditorAssemblyPath()) ?? string.Empty,
                    "UnityEngine");
            var dlls = Directory.GetFiles(assemblyPath, "*.dll").ToList();
            var targetDllDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            dlls.Add($"{targetDllDir}/Assembly-CSharp.dll");
            if (customReference != null)
            {
                dlls.AddRange(customReference);
            }

            return dlls.ToArray();
        }

        public static string GetTempAssemblySavePath(string assemblyName, BuildTarget target)
        {
            var root = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            var assPath = Path.Combine(root, $"{assemblyName}.dll");
            return assPath;
        }

        public static bool IsContentDirty(IList<string> contentList1, IList<string> contentList2)
        {
            if (contentList1.Count != contentList2.Count)
                return true;
            var len = contentList1.Count;
            for (var index = 0; index < len; index++)
            {
                if (contentList1[index] != contentList2[index])
                    return true;
            }

            return false;
        }

        public static void CreateDir(string dir, bool deleteIfExist)
        {
            var exist = Directory.Exists(dir);
            if (deleteIfExist && exist)
            {
                Directory.Delete(dir, true);
                exist = false;
            }

            if (!exist)
                Directory.CreateDirectory(dir);
        }

        public static List<string> GetMatchedContents(string path, string pattern)
        {
            var result = new List<string>();
            var input = File.ReadAllText(path);
            var matches = Regex.Matches(input, pattern);
            foreach (Match match in matches)
            {
                var group = match.Groups[1];
                if (group.Success)
                {
                    var content = group.Value;
                    result.Add(content);
                }
            }

            return result;
        }

        public static bool HasTarget(this HotUpdatePackFlag flag, HotUpdatePackFlag target)
        {
            return (flag & target) != 0;
        }
        
        public static string RunCommand(string appPath, string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = new Process();
            process.StartInfo = startInfo;
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) error.AppendLine(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"SVN command failed: {error}");
            }

            return output.ToString();
        }
    }
}