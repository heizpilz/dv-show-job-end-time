using System;
using System.Collections.Generic;
using System.Reflection;
using DV.Booklets;
using DV.RenderTextureSystem.BookletRender;
using DV.Utils;
using DV.WeatherSystem;
using HarmonyLib;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityModManagerNet;

namespace ShowJobEndTime;

#if DEBUG
[EnableReloading]
#endif
public static class Main
{
	internal static Harmony? harmony = null;

	// Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager
	private static bool Load(UnityModManager.ModEntry modEntry)
	{
		try
		{
			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			// Other plugin startup logic
			modEntry.OnToggle = OnToggle;
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
			harmony?.UnpatchAll(modEntry.Info.Id);
			return false;
		}

		return true;
	}

	private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
	{
		if (value)
		{
			harmony?.PatchAll(Assembly.GetExecutingAssembly());
		}
		else
		{
			harmony?.UnpatchAll(modEntry.Info.Id);
		}
		return true;
	}

	public static string GetLocalizedAbbrMonthDay(DateTime dateTime)
	{
		string pattern = LocalizationManager.CurrentCulture.DateTimeFormat.MonthDayPattern;
		pattern = pattern.Replace("MMMM", "MMM");
		return dateTime.ToString(pattern, LocalizationManager.CurrentCulture);
	}


	[HarmonyPatch(typeof(BookletCreator_Job), "GetBookletTemplateData")]
	class ChangeBonusTimeToTimeOfDay
	{
		static string GetTimeBonusText(Job_data job)
		{
			WeatherPresetManager weatherPresetManager = SingletonBehaviour<WeatherDriver>.Instance.manager;
			float remainingRLSeconds = job.timeLimit - job.timeOnJob;
			double timeScalingFactor = 1440d / weatherPresetManager.DayLengthInMinutes;
			double remainingGameSeconds = remainingRLSeconds * timeScalingFactor;
			DateTime timeBonusEnd = weatherPresetManager.DateTime.AddSeconds(remainingGameSeconds);
			return timeBonusEnd.ToString("t") + "\n" + GetLocalizedAbbrMonthDay(timeBonusEnd);
		}

		public static List<TemplatePaperData> Postfix(List<TemplatePaperData> result, Job_data job)
		{
			if (job.timeLimit <= 0.0)
			{
				// do nothing if there is no bonus time set
				return result;
			}
			foreach (TemplatePaperData templatePaperData in result)
			{
				if (templatePaperData.GetTemplatePaperType() == TemplatePaperType.FrontPage)
				{
					FrontPageTemplatePaperData frontPageTemplatePaperData = (FrontPageTemplatePaperData)templatePaperData;
					frontPageTemplatePaperData.timeBonus = GetTimeBonusText(job);
				}
			}
			return result;
		}
	}

	[HarmonyPatch(typeof(JobBookletRender), "TemplatePaperDataFill")]
	class SmallerTimeBonusText
	{
		public static void Postfix(TemplatePaperData templateData, ref JobBookletRender __instance)
		{
			if (templateData.GetTemplatePaperType() == TemplatePaperType.FrontPage)
			{
				FrontPageTemplatePaper fpt = __instance.frontPageTemplate;
				fpt.timeBonus.fontSize = 45f;
				fpt.timeBonus.alignment = TextAlignmentOptions.Center;
				fpt.timeBonus.lineSpacing = -40f;
				fpt.timeBonus.margin = new Vector4(0, -42, 0, 0);
				fpt.timeBonus.fontWeight = FontWeight.Black;
			}
		}
	}

	[HarmonyPatch(typeof(WeatherForecastPoster), "OnForecastUpdated")]
	class LocalizeWeatherForcastPoster
	{
		public static void Postfix(WeatherForecastPoster __instance)
		{
			DateTime? lastForecast = __instance.forecaster.lastForecastTimestamp;
			if (lastForecast.HasValue)
			{
				__instance.dateTMPro.text = GetLocalizedAbbrMonthDay(lastForecast.Value);
			}
		}
	}
}
