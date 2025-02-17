﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Objects;
using UnityEngine;
using UnityEngine.Networking;


public class Equipment : NetworkBehaviour
{
	public ClothingItem[] clothingSlots;
	public bool IsInternalsEnabled;

	private PlayerNetworkActions playerNetworkActions;
	private PlayerScript playerScript;
	private List<InventorySlot> playerInventory;
	private InventorySlot[] gasSlots;
	private InventorySlot maskSlot;
	private GameObject idPrefab;

	public NetworkIdentity networkIdentity { get; set; }

	private void Awake()
	{
		networkIdentity = GetComponent<NetworkIdentity>();
		playerNetworkActions = gameObject.GetComponent<PlayerNetworkActions>();
		playerScript = gameObject.GetComponent<PlayerScript>();
		playerInventory = InventoryManager.AllClientInventorySlots;

		gasSlots = InitPotentialGasSlots();
		maskSlot = playerInventory.Find(s => s.SlotName == "mask");
		idPrefab = Resources.Load<GameObject>("Prefabs/Items/IDs/ID");
	}

	private InventorySlot[] InitPotentialGasSlots()
	{
		var slots = new List<InventorySlot>();
		foreach (var slot in playerInventory)
		{
			if (GasContainer.GasSlots.Contains(slot.SlotName))
			{
				slots.Add(slot);
			}
		}

		return slots.ToArray();
	}

	public override void OnStartClient()
	{
		InitInternals();
	}

	private void OnDestroy()
	{
		UnregisisterInternals();
	}

	public void NotifyPlayer(GameObject recipient)
	{
		for (int i = 0; i < clothingSlots.Length; i++)
		{
			var clothItem = clothingSlots[i];
			EquipmentSpritesMessage.SendTo(gameObject, i, clothItem.reference, recipient);
		}
	}

	public void SetPlayerLoadOuts()
	{
		JobOutfit standardOutfit = GameManager.Instance.StandardOutfit.GetComponent<JobOutfit>();
		JobOutfit jobOutfit = GameManager.Instance.GetOccupationOutfit(playerScript.mind.jobType);

		Dictionary<string, string> gear = new Dictionary<string, string>();

		gear.Add("uniform", standardOutfit.uniform);
		gear.Add("ears", standardOutfit.ears);
		gear.Add("belt", standardOutfit.belt);
		gear.Add("back", standardOutfit.backpack);
		gear.Add("shoes", standardOutfit.shoes);
		gear.Add("glasses", standardOutfit.glasses);
		gear.Add("gloves", standardOutfit.gloves);
		gear.Add("suit", standardOutfit.suit);
		gear.Add("head", standardOutfit.head);
		//gear.Add("accessory", standardOutfit.accessory);
		gear.Add("mask", standardOutfit.mask);
		//gear.Add("backpack", standardOutfit.backpack);
		//gear.Add("satchel", standardOutfit.satchel);
		//gear.Add("duffelbag", standardOutfit.duffelbag);
		//gear.Add("box", standardOutfit.box);
		//gear.Add("l_hand", standardOutfit.l_hand);
		//gear.Add("l_pocket", standardOutfit.l_pocket);
		//gear.Add("r_pocket", standardOutfit.r_pocket);
		//gear.Add("suit_store", standardOutfit.suit_store);

		if (!string.IsNullOrEmpty(jobOutfit.uniform))
		{
			gear["uniform"] = jobOutfit.uniform;
		}
		/*if (!String.IsNullOrEmpty(jobOutfit.id))
			gear["id"] = jobOutfit.id;*/
		if (!string.IsNullOrEmpty(jobOutfit.ears))
		{
			gear["ears"] = jobOutfit.ears;
		}
		if (!string.IsNullOrEmpty(jobOutfit.belt))
		{
			gear["belt"] = jobOutfit.belt;
		}
		if (!string.IsNullOrEmpty(jobOutfit.backpack))
		{
			gear["back"] = jobOutfit.backpack;
		}
		if (!string.IsNullOrEmpty(jobOutfit.shoes))
		{
			gear["shoes"] = jobOutfit.shoes;
		}
		if (!string.IsNullOrEmpty(jobOutfit.glasses))
		{
			gear["glasses"] = jobOutfit.glasses;
		}
		if (!string.IsNullOrEmpty(jobOutfit.gloves))
		{
			gear["gloves"] = jobOutfit.gloves;
		}
		if (!string.IsNullOrEmpty(jobOutfit.suit))
		{
			gear["suit"] = jobOutfit.suit;
		}
		if (!string.IsNullOrEmpty(jobOutfit.head))
		{
			gear["head"] = jobOutfit.head;
		}
		/*if (!String.IsNullOrEmpty(jobOutfit.accessory))
			gear["accessory"] = jobOutfit.accessory;*/
		if (!string.IsNullOrEmpty(jobOutfit.mask))
		{
			gear["mask"] = jobOutfit.mask;
		}
		/*if (!String.IsNullOrEmpty(jobOutfit.backpack))
			gear["backpack"] = jobOutfit.backpack;
		if (!String.IsNullOrEmpty(jobOutfit.satchel))
			gear["satchel"] = jobOutfit.satchel;
		if (!String.IsNullOrEmpty(jobOutfit.duffelbag))
			gear["duffelbag"] = jobOutfit.duffelbag;
		if (!String.IsNullOrEmpty(jobOutfit.box))
			gear["box"] = jobOutfit.box;
		if (!String.IsNullOrEmpty(jobOutfit.l_hand))
			gear["l_hand"] = jobOutfit.l_hand;
		if (!String.IsNullOrEmpty(jobOutfit.l_pocket))
			gear["l_pocket"] = jobOutfit.l_pocket;
		if (!String.IsNullOrEmpty(jobOutfit.r_pocket))
			gear["r_pocket"] = jobOutfit.r_pocket;
		if (!String.IsNullOrEmpty(jobOutfit.suit_store))
			gear["suit_store"] = jobOutfit.suit_store;*/

		foreach (KeyValuePair<string, string> gearItem in gear)
		{
			if (gearItem.Value.Contains(UniItemUtils.ClothingHierIdentifier) || gearItem.Value.Contains(UniItemUtils.HeadsetHierIdentifier) ||
			gearItem.Value.Contains(UniItemUtils.BackPackHierIdentifier) || gearItem.Value.Contains(UniItemUtils.BagHierIdentifier))
			{
				GameObject obj = ClothFactory.CreateCloth(gearItem.Value, TransformState.HiddenPos, transform.parent);
				//if ClothFactory does not return an object then move on to the next clothing item
				if (!obj)
				{
					Logger.LogWarning("Trying to instantiate clothing item " + gearItem.Value + " failed!", Category.Equipment);
					continue;
				}
				ItemAttributes itemAtts = obj.GetComponent<ItemAttributes>();
				SetItem(GetLoadOutEventName(gearItem.Key), itemAtts.gameObject);
			}
			else if (!string.IsNullOrEmpty(gearItem.Value))
			{
				//					Logger.Log(gearItem.Value + " creation not implemented yet.");
			}
		}
		SpawnID(jobOutfit);

		if (playerScript.mind.jobType == JobType.SYNDICATE)
		{
			//Check to see if there is a nuke and communicate the nuke code:
			Nuke nuke = FindObjectOfType<Nuke>();
			if (nuke != null)
			{
				UpdateChatMessage.Send(gameObject, ChatChannel.Syndicate,
													"We have intercepted the code for the nuclear weapon: " + nuke.NukeCode);
			}
		}
	}

	private void SpawnID(JobOutfit outFit)
	{

		var realName = GetComponent<PlayerScript>().playerName;
		GameObject idObj = PoolManager.PoolNetworkInstantiate(idPrefab, parent: transform.parent);
		if (outFit.jobType == JobType.CAPTAIN)
		{
			idObj.GetComponent<IDCard>().Initialize(IDCardType.captain, outFit.jobType, outFit.allowedAccess, realName);
		}
		else if (outFit.jobType == JobType.HOP || outFit.jobType == JobType.HOS || outFit.jobType == JobType.CMO || outFit.jobType == JobType.RD ||
				 outFit.jobType == JobType.CHIEF_ENGINEER)
		{
			idObj.GetComponent<IDCard>().Initialize(IDCardType.command, outFit.jobType, outFit.allowedAccess, realName);
		}
		else
		{
			idObj.GetComponent<IDCard>().Initialize(IDCardType.standard, outFit.jobType, outFit.allowedAccess, realName);
		}

		SetItem("id", idObj);
	}

	// //Hand item sprites after picking up an item (server)
	// public void SetHandItem(string slotName, GameObject obj)
	// {
	// 	ItemAttributes att = obj.GetComponent<ItemAttributes>();
	// 	SetHandItemSprite(att);
	// 	RpcSendMessage(slotName, obj);
	// }

	// [ClientRpc]
	// private void RpcSendMessage(string eventName, GameObject obj)
	// {
	// 	obj.BroadcastMessage("OnAddToInventory", eventName, SendMessageOptions.DontRequireReceiver);
	// }

	public string GetLoadOutEventName(string uniformPosition)
	{
		switch (uniformPosition)
		{
			case "glasses":
				return "eyes";
			case "head":
				return "head";
			case "neck":
				return "neck";
			case "mask":
				return "mask";
			case "ears":
				return "ear";
			case "suit":
				return "suit";
			case "uniform":
				return "uniform";
			case "gloves":
				return "hands";
			case "shoes":
				return "feet";
			case "belt":
				return "belt";
			case "back":
				return "back";
			case "id":
				return "id";
			case "l_pocket":
				return "storage02";
			case "r_pocket":
				return "storage01";
			case "l_hand":
				return "leftHand";
			case "r_hand":
				return "rightHand";
			default:
				Logger.LogWarning("GetLoadOutEventName: Unknown uniformPosition:" + uniformPosition, Category.Equipment);
				return null;
		}
	}

	//To set the actual sprite on the player obj
	public void SetHandItemSprite(ItemAttributes att, string hand)
	{
		EquipSlot enumA = (EquipSlot)Enum.Parse(typeof(EquipSlot), hand);
		if (hand == "leftHand")
		{
			SetReference((int)enumA, att.NetworkInHandRefLeft());
		}
		else
		{
			SetReference((int)enumA, att.NetworkInHandRefRight());
		}
	}

	public void SetReference(int index, int reference)
	{
		EquipmentSpritesMessage.SendToAll(gameObject, index, reference);
	}

	//
	/// <summary>
	///  Clear any sprite slot by setting the slot to -1 via the slotName (server). If the
	///  specified slot has no associated player sprite, nothing will be done.
	/// </summary>
	/// <param name="slotName">name of the slot (should match an EquipSlot enum)</param>
	public void ClearItemSprite(string slotName)
	{
		EquipSlot enumA = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotName);
		if (HasPlayerSprite(enumA))
		{
			SetReference((int)enumA, -1);
		}
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="slot"></param>
	/// <returns>true iff the specified EquipSlot has an associated player sprite.</returns>
	private bool HasPlayerSprite(EquipSlot slot)
	{
		return slot != EquipSlot.id && slot != EquipSlot.storage01 && slot != EquipSlot.storage02 && slot != EquipSlot.suitStorage;
	}

	private void SetItem(string slotName, GameObject obj)
	{
		playerNetworkActions.AddItemToUISlot(obj, slotName, true);
	}

	private void InitInternals()
	{
		IsInternalsEnabled = false;
		EventManager.AddHandler(EVENT.EnableInternals, OnInternalsEnabled);
		EventManager.AddHandler(EVENT.DisableInternals, OnInternalsDisabled);
	}

	/// <summary>
	/// Removes any handlers from the event system
	/// </summary>
	private void UnregisisterInternals()
	{
		EventManager.RemoveHandler(EVENT.EnableInternals, OnInternalsEnabled);
		EventManager.RemoveHandler(EVENT.DisableInternals, OnInternalsDisabled);
	}

	public void OnInternalsEnabled()
	{
		CmdSetInternalsEnabled(true);
	}

	public void OnInternalsDisabled()
	{
		CmdSetInternalsEnabled(false);
	}

	/// <summary>
	/// Clientside method.
	/// Checks if player has proper internals equipment (Oxygen Tank and Mask)
	/// equipped in the correct inventory slots (suitStorage and mask)
	/// </summary>
	public bool HasInternalsEquipped()
	{
		if ( maskSlot?.ItemAttributes?.itemType == ItemType.Mask )
		{
			foreach ( var gasSlot in gasSlots )
			{
				if ( gasSlot.Item && gasSlot.Item.GetComponent<GasContainer>() )
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Disables or enables the player's internals on the server
	/// </summary>
	/// <param name="internalsEnabled"></param>
	[Command]
	public void CmdSetInternalsEnabled(bool internalsEnabled)
	{
		IsInternalsEnabled = internalsEnabled;
	}
}
