using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using Il2Cpp;

namespace WagePerks
{
    /// <summary>
    /// 运行时检视器 - 鼠标悬停显示对象的key/id/字段名
    /// IL2CPP下无IMGUI，改用日志输出（每0.5秒输出当前悬停对象）
    /// </summary>
    public static class RuntimeInspector
    {
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
        }

        public static void OnUpdate()
        {
            if (!_initialized) return;
            // 【1.1.4 已清理】点击诊断 RaycastAll 曾干扰 StandaloneInputModule 点击派发，整段移除
        }

        // IL2CPP下无IMGUI，OnGUI空实现（保留接口）
        public static void OnGUI() { }

        /// <summary>
        /// 检测鼠标下的UI元素
        // 10-19 P3 C组清理：GetHoveredUIElement() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）

        /// <summary>
        /// 构建悬停信息
        // 10-19 P3 C组清理：BuildHoverInfo() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）

        /// <summary>
        /// 获取层级路径
        /// </summary>
        private static string GetHierarchyPath(Transform transform)
        {
            try
            {
                string path = transform.name;
                Transform current = transform.parent;
                int depth = 0;
                while (current != null && depth < 8)
                {
                    path = current.name + "/" + path;
                    current = current.parent;
                    depth++;
                }
                return path;
            }
            catch { return "未知"; }
        }

        /// <summary>
        /// 尝试获取key/id字段
        /// </summary>
        private static string GetKeyFields(GameObject obj)
        {
            try
            {
                List<string> keys = new List<string>();
                Component[] components = obj.GetComponents<Component>();

                foreach (Component comp in components)
                {
                    if (comp == null) continue;
                    Type type = comp.GetType();

                    // 搜索常见的key/id字段
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    foreach (FieldInfo field in fields)
                    {
                        string name = field.Name.ToLower();
                        if (name.Contains("id") || name.Contains("key") || name.Contains("identifier") ||
                            name.Contains("clientid") || name.Contains("itemid") || name.Contains("tag"))
                        {
                            try
                            {
                                object value = field.GetValue(comp);
                                if (value != null)
                                {
                                    keys.Add($"{field.Name}={value}");
                                }
                            }
                            catch { }
                        }
                    }

                    // 搜索属性
                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (PropertyInfo prop in props)
                    {
                        string name = prop.Name.ToLower();
                        if (name.Contains("id") || name.Contains("key") || name.Contains("identifier") ||
                            name.Contains("clientid") || name.Contains("itemid") || name.Contains("tag"))
                        {
                            try
                            {
                                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                {
                                    object value = prop.GetValue(comp, null);
                                    if (value != null)
                                    {
                                        keys.Add($"{prop.Name}={value}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                return keys.Count > 0 ? string.Join(",", keys.ToArray()) : "无key字段";
            }
            catch { return "无key字段"; }
        }

        /// <summary>
        /// Dump完整字段到文件
        // 10-19 P3 C组清理：DumpObjectToFile() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）
    }
}
