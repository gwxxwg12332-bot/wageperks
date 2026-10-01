using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Reflection;
using Il2CppTMPro;
using WagePerks;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 吞噬季（RiskTaker 特性）：机器模块互食系统
namespace WagePerks;

internal static partial class ModCannibalism
{
	private static readonly string[] MOD_TAGS = new string[3] { "BONUS_PERCENTAGE_PERFORMANCE_INT", "BONUS_PERCENTAGE_EFFICIENCY_INT", "BONUS_PERCENTAGE_QUALITY_INT" };

	internal static string LastNewsLine = null;

	private static bool _nightTabInserted = false;


	// 09-23 修复「吞噬季将节点排除在吞噬范围之外」：节点不参与吞噬判定
	// 拆包实锤 [L1]：ModuleDirectory.txt:1051 "node_small" / :1067 "node_medium" 与 system_module_*、
	// chem_module、furnace_module_* 在同一张模组注册表里 → 节点在原生定义中就是模组（带 MODULE_TAG），
	// 会被 CollectMachines 收进候选池并被吞掉。
	// 只在吞噬季生效；不改 RobinCrusoePerk.IsExcludedModule（那个还被炼蛊器/模组 tooltip 共用，改了会动到别的机制）。








}