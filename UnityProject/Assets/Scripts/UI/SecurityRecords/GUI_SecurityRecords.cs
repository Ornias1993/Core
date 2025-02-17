﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GUI_SecurityRecords : NetTab
{
	[SerializeField]
	private NetPageSwitcher nestedSwitcher;
	[SerializeField]
	private GUI_SecurityRecordsEntriesPage entriesPage;
	[SerializeField]
	private GUI_SecurityRecordsEntryPage entryPage;
	[SerializeField]
	private NetLabel idText;
	private SecurityRecordsConsole console;

	public override void OnEnable()
	{
		base.OnEnable();
		if (CustomNetworkManager.Instance._isServer)
		{
			StartCoroutine(WaitForProvider());
		}
	}

	IEnumerator WaitForProvider()
	{
		while (Provider == null)
		{
			yield return WaitFor.EndOfFrame;
		}

		console = Provider.GetComponentInChildren<SecurityRecordsConsole>();
		console.OnConsoleUpdate.AddListener(UpdateScreen);
		UpdateScreen();
	}

	public void UpdateScreen()
	{
		if (nestedSwitcher.CurrentPage == entriesPage)
		{
			entriesPage.OnOpen(this);
		}
		else if (nestedSwitcher.CurrentPage == entryPage)
		{
			entryPage.UpdateEntry();
		}
		else
		{
			UpdateIdText(idText);
		}
	}

	public void RemoveId()
	{
		if (console.IdCard)
		{
			console.RemoveID();
			UpdateScreen();
		}
	}

	public void UpdateIdText(NetLabel labelToSet)
	{
		var IdCard = console.IdCard;
		if (IdCard)
		{
			labelToSet.SetValue = $"{IdCard.RegisteredName}, {IdCard.GetJobType.ToString()}";
		}
		else
		{
			labelToSet.SetValue = "********";
		}
	}

	public void LogIn()
	{
		if (console.IdCard == null || !console.IdCard.accessSyncList.Contains((int) Access.security))
		{
			return;
		}

		OpenRecords();
	}

	public void LogOut()
	{
		nestedSwitcher.SetActivePage(nestedSwitcher.DefaultPage);
		UpdateIdText(idText);
	}

	public void OpenRecords()
	{
		nestedSwitcher.SetActivePage(entriesPage);
		entriesPage.OnOpen(this);
	}

	public void OpenRecord(SecurityRecord recordToOpen)
	{
		nestedSwitcher.SetActivePage(entryPage);
		entryPage.OnOpen(recordToOpen, this);
	}

	public void CloseTab()
	{
		ControlTabs.CloseTab(Type, Provider);
	}
}

public enum SecurityStatus
{
	None,
	Arrest,
	Parole
}

[System.Serializable]
public class SecurityRecord
{
	public string EntryName;
	public string ID;
	public string Sex;
	public string Age;
	public string Species;
	public string Rank;
	public string Fingerprints;
	public SecurityStatus Status;
	public List<SecurityRecordCrime> Crimes;
	public JobOutfit jobOutfit;
	public CharacterSettings characterSettings;

	public SecurityRecord()
	{
		EntryName = "NewEntry";
		ID = "-";
		Sex = "-";
		Age = "99";
		Species = "Human";
		Rank = "Visitor";
		Fingerprints = "-";
		Status = SecurityStatus.None;
		Crimes = new List<SecurityRecordCrime>();
	}
}