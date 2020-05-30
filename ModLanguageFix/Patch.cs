using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;

namespace ModLanguageFix
{
	[BepInPlugin("net.icanhazcode.stationeers.modlanguagefix","ModLanguageFix","0.1.0.0")]
	public class Patch : BaseUnityPlugin
	{
		public static Patch Instance;
		public static bool Debug = false;

		public Patch()
		{
			Instance = this;
			Debug = Config.Bind(new ConfigDefinition("Logging", "Debug"),
								false,
								new ConfigDescription("Logs transpiler fixes and resultant code")).Value;
		}

		public void Log(string line)
		{
			Logger.LogInfo(line);
		}

		public void LogError(string line)
		{
			Logger.LogError(line);
		}

		public void Awake()
		{
			Log("Patching WorldManager.LoadGameData");
			try
			{
				var harmony = new Harmony("net.icanhazcode.stationeers.modlanguagefix");
				harmony.PatchAll();
			}
			catch(Exception ex)
			{
				LogError($"Exception thrown:\n{ex.Message}");
				throw ex;
			}
			Log("Patch succeeded.");
		}

	}
}
