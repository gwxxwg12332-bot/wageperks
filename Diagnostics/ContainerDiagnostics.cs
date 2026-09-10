using System;
using System.Reflection;
using System.IO;
using UnityEngine;
using MelonLoader;
using Il2Cpp;

namespace JacksonPerks
{
    /// <summary>
    /// 容器诊断补丁 - 按F8 dump MiniSmugglerBay和MachineBayExt的完整结构
    /// </summary>
    public static class ContainerDiagnostics
    {
        private static bool _initialized = false;
        private static float _lastDumpTime = 0f;
        private static string _dumpFile = "";

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _dumpFile = Path.Combine(Application.dataPath, "..", "Mods", "container_diagnostics.txt");
            Core.LogMsg("[容器诊断] 初始化完成，按F8 dump MiniSmugglerBay和MachineBayExt结构");
            // 不在Init时dump（游戏未完全初始化，创建物品会失败），改为OnUpdate中检测到游戏就绪后自动dump
        }

        private static bool _autoDumpDone = false;

        public static void OnUpdate()
        {
            if (!Core.DebugMode) return;
            if (!_initialized) return;

            try
            {
                // 自动dump：检测到游戏完全初始化后（PlayerStore存在且场景是InventoryScene）自动dump一次
                if (!_autoDumpDone)
                {
                    try
                    {
                        if (PlayerStore.Instance != null && PlayerStore.Instance.playerCash > 0)
                        {
                            _autoDumpDone = true;
                            DumpContainers();
                        }
                    }
                    catch { }
                }

                if (Input.GetKeyDown(KeyCode.F8))
                {
                    if (Time.time - _lastDumpTime > 1f)
                    {
                        _lastDumpTime = Time.time;
                        DumpContainers();
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[容器诊断] 异常: {ex.Message}");
            }
        }

        private static void DumpContainers()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(_dumpFile, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine($"===== 容器结构诊断 =====\n时间: {DateTime.Now}\n");

                    // 0. 先dump所有容器的shape大小
                    writer.WriteLine("===== 0. 各容器shape大小对比 =====");
                    string[] containerIds = { "storage_bay", "storage_bay_large", "machine_bay", "machine_bay_ext", "mini_smuggler_bay", "smuggler_bay", "backpack_medium", "backpack_large" };
                    foreach (string cid in containerIds)
                    {
                        try
                        {
                            GameItem ci = DirectoryMaster.Item(cid);
                            if (ci != null && ci.shape != null)
                            {
                                writer.WriteLine($"  {cid}: width={ci.shape.width}, height={ci.shape.height}, globalWidth={ci.shape.globalWidth}, globalHeight={ci.shape.globalHeight}");
                            }
                            else
                            {
                                writer.WriteLine($"  {cid}: 创建失败或shape为null");
                            }
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"  {cid}: 异常 {ex.Message}");
                        }
                    }
                    writer.WriteLine("");

                    // 1. MiniSmugglerBay
                    writer.WriteLine("===== 1. MiniSmugglerBay（迷你走私者暗格） =====");
                    try
                    {
                        GameItem miniBay = DirectoryMaster.Item("mini_smuggler_bay");
                        if (miniBay != null)
                        {
                            DumpGameItem(writer, miniBay, "MiniSmugglerBay");
                        }
                        else
                        {
                            writer.WriteLine("  创建失败，返回null");
                        }
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine($"  创建异常: {ex.Message}\n{ex.StackTrace}");
                    }

                    writer.WriteLine("\n");

                    // 2. MachineBayExt
                    writer.WriteLine("===== 2. MachineBayExt（机器区扩建） =====");
                    try
                    {
                        GameItem machineExt = DirectoryMaster.Item("machine_bay_ext");
                        if (machineExt != null)
                        {
                            DumpGameItem(writer, machineExt, "MachineBayExt");
                        }
                        else
                        {
                            writer.WriteLine("  创建失败，返回null");
                        }
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine($"  创建异常: {ex.Message}\n{ex.StackTrace}");
                    }

                    writer.WriteLine("\n");

                    // 3. StorageBayLarge（大储物箱，参考）
                    writer.WriteLine("===== 3. StorageBayLarge（大储物箱） =====");
                    try
                    {
                        GameItem largeBay = DirectoryMaster.Item("storage_bay_large");
                        if (largeBay != null)
                        {
                            DumpGameItem(writer, largeBay, "StorageBayLarge");
                        }
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine($"  创建异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[容器诊断] Dump失败: {ex.Message}");
            }
        }

        private static void DumpGameItem(StreamWriter writer, GameItem item, string name)
        {
            try
            {
                writer.WriteLine($"  identifier: {item.identifier}");
                writer.WriteLine($"  name: {item.name}");

                // 所有属性
                PropertyInfo[] props = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                writer.WriteLine($"\n  --- 所有属性 ({props.Length}) ---");
                foreach (PropertyInfo prop in props)
                {
                    try
                    {
                        if (prop.GetIndexParameters().Length > 0) continue;
                        object value = prop.GetValue(item, null);
                        string valueStr = value != null ? value.ToString() : "null";
                        if (valueStr.Length > 200) valueStr = valueStr.Substring(0, 200) + "...";
                        writer.WriteLine($"    {prop.PropertyType.Name} {prop.Name} = {valueStr}");
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine($"    {prop.Name} = [读取失败: {ex.Message}]");
                    }
                }

                // GridShape详细信息（船上占用空间）
                writer.WriteLine("\n  --- GridShape（船上占用空间） ---");
                try
                {
                    PropertyInfo shapeProp = item.GetType().GetProperty("shape");
                    if (shapeProp != null)
                    {
                        object shape = shapeProp.GetValue(item, null);
                        if (shape != null)
                        {
                            writer.WriteLine("    类型: {shape.GetType().FullName}");
                            PropertyInfo[] shapeProps = shape.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (PropertyInfo p in shapeProps)
                            {
                                try
                                {
                                    if (p.GetIndexParameters().Length > 0) continue;
                                    object v = p.GetValue(shape, null);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 100) vs = vs.Substring(0, 100) + "...";
                                    writer.WriteLine("    {p.PropertyType.Name} {p.Name} = {vs}");
                                }
                                catch { }
                            }
                            FieldInfo[] shapeFields = shape.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (FieldInfo f in shapeFields)
                            {
                                try
                                {
                                    object v = f.GetValue(shape);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 100) vs = vs.Substring(0, 100) + "...";
                                    writer.WriteLine("    [field] {f.FieldType.Name} {f.Name} = {vs}");
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch
                {
                    writer.WriteLine("    读取GridShape异常: {ex.Message}");
                }

                // contentWindow详细信息
                writer.WriteLine($"\n  --- contentWindow详细 ---");
                try
                {
                    PropertyInfo cwProp = item.GetType().GetProperty("contentWindow");
                    if (cwProp != null)
                    {
                        object cw = cwProp.GetValue(item, null);
                        if (cw != null)
                        {
                            writer.WriteLine($"    类型: {cw.GetType().FullName}");
                            // dump所有属性
                            PropertyInfo[] cwProps = cw.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (PropertyInfo p in cwProps)
                            {
                                try
                                {
                                    if (p.GetIndexParameters().Length > 0) continue;
                                    object v = p.GetValue(cw, null);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 150) vs = vs.Substring(0, 150) + "...";
                                    writer.WriteLine($"    {p.PropertyType.Name} {p.Name} = {vs}");
                                }
                                catch { }
                            }
                            // childElement详细信息（可能包含库存）
                            try
                            {
                                PropertyInfo childProp = cw.GetType().GetProperty("childElement");
                                if (childProp != null)
                                {
                                    object child = childProp.GetValue(cw, null);
                                    if (child != null)
                                    {
                                        writer.WriteLine("    --- childElement详细 ---");
                                        writer.WriteLine("    类型: {child.GetType().FullName}");
                                        PropertyInfo[] childProps = child.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        foreach (PropertyInfo p in childProps)
                                        {
                                            try
                                            {
                                                if (p.GetIndexParameters().Length > 0) continue;
                                                object v = p.GetValue(child, null);
                                                string vs = v != null ? v.ToString() : "null";
                                                if (vs.Length > 150) vs = vs.Substring(0, 150) + "...";
                                                writer.WriteLine("      {p.PropertyType.Name} {p.Name} = {vs}");
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                writer.WriteLine("    读取childElement异常: {ex.Message}");
                            }

                            // dump所有字段
                            FieldInfo[] cwFields = cw.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (FieldInfo f in cwFields)
                            {
                                try
                                {
                                    object v = f.GetValue(cw);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 150) vs = vs.Substring(0, 150) + "...";
                                    writer.WriteLine($"    [field] {f.FieldType.Name} {f.Name} = {vs}");
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            writer.WriteLine("    contentWindow = null");
                        }
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"    读取contentWindow异常: {ex.Message}");
                }

                // itemInventory详细信息
                writer.WriteLine($"\n  --- itemInventory详细 ---");
                try
                {
                    PropertyInfo invProp = item.GetType().GetProperty("itemInventory");
                    if (invProp != null)
                    {
                        object inv = invProp.GetValue(item, null);
                        if (inv != null)
                        {
                            writer.WriteLine($"    类型: {inv.GetType().FullName}");
                            // dump所有属性
                            PropertyInfo[] invProps = inv.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (PropertyInfo p in invProps)
                            {
                                try
                                {
                                    if (p.GetIndexParameters().Length > 0) continue;
                                    object v = p.GetValue(inv, null);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 150) vs = vs.Substring(0, 150) + "...";
                                    writer.WriteLine($"    {p.PropertyType.Name} {p.Name} = {vs}");
                                }
                                catch { }
                            }
                            // dump所有字段
                            FieldInfo[] invFields = inv.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (FieldInfo f in invFields)
                            {
                                try
                                {
                                    object v = f.GetValue(inv);
                                    string vs = v != null ? v.ToString() : "null";
                                    if (vs.Length > 150) vs = vs.Substring(0, 150) + "...";
                                    writer.WriteLine($"    [field] {f.FieldType.Name} {f.Name} = {vs}");
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            writer.WriteLine("    itemInventory = null");
                        }
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"    读取itemInventory异常: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"  DumpGameItem异常: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
