using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using Il2Cpp;

namespace JacksonPerks
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
        /// </summary>
        private static GameObject GetHoveredUIElement()
        {
            try
            {
                if (EventSystem.current == null) return null;

                Vector2 mousePos = Input.mousePosition;
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = new Vector2(mousePos.x, mousePos.y);

                Il2CppSystem.Collections.Generic.List<RaycastResult> results = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    return results[0].gameObject;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 构建悬停信息
        /// </summary>
        private static string BuildHoverInfo(GameObject obj)
        {
            try
            {
                string info = "";
                info += $"名称:{obj.name} | ";
                info += $"场景:{obj.scene.name} | ";

                // 层级路径
                string path = GetHierarchyPath(obj.transform);
                info += $"路径:{path} | ";

                // 挂载的组件
                Component[] components = obj.GetComponents<Component>();
                if (components != null && components.Length > 0)
                {
                    info += $"组件({components.Length}):";
                    for (int i = 0; i < Math.Min(components.Length, 5); i++)
                    {
                        if (components[i] != null)
                        {
                            info += components[i].GetType().Name;
                            if (i < Math.Min(components.Length, 5) - 1) info += ",";
                        }
                    }
                    if (components.Length > 5) info += "...";
                    info += " | ";
                }

                // 尝试获取key/id字段
                string keyInfo = GetKeyFields(obj);
                if (!string.IsNullOrEmpty(keyInfo))
                {
                    info += $"Key:{keyInfo}";
                }

                return info;
            }
            catch (Exception ex)
            {
                return $"构建信息失败:{ex.Message}";
            }
        }

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
        /// </summary>
        private static void DumpObjectToFile(GameObject obj)
        {
            try
            {
                string dumpPath = Path.Combine(Application.dataPath, "..", "Mods", "inspector_dump.txt");
                using (StreamWriter writer = new StreamWriter(dumpPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine($"===== Runtime Inspector Dump =====");
                    writer.WriteLine($"时间: {DateTime.Now}");
                    writer.WriteLine($"对象名: {obj.name}");
                    writer.WriteLine($"类型: {obj.GetType().FullName}");
                    writer.WriteLine($"场景: {obj.scene.name}");
                    writer.WriteLine($"路径: {GetHierarchyPath(obj.transform)}");
                    writer.WriteLine("");

                    Component[] components = obj.GetComponents<Component>();
                    writer.WriteLine($"===== 组件列表 ({components.Length}) =====");
                    foreach (Component comp in components)
                    {
                        if (comp == null) continue;
                        writer.WriteLine($"\n--- {comp.GetType().FullName} ---");

                        // 字段
                        FieldInfo[] fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        writer.WriteLine($"  字段 ({fields.Length}):");
                        foreach (FieldInfo field in fields)
                        {
                            try
                            {
                                object value = field.GetValue(comp);
                                string valueStr = value != null ? value.ToString() : "null";
                                if (valueStr.Length > 200) valueStr = valueStr.Substring(0, 200) + "...";
                                writer.WriteLine($"    {field.FieldType.Name} {field.Name} = {valueStr}");
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"    {field.Name} = [读取失败: {ex.Message}]");
                            }
                        }

                        // 属性
                        PropertyInfo[] props = comp.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        writer.WriteLine($"  属性 ({props.Length}):");
                        foreach (PropertyInfo prop in props)
                        {
                            try
                            {
                                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                {
                                    object value = prop.GetValue(comp, null);
                                    string valueStr = value != null ? value.ToString() : "null";
                                    if (valueStr.Length > 200) valueStr = valueStr.Substring(0, 200) + "...";
                                    writer.WriteLine($"    {prop.PropertyType.Name} {prop.Name} = {valueStr}");
                                }
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"    {prop.Name} = [读取失败: {ex.Message}]");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[RuntimeInspector] Dump失败: {ex.Message}");
            }
        }
    }
}
