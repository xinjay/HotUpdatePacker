using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HotUpdatePacker.Runtime
{
    public static class HotUpdateAOTDefines
    {
        //Assembly
        public const string ReflectorAssName = "Reflector";

        //Type
        public const string ReflectorFullName = "Reflector.AssemblyReflector";
        
        
        //Method
        public const string ReflectCall = "OnHotUpdateAssembliesLoaded";


        //Other
        public const string HotUpdateSettingsName = "HotUpdateSettings";
        
    }
}