using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HotUpdatePacker.Runtime
{
    public static class HotUpdateAOT
    {
        private static Dictionary<string, Assembly> hotupdataAssMap = new();
        private static Dictionary<string, Type> hotUpdateTypeMap = new();

        public static void Clear()
        {
            hotupdataAssMap.Clear();
            hotUpdateTypeMap.Clear();
        }

        /// <summary>
        /// 注册反射类型
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="type"></param>
        public static void RegisteRefelectionType(string klassFullName, Type type)
        {
            hotUpdateTypeMap.TryAdd(klassFullName, type);
        }

        /// <summary>
        /// 注册反射程序集
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="ass"></param>
        public static void RegisteReflectionAssembly(string assemblyName, Assembly ass)
        {
            hotupdataAssMap.TryAdd(assemblyName, ass);
        }
        /// <summary>
        /// 反射调用方法
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="methodName"></param>
        /// <param name="flags"></param>
        /// <param name="target"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object ReflectCallMethod(string klassFullName, string methodName, BindingFlags flags,
            object target, params object[] args)
        {
            var result = default(object);
            if (TryGetHotUpdateHotUpdateType(klassFullName, out var type))
            {
                var method = type.GetMethod(methodName, flags);
                result = method.Invoke(target, args);
            }

            return result;
        }

        /// <summary>
        /// 反射创建实例
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object ReflectCreateInstance(string klassFullName, params object[] args)
        {
            var result = default(object);
            if (TryGetHotUpdateHotUpdateType(klassFullName, out var type))
            {
                result = Activator.CreateInstance(type, args);
            }

            return result;
        }

        /// <summary>
        /// 获取反射类型
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool TryGetHotUpdateHotUpdateType(string klassFullName, out Type type)
        {
            if (!hotUpdateTypeMap.TryGetValue(klassFullName, out type))
            {
                if (GetHotUpdateAssembly(klassFullName, out var ass))
                {
                    type = ass.GetType(klassFullName);
                    hotUpdateTypeMap.Add(klassFullName, type);
                }
            }

            return type != null;
        }

        /// <summary>
        /// 获取反射程序集
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static bool GetHotUpdateAssembly(string klassFullName, out Assembly assembly)
        {
            if (hotupdataAssMap.TryGetValue(klassFullName, out assembly))
                return true;
            foreach (var pair in hotupdataAssMap)
            {
                var ass = pair.Value;
                if (FindTypeInAssembly(klassFullName, ass))
                {
                    assembly = ass;
                    return true;
                }
            }
            return GetAssemblyInGlobal(klassFullName, out assembly);
        }

        /// <summary>
        /// 从全局程序集中查找类型
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static bool GetAssemblyInGlobal(string klassFullName, out Assembly assembly)
        {
            Debug.Log(
                $"---------------------------------------HotUpdateAOT GetAssemblyInGlobal:{klassFullName}---------------------------------------");
            assembly = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var ass in assemblies)
            {
                if (FindTypeInAssembly(klassFullName, ass))
                {
                    assembly = ass;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 从指定程序集中查找类型
        /// </summary>
        /// <param name="klassFullName"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static bool FindTypeInAssembly(string klassFullName, Assembly assembly)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.FullName == klassFullName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}