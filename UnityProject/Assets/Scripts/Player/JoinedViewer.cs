using System.Collections;
using System.Collections.Generic;
using System.Threading;
using DatabaseAPI;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// This is the Viewer object for a joined player.
/// Once they join they will have local ownership of this object until a job is determined
/// and then they are spawned as player entity
/// </summary>
public class JoinedViewer : NetworkBehaviour
{
	public override void OnStartServer()
	{
		base.OnStartServer();
	}

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();

		// Send FirebaseID to server for player setup.
		CmdServerSetupPlayer(PlayerManager.CurrentUserProfile.id);
	}

	/// <summary>
	/// Sends a userdata to the server to add to playerlist
	/// Doesnt send firebaseID, SteamID or role, as those get added after authentication
	/// </summary>
	[Command]
	private void CmdServerSetupPlayer(string firebaseId)
	{
		//Add player to player list
		PlayerList.Instance.Add(new ConnectedPlayer
		{
			Connection = connectionToClient,
			GameObject = gameObject,
			Job = JobType.NULL
		});

		// If they have a player to rejoin send the client the player to rejoin, otherwise send a null gameobject.
		TargetLocalPlayerSetupPlayer(connectionToClient, PlayerList.Instance.TakeLoggedOffPlayer(firebaseId));
	}

	[TargetRpc]
	private void TargetLocalPlayerSetupPlayer(NetworkConnection target, GameObject loggedOffPlayer)
	{
		PlayerManager.SetViewerForControl(this);
		UIManager.ResetAllUI();

		//Send request to be authenticated by the server
		StartCoroutine(WaitUntilServerInit());


		// If player is joining for the first time let them pick faction and job, otherwise rejoin character.
		if (loggedOffPlayer == null)
		{
			UIManager.Display.DetermineGameMode();
		}
		else
		{
			CmdRejoin(loggedOffPlayer);
			loggedOffPlayer.GetComponent<PlayerSync>().setLocalPlayer();
			loggedOffPlayer.GetComponent<PlayerScript>().Init();
		}
	}

	/// <summary>
	/// Send all requests to get authenticated by the server.
	/// Not sending the firebase authentication will lead to a kick!
	/// </summary>
	IEnumerator WaitUntilServerInit()
	{
		//Just ensures connected player record is set on the server first before Auth req is sent
		yield return WaitFor.EndOfFrame;
		if (BuildPreferences.isSteamServer)
		{
			if (SteamClient.IsValid)
			{
				Logger.Log("Steam Client Requesting Auth", Category.Steam);
				// Generate authentication Ticket
				var ticket = SteamUser.GetAuthSessionTicket();
				var ticketBinary = ticket.Data;
				// Send Clientmessage to authenticate
				RequestSteamAuthMessage.Send(SteamClient.SteamId, ticketBinary);
			}
			else
			{
				Logger.Log("{Steam Auth request Failed", Category.Steam);
			}
		}

		if (!string.IsNullOrEmpty(ServerData.Instance.token))
		{
			Logger.Log("Firebase Client Requesting Auth");
			RequestFirebaseAuthMessage.Send(ServerData.Instance.token);
		}
		else
		{
			Logger.Log("Firebase Auth request failed");
		}
	}

	/// <summary>
	/// At the moment players can choose their jobs on round start:
	/// </summary>
	[Command]
	public void CmdRequestJob(JobType jobType, CharacterSettings characterSettings)
	{
		var player = PlayerList.Instance.Get(connectionToClient);
		/// Verifies that the player has no job
		if (player.Job == JobType.NULL)
		{
			SpawnHandler.RespawnPlayer(connectionToClient, playerControllerId,
			GameManager.Instance.GetRandomFreeOccupation(jobType), characterSettings, gameObject);

		}
		/// Spawns in player if they have a job but aren't spawned
		else if (player.GameObject == null)
		{
			SpawnHandler.RespawnPlayer(connectionToClient, playerControllerId,
			GameManager.Instance.GetRandomFreeOccupation(player.Job), characterSettings, gameObject);

		}
		else
		{
			Logger.LogWarning("[Jobs] Request Job Failed: Already Has Job", Category.Jobs);


		}

	}

	/// <summary>
	/// Asks the server to let the client rejoin into a logged off character.
	/// </summary>
	/// <param name="loggedOffPlayer">The character to be rejoined into.</param>
	[Command]
	public void CmdRejoin(GameObject loggedOffPlayer)
	{
		SpawnHandler.TransferPlayer(connectionToClient, playerControllerId, loggedOffPlayer, gameObject, EVENT.PlayerSpawned, null);
		loggedOffPlayer.GetComponent<PlayerScript>().playerNetworkActions.ReenterBodyUpdates(loggedOffPlayer);
	}
}