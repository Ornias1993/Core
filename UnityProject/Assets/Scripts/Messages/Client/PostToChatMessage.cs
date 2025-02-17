﻿using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
///     Attempts to send a chat message to the server
/// </summary>
public class PostToChatMessage : ClientMessage
{
	public static short MessageType = (short) MessageTypes.PostToChatMessage;
	public ChatChannel Channels;
	public string ChatMessageText;

	public override IEnumerator Process()
	{
		if (SentByPlayer != ConnectedPlayer.Invalid)
		{
			if (ValidRequest(SentByPlayer)) {
				ChatEvent chatEvent = new ChatEvent(ChatMessageText, SentByPlayer, Channels);
				ChatRelay.Instance.AddToChatLogServer(chatEvent);
			}
		}
		else
		{
			ChatEvent chatEvent = new ChatEvent(ChatMessageText, Channels);
			ChatRelay.Instance.AddToChatLogServer(chatEvent);
		}
		yield return null;
	}

	public static void SendThrowHitMessage( GameObject item, GameObject victim, int damage, BodyPartType hitZone = BodyPartType.None )
	{
		var player = victim.Player();
		if ( player == null ) {
			hitZone = BodyPartType.None;
		}

		var message = $"{victim.ExpensiveName()} has been hit by {item.Item()?.itemName ?? item.name}{InTheZone( hitZone )}";
		ChatRelay.Instance.AddToChatLogServer( new ChatEvent {
			channels = ChatChannel.Combat,
			message = message,
			position = victim.transform.position,
			radius = 9f,
			sizeMod = Mathf.Clamp( damage/15, 1, 3 )
		} );
	}

	/// <summary>
	/// Sends a message to all players about an attack that took place
	/// </summary>
	/// <param name="attacker">GameObject of the player that attacked</param>
	/// <param name="victim">GameObject of the player hat was the victim</param>
	/// <param name="damage">damage done</param>
	/// <param name="hitZone">zone that was damaged</param>
	/// <param name="item">optional gameobject with an itemattributes, representing the item the attack was made with</param>
	public static void SendAttackMessage( GameObject attacker, GameObject victim, float damage, BodyPartType hitZone = BodyPartType.None, GameObject item = null )
	{
		string attackVerb;
		string attack;

		if (item)
		{
			var itemAttributes = item.GetComponent<ItemAttributes>();
			attackVerb = itemAttributes.attackVerb.GetRandom() ?? "attacked";
			attack = $" with {itemAttributes.itemName}";
		}
		else
		{
			// Punch attack as there is no item.
			attackVerb = "punched";
			attack = "";
		}

		var player = victim.Player();
		if ( player == null ) {
			hitZone = BodyPartType.None;
		}

		string victimName;
		if ( attacker == victim ) {
			victimName = "self";
		} else {
			victimName = victim.ExpensiveName();
		}

		var message = $"{attacker.Player()?.Name} has {attackVerb} {victimName}{InTheZone( hitZone )}{attack}!";
		ChatRelay.Instance.AddToChatLogServer( new ChatEvent {
			channels = ChatChannel.Combat,
			message = message,
			position = victim.transform.position,
			radius = 9f,
			sizeMod = Mathf.Clamp( damage/15, 1, 3 )
		} );
	}

	/// <summary>
	/// Sends a gasp message to nearby players
	public static void SendGasp(GameObject victim)
	{
		var message = $"{victim.ExpensiveName()} gasps";
		ChatRelay.Instance.AddToChatLogServer( new ChatEvent {
			channels = ChatChannel.Local,
			message = message,
			position = victim.transform.position,
			radius = 9f,
		} );

	}

	private static string InTheZone( BodyPartType hitZone ) {
		return hitZone == BodyPartType.None ? "" : $" in the {hitZone.ToString().ToLower().Replace( "_", " " )}";
	}

	//We want ChatEvent to be created on the server, so we're only passing the individual variables
	public static PostToChatMessage Send(string message, ChatChannel channels)
	{
		PostToChatMessage msg = new PostToChatMessage
		{
			Channels = channels,
			ChatMessageText = message
		};
		msg.Send();

		return msg;
	}

	public bool ValidRequest(ConnectedPlayer player)
	{
		PlayerScript playerScript = player.Script;
		//Need to add system channel here so player can transmit system level events but not select it in the UI
		ChatChannel availableChannels = playerScript.GetAvailableChannelsMask() | ChatChannel.System;
		if ((availableChannels & Channels) == Channels)
		{
			return true;
		}
		return false;
	}

	public override string ToString()
	{
		return $"[PostToChatMessage ChatMessageText={ChatMessageText} Channels={Channels} MessageType={MessageType}]";
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		Channels = (ChatChannel) reader.ReadUInt32();
		ChatMessageText = reader.ReadString();
	}

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write((int) Channels);
		writer.Write(ChatMessageText);
	}
}