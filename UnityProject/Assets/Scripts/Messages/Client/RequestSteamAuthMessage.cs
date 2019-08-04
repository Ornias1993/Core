using System.Collections;
using UnityEngine.Networking;
using Steamworks;
using Steamworks.Data;

/// <summary>
/// This message checks if the steam ID send by player, is the same as the ticket they get from steam
/// basically it authenticates the steamID
/// Not sending a steamID should (prob) not lead to kicking, but if a user sends a steam ID it should be verified.
/// </summary>
public class RequestSteamAuthMessage : ClientMessage
{
	public static short MessageType = (short) MessageTypes.RequestSteamAuthMessage;
	public ulong SteamID;
	public byte[] TicketBinary;

	public override IEnumerator Process()
	{
		//	Logger.Log("Processed " + ToString());
//		yield return WaitFor(SentBy);

//		if ( !Managers.instance.isForRelease )
//		{
//			Logger.Log($"Ignoring {this}, not for release");
//			yield break;
//		}

		// Primarily used for localhost
		if ( PlayerList.IsConnWhitelisted(SentByPlayer) )
		{
			Logger.Log( $"Player whitelisted: {SentByPlayer}", Category.Steam);
			yield break;
		}

		Logger.Log("Server Starting Auth for User: " + SteamID, Category.Steam);
		if (SteamServer.IsValid && SteamID != 0 && TicketBinary != null)
		{

			//This results in a callback in CustomNetworkManager
			if (!SteamServer.BeginAuthSession(TicketBinary, SteamID))
			{
				// This can trigger for a lot of reasons
				// More info: http://projectzomboid.com/modding//net/puppygames/steam/BeginAuthSessionResult.html
				// if triggered does prevent the authchange callback.
				Logger.Log("Start Session returned false, Not registering steamID", Category.Steam);

			}
			else
			{
				SentByPlayer.SteamId = SteamID;
				Logger.Log("Callback in CustomNetworkManager for: " + SteamID, Category.Steam);
			}
		}

	}

	public static RequestSteamAuthMessage Send(ulong steamid, byte[] ticketBinary)
	{
		RequestSteamAuthMessage msg = new RequestSteamAuthMessage
		{
			SteamID = steamid,
			TicketBinary = ticketBinary
		};

		msg.Send();
		return msg;
	}


	public override string ToString()
	{
		return $"[RequestAuthMessage SteamID={SteamID} TicketBinary={TicketBinary} Type={MessageType} SentBy={SentByPlayer}]";
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		SteamID = reader.ReadUInt64();
		int length = reader.ReadInt32();
		TicketBinary = reader.ReadBytes(length);
	}

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write(SteamID);
		writer.Write(TicketBinary.Length);
		writer.Write(TicketBinary, TicketBinary.Length);
	}
}