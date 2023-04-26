// GameScripts, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// FoilFolderScreen
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;

namespace FoilFolderNS;
public class FoilFolderScreen : MonoBehaviour
{
	public RectTransform EntriesParent;

	public CustomButton BackButton;

	public TMP_InputField SearchField;

	public GameObject LabelPrefab;

	public CardopediaEntryElement CardopediaEntryPrefab;

	public CardopediaEntryElement HoveredEntry;

	public TextMeshProUGUI CardFoundAmount;

	public TextMeshProUGUI CardDescription;

	public Transform TargetCardPos;

	public Transform CardopediaBackground;

	public List<CardopediaEntryElement> entries = new List<CardopediaEntryElement>();

	public List<ExpandableLabel> labels = new List<ExpandableLabel>();

	public CardopediaEntryElement lastHoveredEntry;

	public GameCard demoCard;

	public static FoilFolderScreen instance;

	public bool UnlockAllInEditor;

	public int totalFoundCount;

	public void Awake()
	{
		//CopyDataFrom(FoilFolderPlugin.ffScreen.GetComponent<CardopediaScreen>());
		instance = this;
		BackButton.Clicked += delegate
		{
			CardopediaBackground.gameObject.SetActive(value: false);
			ClearScreen();
			if (WorldManager.instance.CurrentGameState == WorldManager.GameState.InMenu)
			{
				GameCanvas.instance.SetScreen(GameCanvas.instance.MainMenuScreen);
			}
			else
			{
				GameCanvas.instance.SetScreen(GameCanvas.instance.PauseScreen);
			}
		};
		SearchField.onValueChanged.AddListener(delegate(string value)
		{
			FilterEntries();
			foreach (ExpandableLabel label in labels)
			{
				if (GetActiveLabelChildrenCount(label) > 0 && !string.IsNullOrEmpty(value))
				{
					label.IsExpanded = true;
					label.ShowChildrenCardopedia();
				}
			}
		});
		transform.Find("Background").Find("Title").gameObject.GetComponent<TMPro.TextMeshProUGUI>().text = "Foil Folder";
		CardopediaBackground.gameObject.SetActive(value: false);
	}

	public void OnEnable()
	{
		CreateEntries();
		CardDescription.transform.parent.gameObject.SetActive(value: false);
		CardopediaBackground.gameObject.SetActive(value: true);
		totalFoundCount = DetermineFoundCount();
	}

	public int DetermineFoundCount()
	{
		return FoilFolderPlugin.FoundFoilCards.Count;
	}

	public void FilterEntries()
	{
		string text = "";
		if (!string.IsNullOrEmpty(SearchField.text))
		{
			text = SearchField.text;
			foreach (CardopediaEntryElement entry in entries)
			{
				if (entry.MyCardData.Name.ToLower().Replace(" ", "").Contains(text.ToLower().Replace(" ", "")))
				{
					entry.isFiltered = true;
				}
				else
				{
					entry.isFiltered = false;
				}
			}
		}
		else
		{
			foreach (CardopediaEntryElement entry2 in entries)
			{
				entry2.isFiltered = true;
			}
		}
		foreach (ExpandableLabel label in labels)
		{
			//label.ShowChildrenCardopedia();
			foreach (GameObject child in label.Children)
			{
				CardopediaEntryElement component = child.GetComponent<CardopediaEntryElement>();
				if (component != null)
				{
					child.SetActive(component.isFiltered && label.IsExpanded);
				}
			}
			CardType type = label.Children[0].GetComponent<CardopediaEntryElement>().MyCardData.MyCardType;
			int num = FoilFolderPlugin.PossibleFoilCards.Count((string x) => WorldManager.instance.GameDataLoader.GetCardFromId(x, false)?.MyCardType == type);
			label.SetText(CardTypeToText(type) + $" ({entries.Count(entrie => entrie.MyCardData.MyCardType == type && WasFoilFound(entrie))}/{num})");
			label.gameObject.SetActive(value: true);
		}
	}

	public int GetActiveLabelChildrenCount(ExpandableLabel label)
	{
		return label.Children.Count((GameObject x) => x.GetComponent<CardopediaEntryElement>() != null && x.GetComponent<CardopediaEntryElement>().isFiltered && x.GetComponent<CardopediaEntryElement>().wasFound);
	}

	public void UpdateEntries()
	{
		FilterEntries();
	}

	public void CreateEntries()
	{
		List<CardData> cardDataPrefabs = WorldManager.instance.CardDataPrefabs;
		cardDataPrefabs = (from x in cardDataPrefabs
			orderby x.MyCardType, x.FullName
			select x).ToList();
		cardDataPrefabs.RemoveAll((CardData x) => !FoilFolderPlugin.PossibleFoilCards.Contains(x.Id));
		new List<Transform>();
		foreach (Transform item in EntriesParent)
		{
            UnityEngine.Object.Destroy(item.gameObject);
		}
		ExpandableLabel expandableLabel = null;
		labels = new List<ExpandableLabel>();
		entries.Clear();
		for (int j = 0; j < cardDataPrefabs.Count; j++)
		{
			CardData c = cardDataPrefabs[j];
			bool flag = true;
			if (j == 0 || cardDataPrefabs[j - 1].MyCardType != cardDataPrefabs[j].MyCardType)
			{
				ExpandableLabel component = UnityEngine.Object.Instantiate(LabelPrefab).GetComponent<ExpandableLabel>();
				component.transform.SetParentClean(EntriesParent);
				int num = cardDataPrefabs.Count((CardData x) => x.MyCardType == c.MyCardType);
				int num2 = cardDataPrefabs.Count((CardData x) => x.MyCardType == c.MyCardType && FoilFolderPlugin.FoundFoilCards.Contains(x.Id));
				component.SetText(CardTypeToText(cardDataPrefabs[j].MyCardType) + $" ({num2}/{num})");
				component.Tag = cardDataPrefabs[j].MyCardType;
				component.SetCallback(UpdateEntries);
				labels.Add(component);
				if (flag)
				{
					component.SetExpanded(expanded: false);
				}
				expandableLabel = component;
			}
			CardopediaEntryElement cardopediaEntryElement = UnityEngine.Object.Instantiate(CardopediaEntryPrefab);
			expandableLabel.Children.Add(cardopediaEntryElement.gameObject);
			if (flag)
			{
				cardopediaEntryElement.gameObject.SetActive(value: false);
			}
			cardopediaEntryElement.transform.SetParentClean(EntriesParent);
			cardopediaEntryElement.SetCardData(c);
			cardopediaEntryElement.isFiltered = false;
			cardopediaEntryElement.UndiscoveredCards = false;
			cardopediaEntryElement.UndiscoveredTransform.gameObject.SetActive(false);
			cardopediaEntryElement.isNew = FoilFolderPlugin.NewFoundFoilCards.Contains(c.Id);
			cardopediaEntryElement.NewTransform.gameObject.SetActive(false);
			if (!WasFoilFound(cardopediaEntryElement)) cardopediaEntryElement.Button.TextMeshPro.text = "<color=#d9d7db>" + cardopediaEntryElement.Button.TextMeshPro.text + "</color>";
			entries.Add(cardopediaEntryElement);
		}
		foreach (ExpandableLabel i in labels)
		{
			if (entries.Any((CardopediaEntryElement e) => e.isNew && e.MyCardData.MyCardType == (CardType)i.Tag))
			{
				i.SetExpanded(expanded: true);
			}
		}
	}

	public string CardTypeToText(CardType type)
	{
		return type.TranslateEnum();
	}

	public void OnDisable()
	{
		SearchField.text = string.Empty;
		ClearScreen();
	}

	public void ClearScreen()
	{
		if (demoCard != null)
		{
            UnityEngine.Object.Destroy(demoCard.gameObject);
		}
		CardDescription.transform.parent.gameObject.SetActive(value: false);
		lastHoveredEntry = null;
		CardopediaBackground.gameObject.SetActive(value: false);
	}

	public void Update()
	{
		HoveredEntry = null;
		if (GameCanvas.instance.ScreenIsInteractable(FoilFolderPlugin.ffScreen))
		{
			foreach (CardopediaEntryElement entry in entries)
			{
				if (entry.Button.IsHovered || entry.Button.IsSelected)
				{
					HoveredEntry = entry;
				}
			}
		}
		if (lastHoveredEntry != HoveredEntry && HoveredEntry != null)
		{
			if (demoCard != null)
			{
                UnityEngine.Object.Destroy(demoCard.gameObject);
			}
			demoCard = UnityEngine.Object.Instantiate(PrefabManager.instance.GameCardPrefab);
			CardData cardData = UnityEngine.Object.Instantiate(HoveredEntry.MyCardData);
			cardData.transform.SetParent(demoCard.transform);
			demoCard.CardData = cardData;
			cardData.MyGameCard = demoCard;
			demoCard.FaceUp = HoveredEntry.wasFound && WasFoilFound(HoveredEntry);
			demoCard.IsDemoCard = true;
			cardData.SetFoil();
			demoCard.SetDemoCardRotation();
			cardData.UpdateCard();
			demoCard.ForceUpdate();
		}
		if (demoCard != null)
		{
			Vector3 position = TargetCardPos.position;
			demoCard.transform.position = (demoCard.TargetPosition = position);
			ParticleSystem.EmissionModule emission = demoCard.FoilParticles.emission;
			emission.enabled = true;
		}
		if (HoveredEntry != null)
		{
			CardDescription.transform.parent.gameObject.SetActive(value: true);
			if (WasFoilFound(HoveredEntry))
			{
				demoCard.CardData.UpdateCardText();
				string description = demoCard.CardData.Description;
				description = description.Replace("\\d", "\n\n");
				if (HoveredEntry.MyCardData is Combatable combatable)
				{
					description += combatable.GetCombatableDescriptionAdvanced();
				}
				if (HoveredEntry.MyCardData is Blueprint blueprint)
				{
					description = blueprint.GetText();
				}
				CardDescription.text = description;
			}
			else
			{
				CardDescription.text = SokLoc.Translate("label_card_not_found");
			}
		}
		CardFoundAmount.text = SokLoc.Translate("label_cards_found",
			LocParam.Create("found", FoilFolderPlugin.FoundFoilCards.Count.ToString()),
			LocParam.Create("total", FoilFolderPlugin.PossibleFoilCards.Count.ToString()));
		lastHoveredEntry = HoveredEntry;
	}

	public string GetDropSummaryFromCard(CardData cardData)
	{
		if (cardData is Harvestable)
		{
			return Boosterpack.GetSummaryFromAllCards(cardData.GetPossibleDrops(), "label_can_drop");
		}
		if (cardData is Enemy)
		{
			return Boosterpack.GetSummaryFromAllCards(cardData.GetPossibleDrops(), "label_can_drop");
		}
		return "";
	}
	public void CopyDataFrom(CardopediaScreen cpScreenData)
	{
		this.EntriesParent = cpScreenData.EntriesParent;

		this.BackButton = cpScreenData.BackButton;
		
		this.SearchField = cpScreenData.SearchField;

		this.LabelPrefab = cpScreenData.LabelPrefab;

		this.CardopediaEntryPrefab = cpScreenData.CardopediaEntryPrefab;

		this.HoveredEntry = cpScreenData.HoveredEntry;

		this.CardFoundAmount = cpScreenData.CardFoundAmount;

		this.CardDescription = cpScreenData.CardDescription;

		this.TargetCardPos = cpScreenData.TargetCardPos;

		this.CardopediaBackground = cpScreenData.CardopediaBackground;

		//this.entries = cpScreenData.entries;

		//this.labels = cpScreenData.labels;

		//this.lastHoveredEntry = cpScreenData.lastHoveredEntry;

		//this.demoCard = cpScreenData.demoCard;

		//this.instance = cpScreenData.instance;

		this.UnlockAllInEditor = cpScreenData.UnlockAllInEditor;

		//this.totalFoundCount = cpScreenData.totalFoundCount;

	}
	public static void CopyProperties(CardopediaScreen source, object destination)
    {
        if (source == null)
            throw new Exception("Source Object is null");
		if ( destination == null)
            throw new Exception("Destination Object is null");
        Type typeDest = destination.GetType();
        Type typeSrc = source.GetType();
        var results = from srcProp in typeSrc.GetProperties()
                                    let targetProperty = typeDest.GetProperty(srcProp.Name)
                                    where srcProp.CanRead
                                    && targetProperty != null
                                    && (targetProperty.GetSetMethod(true) != null)
                                    && (targetProperty.GetSetMethod().Attributes & MethodAttributes.Static) == 0
                                    && targetProperty.PropertyType.IsAssignableFrom(srcProp.PropertyType)
                                    select new { sourceProperty = srcProp, targetProperty = targetProperty };
        //map the properties
        foreach (var props in results)
        {
            props.targetProperty.SetValue(destination, props.sourceProperty.GetValue(source, null), null);
        }
    }

	public static bool WasFoilFound(CardopediaEntryElement element)
	{
		return FoilFolderPlugin.FoundFoilCards.Contains(element.MyCardData.Id);
	}
}
