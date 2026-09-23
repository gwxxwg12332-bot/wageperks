using System;

using System.Reflection;

using HarmonyLib;

using Il2Cpp;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;



// 箱子复制功能（从DoctorShopPlus移植）

internal static class BoxDuplicator

{

    private static MethodInfo placeAtMethod;

    private static MethodInfo machineBayMethod;

    private static MethodInfo storageBayMethod;

    private static GameItem lastCreatedBox = null;

    private static bool isAdding = false;

    // 用Guid生成唯一ID，避免从固定数字开始自增导致读档后重复
    private static int GenerateUniqueId()
    {
        return Math.Abs(Guid.NewGuid().GetHashCode()) + 1000000;
    }

    private const int EXTRA_BOXES = 5;



    internal static void Initialize()

    {

        try

        {

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)

            {

                if (!assembly.GetName().Name.StartsWith("Assembly-CSharp")) continue;

                Type[] types;

                try { types = assembly.GetTypes(); }

                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                foreach (Type type in types)

                {

                    if (type == null) continue;

                    if (type.Name == "PowerHelper")

                    {

                        placeAtMethod = type.GetMethod("PlaceAt", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

                    }

                    if (type.Name == "ShipSystemDirectory")

                    {

                        machineBayMethod = type.GetMethod("MachineBayExt", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

                        storageBayMethod = type.GetMethod("StorageBayLarge", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

                    }

                }

            }

            Core.LogMsg("箱子复制功能初始化完成");

        }

        catch (Exception ex)

        {

            MelonLogger.Error("箱子复制初始化失败: " + ex.Message);

        }

    }



    internal static void BayCreated(ref GameItem __result)

    {

        try

        {

            if (!isAdding && __result != null)

            {

                lastCreatedBox = __result;

            }

        }

        catch (System.Exception ex) { Core.LogMsg("[BoxDuplicator] 异常: " + ex.Message); }

    }



    private static bool IsBox(GameItem item)

    {

        if (item == null) return false;

        string text = item.name ?? "";

        if (!text.Contains("机器") && !text.Contains("储存") && !text.Contains("Machine") && !text.Contains("Storage"))

        {

            return text.Contains("扩建");

        }

        return true;

    }



    private static object GetShipDir()

    {

        if (machineBayMethod == null) return null;

        Type declaringType = machineBayMethod.DeclaringType;

        PropertyInfo property = declaringType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);

        if (property != null) return property.GetValue(null);

        try { return Activator.CreateInstance(declaringType); }

        catch { return null; }

    }



    private static bool PlaceItem(GameItem item, object inv)

    {

        try

        {

            if (item == null || placeAtMethod == null) return false;

            try

            {

                PropertyInfo property = ((object)item).GetType().GetProperty("uniqueId");

                if (property != null && property.CanWrite)

                {

                    property.SetValue(item, GenerateUniqueId());

                }

            }

            catch (System.Exception ex) { Core.LogMsg("[BoxDuplicator] 异常: " + ex.Message); }

            PropertyInfo property2 = ((object)item).GetType().GetProperty("shape");

            object obj2 = ((property2 != null) ? property2.GetValue(item) : null);

            placeAtMethod.Invoke(null, new object[3] { item, inv, obj2 });

            return true;

        }

        catch { return false; }

    }



    private static GameItem CreateBox(GameItem original)

    {

        try

        {

            object shipDir = GetShipDir();

            if (shipDir == null) return null;

            string text = original.name ?? "";

            MethodInfo methodInfo = ((text.Contains("机器") || text.Contains("Machine")) ? machineBayMethod : storageBayMethod);

            if (methodInfo == null) return null;

            object obj = methodInfo.Invoke(shipDir, null);

            GameItem val = (GameItem)((obj is GameItem) ? obj : null);

            if (val == null) return null;

            string[] array = new string[13]

            {

                "name", "shortDescription", "longDescription", "flavorText", "unitValue", "lateUnitValue", "unitBaseValue", "backupUnitValue", "spritePath", "spriteAtlasPath",

                "soundDragStart", "soundContainerOpen", "identifier"

            };

            foreach (string name in array)

            {

                try

                {

                    PropertyInfo property = ((object)original).GetType().GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (property != null && property.CanWrite && property.CanRead)

                    {

                        object value = property.GetValue(original);

                        if (value != null) property.SetValue(val, value);

                    }

                }

                catch (System.Exception ex) { Core.LogMsg("[BoxDuplicator] 异常: " + ex.Message); }

            }

            try

            {

                PropertyInfo property2 = ((object)val).GetType().GetProperty("uniqueId");

                if (property2 != null && property2.CanWrite)

                {

                    property2.SetValue(val, GenerateUniqueId());

                }

            }

            catch (System.Exception ex) { Core.LogMsg("[BoxDuplicator] 异常: " + ex.Message); }

            return val;

        }

        catch { return null; }

    }



    internal static void GridInvAccepted(object __instance, GameItem item)

    {

        try

        {

            if (isAdding || !IsBox(item) || lastCreatedBox == null || item != lastCreatedBox) return;

            isAdding = true;

            lastCreatedBox = null;

            int num = 0;

            for (int i = 0; i < EXTRA_BOXES; i++)

            {

                GameItem val = CreateBox(item);

                if (val != null && PlaceItem(val, __instance))

                {

                    num++;

                }

            }

            isAdding = false;

        }

        catch

        {

            isAdding = false;

            lastCreatedBox = null;

        }

    }

}



// 补丁：箱子创建时记录

// [HarmonyPatch(typeof(ShipSystemDirectory), "MachineBayExt")]

internal static class MachineBayExtPatch

{

    static void Postfix(ref GameItem __result)

    {

        BoxDuplicator.BayCreated(ref __result);

    }

}



// [HarmonyPatch(typeof(ShipSystemDirectory), "StorageBayLarge")]

internal static class StorageBayLargePatch

{

    static void Postfix(ref GameItem __result)

    {

        BoxDuplicator.BayCreated(ref __result);

    }

}



// 补丁：箱子放入库存时复制

// [HarmonyPatch(typeof(GameGridInventory), "UncheckedAccept")]

internal static class GridInvAcceptedPatch

{

    static void Postfix(object __instance, GameItem item)

    {

        BoxDuplicator.GridInvAccepted(__instance, item);

    }

}

