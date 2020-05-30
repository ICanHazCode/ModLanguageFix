using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
//using System.Reflection.Emit;
using System.Text;
using System.Xml.Serialization;
using Assets.Scripts;
using Assets.Scripts.Serialization;
using Assets.Scripts.Util;
using BepInEx.Harmony;
using HarmonyLib;
using ICanHazCode.ModUtils;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using MonoMod;
using MonoMod.Utils.Cil;
using UnityEngine;
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;
using SRE = System.Reflection.Emit;
namespace ModLanguageFix
{
	[HarmonyPatch]
	class WorldManagerPatch
	{
		#region Logging
		static void Log(string line)
		{
			Patch.Instance.Log(line);
		}

		static void LogError(string line)
		{
			Patch.Instance.LogError(line);
		}

		private static void printCode(IEnumerable<CodeInstruction> codes, Collection<VariableDefinition> locals, string header)
		{
			StringBuilder sb = new StringBuilder(header);
			sb.Append("\nCode:\n");
			//cecilGen.DefineLabel();

			sb.AppendLine(".locals(");
			foreach (VariableDefinition local in locals)
			{
				sb.AppendLine(string.Format("\t{0,-3}:\t{1}", local.Index, local.VariableType));
			}
			sb.AppendLine(")\n======================");
			int i = 0;

			foreach (CodeInstruction code in codes)
			{
				string ln = code.labels.Count > 0 ? string.Format("lbl[{0}]", code.labels[0].GetHashCode())
												: i.ToString();
				i++;
				sb.AppendLine(string.Format("{0,-8}:{1,-10}\t{2}",
											ln,
											code.opcode,
											code.operand is SRE.Label ? $"lbl[{code.operand.GetHashCode()}]" :
											code.operand is SRE.LocalBuilder lb ? $"Local:{lb.LocalType} ({lb.LocalIndex})" :
											code.operand is string ? $"\"{code.operand}\"" :
											code.operand is MethodBase info ? info.FullDescription() :
											code.operand
											)
							);
			}

			Log(sb.ToString());

		}

		static void printHandlers(Collection<ExceptionHandler> exceptionHandlers, IList<ExceptionHandlingClause> exceptionClauses)
		{
			StringBuilder sb = new StringBuilder("\nException Handlers:\n");
			string fmt = "\t{0,15} {1,15} {2,15} {3,15} {4,15} {5,15} {6,15}\n";
			if (exceptionHandlers.Count > 0)
			{
				sb.AppendFormat(fmt, "TryStart",
								"TryEnd",
								"CatchType",
								"HandlerStart",
								"HandlerEnd",
								"HandlerType",
								"FilterStart");
				foreach (ExceptionHandler entry in exceptionHandlers)
				{
					sb.AppendFormat(fmt,
									entry.TryStart,
									entry.TryEnd,
									entry.CatchType,
									entry.HandlerStart,
									entry.HandlerEnd,
									entry.HandlerType,
									entry.FilterStart);
				}
			}
			if (exceptionClauses.Count > 0)
			{
				sb.AppendFormat(fmt,
								"TryOffset",
								"TryLength",
								"HandlerOffset",
								"HandlerLength",
								"CatchType",
								"FilterOffset",
								"Flags");
				foreach (var entry in exceptionClauses)
				{
					sb.AppendFormat(fmt,
									entry.TryOffset,
									entry.TryLength,
									entry.HandlerOffset,
									entry.HandlerLength,
									entry.CatchType,
									entry.FilterOffset,
									entry.Flags);

				}
			}
			Log(sb.ToString());

		}

		#endregion

		#region Config

		[HarmonyTargetMethod]
		static MethodBase GetTargetMethod()
		{

			if (Patch.Debug) Log("Finding Inner Type:");
		//	var inner = typeof(WorldManager).GetNestedTypes().First((type) => type.Name.StartsWith("<LoadGameData>"));
			var inner = AccessTools.FindIncludingInnerTypes(typeof(WorldManager), (type) =>
			{
				//type.Name.StartsWith("<LoadGameData>") ? type : null
				if (Patch.Debug) Log($"\tType:{type.Name}");
				return type.Name.StartsWith("<LoadGameData>") ? type : null;
			});
			if(Patch.Debug) Log($"Found type:{inner.Name}");
			var method = AccessTools.Method(inner, "MoveNext");
			if (Patch.Debug) Log($"\tMethod:{method.Name}");
			return method;
		}

		#endregion

		#region Transpilers

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> LoadGameDataTranspiler(IEnumerable<CodeInstruction> instructions, SRE.ILGenerator generator, MethodBase methodInfo)
		{
			var methodTry = methodInfo.GetMethodBody().ExceptionHandlingClauses;
			var locals = generator.GetGenVariables();
			var tryBlocks = generator.GetCecilGen().IL.Body.ExceptionHandlers;
			if (Patch.Debug)
			{
				printCode(instructions, locals, "Before Transpiler:");
				printHandlers(tryBlocks, methodTry);
			}
			bool fail = false;
			IEnumerable<CodeInstruction> codes;
			try
			{
				codes = XPiler(instructions, generator);
			}
			catch(Exception ex)
			{
				fail = true;
				LogError($"Error in Xpiler:\n{ex}");
				codes = null;
			}
			if (!fail && Patch.Debug)
			{
				printCode(codes, locals, "After Transpiler");
				printHandlers(tryBlocks, methodTry);
			}
			return fail ? instructions : codes;
		}

		static IEnumerable<CodeInstruction> XPiler(IEnumerable<CodeInstruction> instructions,SRE.ILGenerator generator)
		{
			var codes = instructions.ToList();
			var steamAppPath = AccessTools.PropertyGetter(typeof(GameManager), nameof(GameManager.SteamAppPath));
			//Find the ldarg.0 and call Assets.Scripts.GameManager.get_SteamAppPath()
			for(int index = 0;index < codes.Count;index++)
			{
				if(codes[index].opcode == SRE.OpCodes.Ldarg_0
					&& codes[index+1].opcode == SRE.OpCodes.Call
					&& steamAppPath.Equals(codes[index+1].operand))
				{
					//Call our fixed Mod loader
					codes[index].opcode = SRE.OpCodes.Call;
					codes[index++].operand = typeof(WorldManagerPatch).GetMethod(nameof(LoadMods));
					bool eol = false;
					while(!eol)
					{
						//remove the foreach loop
						if (codes[index].opcode == SRE.OpCodes.Endfinally)
							eol = true;
						codes.RemoveAt(index);
						//codes[index].opcode = SRE.OpCodes.Nop;
						//codes[index++].operand = null;
					}
					//remove the generated enumerator
					codes.RemoveAt(index);
					codes.RemoveAt(index);
					codes.RemoveAt(index);
					break;
				}
			}
			//var cecilGen = generator.GetCecilGen();
			//if (cecilGen.IL.Body.HasExceptionHandlers)
			//{
			//	var
			//}


			return codes;
		}
		#endregion

		#region Replacement Methods

		public static void LoadMods()
		{
			string installDir = GameManager.SteamAppPath;
			Settings.ValidateCharacterData();
			WorldManager.ModLoadLog = "";
			string gameDataDirectory = Path.Combine(Application.streamingAssetsPath, "Data").SanitizePath();
			string gameWorkshopDirectory = Path.Combine(installDir, "../../workshop/content/544550").SanitizePath();
			string localModDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games/Stationeers/mods").SanitizePath();
			foreach (ulong enabledMod in WorkshopMenu.CurrentMods.EnabledMods)
			{
				if (enabledMod == 1UL)
				{
					LoadDataFiles(gameDataDirectory);
				}
				else if (enabledMod > 10000UL)
				{
					LoadDataFiles(Path.Combine(gameWorkshopDirectory, ((uint)enabledMod).ToString(), "GameData"));
				}
				else if (WorkshopMenu.CurrentMods.LocalMods.Find((WorkshopMenu.LocalMod mod) => mod.modID == (uint)enabledMod) is WorkshopMenu.LocalMod localMod)
				{
					LoadDataFiles(Path.Combine(localModDirectory, localMod.folder, "GameData"));
				}
			}
			Localization.ProcessNewPages(Settings.CurrentData.LanguageCode);
		}

		private static void LoadDataFiles(string modPath)
		{
			if (Directory.Exists(modPath))
			{
				HashSet<string> filesToLoad = new HashSet<string>();
				filesToLoad.AddRange(Directory.GetFiles(modPath, "*.xml"));
				foreach (string path in Directory.GetDirectories(modPath))
				{
					if (Path.GetFileName(path) == "Language")
						Localization.GetLanguages(modPath+"/");
					else
						filesToLoad.AddRange(Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories));
				}
				XmlSerializer serializer = new XmlSerializer(typeof(WorldManager.GameData), XmlSaveLoad.ExtraTypes);
				foreach (string xmlFile in filesToLoad)
				{
					WorldManager.LoadXmlFileData(serializer, xmlFile);
				}
				WorldManager.ModLoadLog = WorldManager.ModLoadLog + "Loaded Mod Data from " + modPath + "\n";
			}

		}
		#endregion

	}
}
