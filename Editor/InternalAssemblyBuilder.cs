using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;
using UnityEngine;
using HybridCLR.Editor.Link;
using HybridCLR.Editor.Meta;
using Mono.Cecil;
using UnityEditor;

namespace HotUpdatePacker.Editor
{
    internal class InternalAssemblyBuilder
    {
        private AssemblyBuilder builder;
        private AssemblyBuilderParam buildParam;

        /// <summary>
        /// Compile Assembly
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public bool Build(AssemblyBuilderParam param)
        {
            this.buildParam = param;
            var assemblyName = param.assemblyName;
            var scripts = BuilderUtil.GetAllScripts(param.compileDir);
            var assPath = BuilderUtil.GetTempAssemblySavePath(assemblyName, param.target);
            builder = new AssemblyBuilder(assPath, scripts)
            {
                additionalReferences =
                    BuilderUtil.GetUnityEngineModuleAssembliesWithACsharp(param.otherReferences, param.target),
                additionalDefines = param.defines,
                buildTarget = param.target,
                buildTargetGroup = param.targetGroup,
                flags = param.developmentBuild ? AssemblyBuilderFlags.DevelopmentBuild : AssemblyBuilderFlags.None,
                compilerOptions =
                {
                    CodeOptimization = param.developmentBuild ? CodeOptimization.Debug : CodeOptimization.Release,
                    AdditionalCompilerArguments = new[]
                    {
                        "/deterministic", // 启用确定性编译
                        "/nostdlib+", // 最小化引用
                        "/optimize+", // 启用优
                        "/nowarn:1701,1702,2008", // 禁用特定警告
                    }
                }
            };
            builder.buildStarted += (info) => { Debug.Log($"Start Compiling!->{info}"); };
            builder.buildFinished += (info, msgs) =>
            {
                Debug.Log("Compile Finished!");
                var sucess = true;
                foreach (var msg in msgs)
                {
                    if (msg.type == CompilerMessageType.Error)
                    {
                        sucess = false;
                        Debug.LogError($"{msg.message}");
                    }
                }

                if (sucess)
                {
                    FixTimeStamp(assPath);
                    FixMvid(assPath, param.Mvid);
                }

                else
                {
                    throw new Exception($"Build assembly fail->{info}");
                }

                if (buildParam.generateXml)
                {
                    GenerateHotAssemblyLinkXml(buildParam);
                }
            };
            return builder.Build();
        }

        /// <summary>
        /// 固定MVID
        /// </summary>
        /// <param name="assemblyPath"></param>
        private static void FixMvid(string assemblyPath, string mvid)
        {
            var fixedMvid = new Guid(mvid);
            var temp = $"Library/fixedmvid.dll";
            using (var assembly =
                   AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadWrite = true }))
            {
                assembly.MainModule.Mvid = fixedMvid;
                assembly.Write(temp);
            }

            File.Copy(temp, assemblyPath, true);
        }

        /// <summary>
        /// 固定时间戳
        /// </summary>
        /// <param name="assemblyPath"></param>
        private static void FixTimeStamp(string assemblyPath)
        {
            try
            {
                var dllBytes = File.ReadAllBytes(assemblyPath);
                // 1. 检查DOS头签名 "MZ"
                if (dllBytes[0] != 0x4D || dllBytes[1] != 0x5A)
                {
                    Debug.LogError("Invalid DLL: Missing MZ header");
                    return;
                }

                // 2. 定位PE头偏移 (e_lfanew在偏移0x3C处)
                var peHeaderOffset = BitConverter.ToInt32(dllBytes, 0x3C);

                // 3. 验证PE头签名 "PE\0\0"
                if (peHeaderOffset < 0 ||
                    dllBytes[peHeaderOffset] != 0x50 ||
                    dllBytes[peHeaderOffset + 1] != 0x45 ||
                    dllBytes[peHeaderOffset + 2] != 0x00 ||
                    dllBytes[peHeaderOffset + 3] != 0x00)
                {
                    Debug.LogError("Invalid PE header");
                    return;
                }

                // 4. 时间戳在PE头后偏移+8的位置
                var timestampOffset = peHeaderOffset + 8;
                if (timestampOffset + 4 > dllBytes.Length)
                {
                    Debug.LogError("DLL file too small");
                    return;
                }

                var fixedTime = 0xDEF05F00;
                // 5. 覆盖时间戳字段 (4字节小端序)
                var timestampBytes = BitConverter.GetBytes(fixedTime);
                Array.Copy(timestampBytes, 0, dllBytes, timestampOffset, 4);
                // 6. 写回文件
                File.WriteAllBytes(assemblyPath, dllBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set timestamp: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate linkxml
        /// </summary>
        /// <param name="param"></param>
        public static void GenerateHotAssemblyLinkXml(AssemblyBuilderParam param)
        {
            var assemblyName = param.assemblyName;
            var path = param.compileDir;
            var target = param.target;
            var hotfixAssemblies = new List<string> { assemblyName };
            var analyzer = new Analyzer(MetaUtil.CreateHotUpdateAndAOTAssemblyResolver(target, hotfixAssemblies));
            var refTypes = analyzer.CollectRefs(hotfixAssemblies);
            var linkpath = $"{path}/link.xml";
            Debug.Log(
                $"[LinkGeneratorCommand] hotfix assembly count:{hotfixAssemblies.Count}, ref type count:{refTypes.Count} output:{Application.dataPath}/{linkpath}");
            var linkXmlWriter = new LinkXmlWriter();
            linkXmlWriter.Write(linkpath, refTypes);
            AssetDatabase.Refresh();
        }
    }
}