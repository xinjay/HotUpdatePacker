using System.Collections.Generic;
using UnityEngine;

namespace HotUpdatePacker.Editor
{
    public class AssemblyBuilderManager
    {
        private static HashSet<string> assemblyInBuild = new();
        public static void ClearBuilders()
        {
            assemblyInBuild.Clear();
        }

        /// <summary>
        /// Compile HotUpdate Assembly
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static void Build(AssemblyBuilderParam param)
        {
            var assemblyName = param.assemblyName;
            if (!assemblyInBuild.Add(assemblyName))
            {
                Debug.LogError($"Assembly is in building!->{assemblyName}");
                return;
            }

            try
            {
                var builder = new InternalAssemblyBuilder();
                builder.Build(param);
            }
            finally
            {
                assemblyInBuild.Remove(assemblyName);
            }
        }
    }
}