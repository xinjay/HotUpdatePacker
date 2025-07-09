using System;
using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEngine;
using HybridCLR.Editor.Link;
using HybridCLR.Editor.Meta;
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
                    CodeOptimization = param.developmentBuild ? CodeOptimization.Debug : CodeOptimization.Release
                }
            };
            builder.buildStarted += OnBuildStarted;
            builder.buildFinished += OnBuilderFinish;
            return builder.Build();
        }

        private void OnBuildStarted(string info)
        {
            Debug.Log($"Start Compiling!->{info}");
        }

        private void OnBuilderFinish(string info, CompilerMessage[] msgs)
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
            if (!sucess)
                throw new Exception($"Build assembly fail->{info}");
            if (buildParam.generateXml)
            {
                GenerateHotAssemblyLinkXml(buildParam);
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