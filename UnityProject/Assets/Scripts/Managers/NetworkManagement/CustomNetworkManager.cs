﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
	public static bool IsServer => Instance._isServer;

	public static CustomNetworkManager Instance;

	[HideInInspector] public bool _isServer;
	[HideInInspector] public bool spawnableListReady;
	public GameObject humanPlayerPrefab;
	public GameObject ghostPrefab;

	public SteamManager steamManager;
 
	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private void Start()
	{
		customConfig = true;

		SetSpawnableList();
		//		if (!IsClientConnected() && !GameData.IsHeadlessServer &&
		//		    GameData.IsInGame)
		//		{
		//			UIManager.Display.logInWindow.SetActive(true);
		//		}

		channels.Add(QosType.ReliableSequenced);
		channels.Add(QosType.UnreliableFragmented);

		ConnectionConfig config = connectionConfig;
		config.AcksType = ConnectionAcksType.Acks64;
		config.FragmentSize = 512;
		config.PacketSize = 1440;

		if (GameData.IsInGame && PoolManager.Instance == null)
		{
			ObjectManager.StartPoolManager();
		}

		//Automatically host if starting up game *not* from lobby
		if (SceneManager.GetActiveScene().name != offlineScene)
		{
			StartHost();
		}
	}

	private void SetSpawnableList()
	{
		spawnPrefabs.Clear();

		NetworkIdentity[] networkObjects = Resources.LoadAll<NetworkIdentity>("");
		foreach (NetworkIdentity netObj in networkObjects)
		{
			spawnPrefabs.Add(netObj.gameObject);
		}

		string[] dirs = Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories);

		foreach (string dir in dirs)
		{
			loadFolder(dir);
			foreach (string subdir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
			{
				loadFolder(subdir);
			}
		}

		spawnableListReady = true;
	}

	private void loadFolder(string folderpath)
	{
		folderpath = folderpath.Substring(folderpath.IndexOf("Resources", StringComparison.Ordinal) + "Resources".Length);
		foreach (NetworkIdentity netObj in Resources.LoadAll<NetworkIdentity>(folderpath))
		{
			if (!spawnPrefabs.Contains(netObj.gameObject))
			{
				spawnPrefabs.Add(netObj.gameObject);
			}
		}
	}

	private void OnEnable()
	{
		SceneManager.activeSceneChanged += OnLevelFinishedLoading;
	}

	private void OnDisable()
	{
		SceneManager.activeSceneChanged -= OnLevelFinishedLoading;
	}

	public override void OnStartServer()
	{
		_isServer = true;
		base.OnStartServer();
		this.RegisterServerHandlers();
		if (BuildPreferences.isSteamServer)
		{
			steamManager.SteamServerStart();
		}
	}




	public static void Kick(ConnectedPlayer player, string raisins = "4 no raisins")
	{
		if (!player.Connection.isConnected)
		{
			Logger.Log($"Not kicking, already disconnected: {player}", Category.Connections);
			return;
		}
		Logger.Log($"Kicking {player} : {raisins}", Category.Connections);
		InfoWindowMessage.Send(player.GameObject, $"Kicked: {raisins}", "Kicked");
		PostToChatMessage.Send($"Player '{player.Name}' got kicked: {raisins}", ChatChannel.System);
		player.Connection.Disconnect();
		player.Connection.Dispose();
	}

	public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
	{
		//This spawns the player prefab
		if (GameData.IsHeadlessServer || GameData.Instance.testServer)
		{
			//this is a headless server || testing headless (it removes the server player for localClient)
			if (conn.address != "localClient")
			{
				StartCoroutine(WaitToSpawnPlayer(conn, playerControllerId));
			}
		}
		else
		{
			//This is a host server (keep the server player as it is for the host player)
			StartCoroutine(WaitToSpawnPlayer(conn, playerControllerId));
		}

		if (_isServer)
		{
			//Tell them what the current round time is
			UpdateRoundTimeMessage.Send(GameManager.Instance.stationTime.ToString("O"));
		}
	}
	private IEnumerator WaitToSpawnPlayer(NetworkConnection conn, short playerControllerId)
	{
		yield return WaitFor.Seconds(1f);
		OnServerAddPlayerInternal(conn, playerControllerId);
	}

	void Update()
	{
	}

	private void OnApplicationQuit()
	{

	}

	private void OnServerAddPlayerInternal(NetworkConnection conn, short playerControllerId)
	{
		if (playerPrefab == null)
		{
			if (!LogFilter.logError)
			{
				return;
			}
			Logger.LogError("The PlayerPrefab is empty on the NetworkManager. Please setup a PlayerPrefab object.", Category.Connections);
		}
		else if (playerPrefab.GetComponent<NetworkIdentity>() == null)
		{
			if (!LogFilter.logError)
			{
				return;
			}
			Logger.LogError("The PlayerPrefab does not have a NetworkIdentity. Please add a NetworkIdentity to the player prefab.", Category.Connections);
		}
		else if (playerControllerId < conn.playerControllers.Count && conn.playerControllers[playerControllerId].IsValid &&
			conn.playerControllers[playerControllerId].gameObject != null)
		{
			if (!LogFilter.logError)
			{
				return;
			}
			Logger.LogError("There is already a player at that playerControllerId for this connections.", Category.Connections);
		}
		else
		{
			SpawnHandler.SpawnViewer(conn, playerControllerId);
		}
	}

	public override void OnClientConnect(NetworkConnection conn)
	{
		this.RegisterClientHandlers(conn);
		//		if (_isServer)
		//		{
		//			//do special server wizardry here
		//			PlayerList.Instance.Add(new ConnectedPlayer
		//			{
		//				Connection = conn,
		//			});
		//		}

		if (GameData.IsInGame && PoolManager.Instance == null)
		{
			ObjectManager.StartPoolManager();
		}

		//This client connecting to server, wait for the spawnable prefabs to register
		StartCoroutine(WaitForSpawnListSetUp(conn));
	}

	///Sync some position data explicitly, if it is required
	/// Warning: sending a lot of data, make sure client receives it only once
	public void SyncPlayerData(GameObject playerGameObject)
	{
		//All matrices
		MatrixMove[] matrices = FindObjectsOfType<MatrixMove>();
		for (var i = 0; i < matrices.Length; i++)
		{
			matrices[i].NotifyPlayer(playerGameObject, true);
		}
		//All transforms
		CustomNetTransform[] scripts = FindObjectsOfType<CustomNetTransform>();
		for (var i = 0; i < scripts.Length; i++)
		{
			scripts[i].NotifyPlayer(playerGameObject);
		}
		//All player bodies
		PlayerSync[] playerBodies = FindObjectsOfType<PlayerSync>();
		for (var i = 0; i < playerBodies.Length; i++)
		{
			playerBodies[i].NotifyPlayer(playerGameObject, true);
		}

		//StorageObject UUIDs
		StorageObject[] storageObjs = FindObjectsOfType<StorageObject>();
		for (var i = 0; i < storageObjs.Length; i++)
		{
			storageObjs[i].SyncUUIDsWithPlayer(playerGameObject);
		}

		//TileChange Data
		TileChangeManager[] tcManagers = FindObjectsOfType<TileChangeManager>();
		for (var i = 0; i < tcManagers.Length; i++)
		{
			tcManagers[i].NotifyPlayer(playerGameObject);
		}

		//Doors
		DoorController[] doors = FindObjectsOfType<DoorController>();
		for (var i = 0; i < doors.Length; i++)
		{
			doors[i].NotifyPlayer(playerGameObject);
		}
		Logger.Log($"Sent sync data ({matrices.Length} matrices, {scripts.Length} transforms, {playerBodies.Length} players) to {playerGameObject.name}", Category.Connections);
	}

	public void SyncCharSprites(GameObject recipient, bool newMob)
	{
		//All player bodies
		PlayerSync[] playerBodies = FindObjectsOfType<PlayerSync>();
		for (var i = 0; i < playerBodies.Length; i++)
		{
			var playerBody = playerBodies[i];
			if(newMob && playerBody.gameObject == recipient)
			{
				continue;
			}
			var playerSprites = playerBody.GetComponent<PlayerSprites>();
			if (playerSprites)
			{
				playerSprites.NotifyPlayer(recipient);
			}
			var equipment = playerBody.GetComponent<Equipment>();
			if(equipment)
			{
				equipment.NotifyPlayer(recipient);
			}
		}
	}

	private IEnumerator WaitForSpawnListSetUp(NetworkConnection conn)
	{
		while (!spawnableListReady)
		{
			yield return WaitFor.Seconds(1);
		}

		base.OnClientConnect(conn);
	}

	/// server actions when client disconnects
	public override void OnServerDisconnect(NetworkConnection conn)
	{
		var player = PlayerList.Instance.Get(conn);
		Logger.Log($"Player Disconnected: {player.Name}", Category.Connections);
		PlayerList.Instance.Remove(conn);
	}

	private void OnLevelFinishedLoading(Scene oldScene, Scene newScene)
	{
		if (newScene.name != "Lobby")
		{
			//INGAME:
			EventManager.Broadcast(EVENT.RoundStarted);
			if (PoolManager.Instance == null)
			{
				ObjectManager.StartPoolManager();
				StartCoroutine(DoHeadlessCheck());
			}
		}
		else
		{
			EventManager.Broadcast(EVENT.RoundEnded);
		}
	}

	private IEnumerator DoHeadlessCheck()
	{
		yield return WaitFor.Seconds(0.1f);
		if (!GameData.IsHeadlessServer && !GameData.Instance.testServer)
		{
			if (!IsClientConnected())
			{
				//				if (GameData.IsInGame) {
				//					UIManager.Display.logInWindow.SetActive(true);
				//				}
				UIManager.Display.jobSelectWindow.SetActive(false);
			}
		}
		else
		{
			//Set up for headless mode stuff here
			//Useful for turning on and off components
			_isServer = true;
		}
	}

	//Editor item transform dance experiments
#if UNITY_EDITOR
	public void MoveAll()
	{
		StartCoroutine(TransformWaltz());
	}

	private IEnumerator TransformWaltz()
	{
		CustomNetTransform[] scripts = FindObjectsOfType<CustomNetTransform>();
		var sequence = new []
		{
			Vector3.right, Vector3.up, Vector3.left, Vector3.down,
				Vector3.down, Vector3.left, Vector3.up, Vector3.right
		};
		for (var i = 0; i < sequence.Length; i++)
		{
			for (var j = 0; j < scripts.Length; j++)
			{
				NudgeTransform(scripts[j], sequence[i]);
			}
			yield return WaitFor.Seconds(1.5f);
		}
	}

	private static void NudgeTransform(CustomNetTransform netTransform, Vector3 where)
	{
		netTransform.SetPosition(netTransform.ServerState.Position + where);
	}
#endif
}