using System;
using System.Collections.Generic;

namespace PotatoOptimization.Utilities
{
    /// <summary>
    /// 类型辅助工具 - 提供带缓存的类型查找功能
    /// </summary>
    public static class TypeHelper
    {
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        /// <summary>
        /// 获取游戏的 PulldownListUI 类型 (带缓存)
        /// </summary>
        public static Type GetPulldownUIType()
        {
            const string cacheKey = "PulldownUI";

            if (_typeCache.TryGetValue(cacheKey, out Type cachedType))
                return cachedType;

            Type type = Type.GetType("Bulbul.PulldownListUI, Assembly-CSharp")
                ?? Type.GetType("PulldownListUI, Assembly-CSharp")
                ?? Type.GetType("PulldownListUI");

            if (type != null)
            {
                _typeCache[cacheKey] = type;
            }

            return type;
        }

        /// <summary>
        /// 获取游戏的 SettingUI 类型
        /// </summary>
        public static Type GetSettingUIType()
        {
            const string cacheKey = "SettingUI";

            if (_typeCache.TryGetValue(cacheKey, out Type cachedType))
                return cachedType;

            Type type = Type.GetType("Bulbul.SettingUI, Assembly-CSharp")
                ?? Type.GetType("SettingUI, Assembly-CSharp")
                ?? Type.GetType("SettingUI");

            if (type != null)
            {
                _typeCache[cacheKey] = type;
            }

            return type;
        }

        /// <summary>
        /// 清除类型缓存 (用于场景切换等情况)
        /// </summary>
        public static void ClearCache()
        {
            _typeCache.Clear();
        }

        /// <summary>
        /// 通用的类型查找方法 (带缓存)
        /// </summary>
        public static Type FindType(string typeName, params string[] assemblyNames)
        {
            if (_typeCache.TryGetValue(typeName, out Type cachedType))
                return cachedType;

            Type type = null;

            // 尝试不同的程序集名称
            foreach (var assemblyName in assemblyNames)
            {
                type = Type.GetType($"{typeName}, {assemblyName}");
                if (type != null) break;
            }

            // 最后尝试不带程序集名称
            if (type == null)
            {
                type = Type.GetType(typeName);
            }

            if (type != null)
            {
                _typeCache[typeName] = type;
            }

            return type;
        }
    }
}
