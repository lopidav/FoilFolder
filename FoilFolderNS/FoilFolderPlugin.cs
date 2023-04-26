using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoilFolderNS;

[BepInPlugin("FoilFolder", "FoilFolder", "0.1.0")]
public class FoilFolderPlugin : BaseUnityPlugin
{
	public static ManualLogSource L;
	public static void Log(string s)
	{
		L.LogInfo((object)(DateTime.Now.ToString("HH:MM:ss") + ": " + s));
	}
	public static Harmony HarmonyInstance;
	public static RectTransform ffScreen;
	public static Transform ffButtonNewElement;
	public static Transform ffPauseButtonNewElement;
	public static List<string> PossibleFoilCards = new List<string>();
	public static List<string> FoundFoilCards = new List<string>();
	public static List<string> NewFoundFoilCards = new List<string>();
	public void Awake()
	{
		L = ((FoilFolderPlugin)this).Logger;

		try
		{
			HarmonyInstance = new Harmony("FoilFolderPlugin");
			HarmonyInstance.PatchAll(typeof(FoilFolderPlugin));
		}
		catch (Exception ex3)
		{
			Log("Patching failed: " + ex3.Message);
		}
		

	}
	public static RectTransform CreateFoilFolderScreen()
	{
		ffScreen = MonoBehaviour.Instantiate(GameCanvas.instance.CardopediaScreen, GameCanvas.instance.transform);
		ffScreen.name = "FoilFolderScreen";
		FieldInfo ScreensField = typeof(GameCanvas).GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic);
		List<RectTransform> screens = (List<RectTransform>)ScreensField.GetValue(GameCanvas.instance);
		screens.Add(ffScreen);
		ScreensField.SetValue(GameCanvas.instance, screens);

		FieldInfo ScreenPositionsField = typeof(GameCanvas).GetField("screenPositions", BindingFlags.Instance | BindingFlags.NonPublic);
		List<GameCanvas.ScreenPosition> screenPositions = (List<GameCanvas.ScreenPosition>)ScreenPositionsField.GetValue(GameCanvas.instance);
		screenPositions.Add(GameCanvas.ScreenPosition.Left);
		ScreenPositionsField.SetValue(GameCanvas.instance, screenPositions);
		
		FieldInfo ScreenInTransitionField = typeof(GameCanvas).GetField("screenInTransition", BindingFlags.Instance | BindingFlags.NonPublic);
		List<bool> screenInTransition = (List<bool>)ScreenInTransitionField.GetValue(GameCanvas.instance);
		screenInTransition.Add(false);
		ScreenInTransitionField.SetValue(GameCanvas.instance, screenInTransition);
		
		ffScreen.gameObject.AddComponent<FoilFolderScreen>();
		FoilFolderScreen ffScreenData = ffScreen.GetComponent<FoilFolderScreen>();
		CardopediaScreen cpScreenData = ffScreen.GetComponent<CardopediaScreen>();
		ffScreenData.CopyDataFrom(cpScreenData);
		Destroy(ffScreen.GetComponent<CardopediaScreen>());
		

		return ffScreen;
	}
	public static CustomButton CreateButton(Transform parent, string text, RectTransform screen)
	{
		CustomButton button = MonoBehaviour.Instantiate(PrefabManager.instance.ButtonPrefab, parent);
		MonoBehaviour.Destroy(button.transform.GetChild(0).GetComponent<LocSetter>());
		button.GetComponentInChildren<TextMeshProUGUI>().text = text;
		button.Clicked += (() => { GameCanvas.instance.SetScreen(screen); });
		return button;
	}
	
	[HarmonyPatch(typeof(GameCanvas), "Awake")]
	[HarmonyPostfix]
	public static void GameCanvas_Awake_Postfix(GameCanvas __instance)
	{
		CreateFoilFolderScreen();
		CustomButton ffMenuButton;
		ffMenuButton = CreateButton(__instance.MainMenuScreen.Find("Background").Find("Buttons"), "Foil Folder", ffScreen);// MonoBehaviour.Instantiate(__instance.MainMenuScreen.Find("Background").Find("Buttons").Find("Cardopedia").GetComponent<CustomButton>(), __instance.MainMenuScreen.Find("Background").Find("Buttons"));
		ffMenuButton.name = "FoilFolder";
		HorizontalLayoutGroup layout = ffMenuButton.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
		layout.childForceExpandWidth = false;
		layout.spacing = 16f;
		// MonoBehaviour.Destroy(ffMenuButton.transform.GetChild(0).GetComponent<LocSetter>());
		// ffMenuButton.Clicked += (() => { GameCanvas.instance.SetScreen(ffScreen); });
		ffMenuButton.transform.SetSiblingIndex(__instance.MainMenuScreen.Find("Background").Find("Buttons").Find("Cardopedia").GetSiblingIndex());
		// ffMenuButton.GetComponentInChildren<TextMeshProUGUI>().text = "Foil folder";
		ffButtonNewElement = MonoBehaviour.Instantiate(__instance.MainMenuScreen.Find("Background").Find("Buttons").Find("Cardopedia").Find("New"), ffMenuButton.transform);
		ffButtonNewElement.name = "New";

		var ffPauseMenuButton = MonoBehaviour.Instantiate(ffMenuButton, GameCanvas.instance.PauseScreen.Find("Background").Find("Buttons"));
		ffPauseMenuButton.transform.SetSiblingIndex(GameCanvas.instance.PauseScreen.Find("Background").Find("Buttons").Find("Cardopedia").GetSiblingIndex());
		ffPauseMenuButton.Clicked += (() => { GameCanvas.instance.SetScreen(ffScreen); });
		ffPauseButtonNewElement = ffPauseMenuButton.transform.Find("New");
	}

	[HarmonyPatch(typeof(WorldManager), "Awake")]
	[HarmonyPostfix]
	public static void WorldManager_Awake_Postfix(WorldManager __instance)
	{
		CalculatePossibleFoilCardsFromPackPrefubs();
		if (!PossibleFoilCards.Contains("villager")) PossibleFoilCards.Add("villager");
	}

	public static void CalculatePossibleFoilCardsFromPackPrefubs()
	{
		foreach (var booster in WorldManager.instance.BoosterPackPrefabs)
		{
			foreach (CardBag cardBag in booster.CardBags)
			{
				if (cardBag.CardBagType == CardBagType.SetPack) continue;
				if (cardBag.CardBagType == CardBagType.Chances)
				{
					foreach (CardChance cardChance in cardBag.Chances)
					{
						if (!PossibleFoilCards.Contains(cardChance.Id)) PossibleFoilCards.Add(cardChance.Id);
					}
				}
				else if (cardBag.CardBagType == CardBagType.SetCardBag)
				{
					List<CardChance> chances = ((!cardBag.UseFallbackBag) ? CardBag.GetChancesForSetCardBag(WorldManager.instance.GameDataLoader, cardBag.SetCardBag) : CardBag.GetChancesForSetCardBag(WorldManager.instance.GameDataLoader, cardBag.SetCardBag, cardBag.FallbackBag));
					foreach (CardChance cardChance in chances)
					{
						if (!PossibleFoilCards.Contains(cardChance.Id)) PossibleFoilCards.Add(cardChance.Id);
					}
				}
				else if (cardBag.CardBagType == CardBagType.Enemies)
					foreach (Combatable c in SpawnHelper.GetEnemyPoolFromCardbags(WorldManager.instance.GameDataLoader.GetSetCardBagForEnemyCardBag(cardBag.EnemyCardBag).AsList(), true))
					{
						if (!PossibleFoilCards.Contains(c.Id)) PossibleFoilCards.Add(c.Id);
					}
				
			}
		}
	}
	
	
	[HarmonyPatch(typeof(SelectSaveScreen), "SetSave")]
	[HarmonyPostfix]
	public static void SelectSaveScreen_SetSaved_Postfix()
	{
		FoundFoilCards.Clear();
		NewFoundFoilCards.Clear();
		refreshFoundFoilsSave();
	}
	[HarmonyPatch(typeof(WorldManager), "Load")]
	[HarmonyPostfix]
	public static void WorldManager_Load_Postfix()
	{
		FoundFoilCards.Clear();
		NewFoundFoilCards.Clear();
		refreshFoundFoilsSave();
		newFoundFoilsLoad();
	}
	[HarmonyPatch(typeof(CardData), "SetFoil")]
	[HarmonyPostfix]
	public static void CardData_SetFoil_Postfix(CardData __instance)
	{
		if (__instance.MyGameCard != null && __instance.MyGameCard.IsDemoCard) return;
		if (!FoundFoilCards.Contains(__instance.Id))
		{
			//Log("foilFound");

			FoundFoilCards.Add(__instance.Id);
			NewFoundFoilCards.Add(__instance.Id);
			newFoundFoilsSave();
			refreshFoundFoilsSave();
		}
		if (!PossibleFoilCards.Contains(__instance.Id))
		{
			PossibleFoilCards.Add(__instance.Id);
		}
	}
	[HarmonyPatch(typeof(GameCard), "Update")]
	[HarmonyPostfix]
	public static void GameCard_Update_Postfix(GameCard __instance)
	{
		if (!__instance.IsDemoCard) return;
		if (!GameCanvas.instance.ScreenIsInteractable(ffScreen)) return;
		ParticleSystem.EmissionModule emission = __instance.FoilParticles.emission;
		emission.enabled = __instance.CardData.IsFoil;
		if (emission.enabled)
		{
			ParticleSystem.MainModule mainFoil = __instance.FoilParticles.main;
			mainFoil.startSizeMultiplier = 0.1f; 
		} 
	}
	public static void refreshFoundFoilsSave()
	{
		string foundFoilsString = SerializedKeyValuePairHelper.GetWithKey(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "FoundFoilCards")?.Value;
		var FoundFoilsSave = (foundFoilsString ?? "").Split(',').ToList();
		foreach (string card in FoundFoilsSave)
		{
			if (card != "" && !FoundFoilCards.Contains(card))
			{
				if (!PossibleFoilCards.Contains(card)) PossibleFoilCards.Add(card);
				FoundFoilCards.Add(card);
			}
		}
		FoundFoilCards.RemoveAll(x=>x=="");
		FoundFoilCards = FoundFoilCards.Distinct().ToList();
		foundFoilsString = FoundFoilCards.Join(delimiter:",");
		SerializedKeyValuePairHelper.SetOrAdd(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "FoundFoilCards", foundFoilsString);

	}
	public static void newFoundFoilsSave()
	{
		NewFoundFoilCards.RemoveAll(x=>x=="");
		NewFoundFoilCards = NewFoundFoilCards.Distinct().ToList();
		var newFoundFoilsString = NewFoundFoilCards.Join(delimiter:",");
		SerializedKeyValuePairHelper.SetOrAdd(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "NewFoundFoilCards", newFoundFoilsString);
	}
	public static void newFoundFoilsLoad()
	{
		string newFoundFoilsString = SerializedKeyValuePairHelper.GetWithKey(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "NewFoundFoilCards")?.Value;
		NewFoundFoilCards = (newFoundFoilsString ?? "").Split(',').ToList();
		NewFoundFoilCards.RemoveAll(x=>x=="");
	}
	[HarmonyPatch(typeof(CardopediaEntryElement), "Update")]
	[HarmonyPrefix]
	public static void CardopediaEntryElement_Update_Prefix(CardopediaEntryElement __instance, ref bool ___wasHoveredAndNew)
	{
		if (___wasHoveredAndNew && GameCanvas.instance.ScreenIsInteractable(ffScreen))
		{
			NewFoundFoilCards.Remove(__instance.MyCardData.Id);
			newFoundFoilsSave();
		}
	}
	[HarmonyPatch(typeof(MainMenu), "Update")]
	[HarmonyPostfix]
	public static void MainMenu_Update_Postfix()
	{
		PerformanceHelper.SetActive(ffButtonNewElement.gameObject, NewFoundFoilCards.Count > 0);
	}
	[HarmonyPatch(typeof(PauseScreen), "Update")]
	[HarmonyPostfix]
	public static void PauseScreen_Update_Postfix()
	{
		PerformanceHelper.SetActive(ffPauseButtonNewElement.gameObject, NewFoundFoilCards.Count > 0);
	}

}
