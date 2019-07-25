using Steamworks;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

//
// This class takes care of a lot of stuff for you.
//
//  1. It initializes steam on startup.
//  2. It calls Update so you don't have to.
//  3. It disposes and shuts down Steam on close.
//
// You don't need to reference this class anywhere or access the client via it.
// To access the client use Facepunch.Steamworks.Client.Instance, see SteamAvatar
// for an example of doing this in a nice way.
//
public class SteamManager : MonoBehaviour
{
    public uint AppId;

    void Awake()
    {
        // keep us around until the game closes
        DontDestroyOnLoad(this.gameObject);
        // We do not want a client running on a dedicated server
 
            if (AppId == 0)
                throw new System.Exception("You need to set the AppId to your game");

        // Check if dedicated server
        if (GameData.IsHeadlessServer || GameData.Instance.testServer || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
			Logger.Log("Skipping Steam Client Init as this is a Headless Server",Category.Steam);
        }
        else
        {
            //Run steamclient init if there is no steam client active
            if(!SteamClient.IsValid)
            {
               try
                {
	                Steamworks.SteamClient.Init( AppId );
                    Logger.Log("Steam Initialized: " + SteamClient.Name + " / " + SteamClient.SteamId, Category.Steam);
                }
                // Prevents NRE's if something goes wrong with the Client
                catch ( System.Exception e )
                {
                     Logger.LogWarning("Steam Init Error: " + e, Category.Steam);
                }
            }
            //If there is a running steamclient, throw it into log
            else
            {
                Logger.Log("Steam Already Initialized", Category.Steam);
            }
    }
    }


    	public void SteamServerStart()
	{
		// init the SteamServer needed for authentication of players
		//
		string path = Path.GetFullPath(".");
		string folderName = Path.GetFileName(Path.GetDirectoryName(path));
		SteamServerInit  serverInit  = new SteamServerInit(folderName, "Expedition 13");
		try
		{
    	SteamServer.Init( 787180, serverInit );
		Logger.Log("Server registered", Category.Steam);

			if (GameData.IsHeadlessServer || GameData.Instance.testServer || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
			{
				SteamServer.DedicatedServer = true;
			}

		SteamServer.LogOnAnonymous();
		// Set required settings for dedicated server

		Logger.Log("Setting up Auth hook", Category.Steam);

		//Process callback data for authentication
		SteamServer.OnValidateAuthTicketResponse += ProcessAuth;

		}
		catch ( System.Exception e)
		{
    	// Couldn't init for some reason (dll errors, blocked ports)
		Logger.Log("Server NOT registered" + e, Category.Steam);
		}

	}

    
	//Process incomming authentication responses from steam
	public void ProcessAuth(SteamId steamid, SteamId ownerid, Steamworks.AuthResponse status)
	{
		var player = PlayerList.Instance.Get(steamid);
		if (player == ConnectedPlayer.Invalid)
		{
			Logger.LogWarning($"Steam gave us a {status} ticket response for unconnected id {steamid}", Category.Steam);
			return;
		}

		// User Authenticated
		if (status == AuthResponse.OK)
		{
			Logger.LogWarning($"Steam gave us a 'ok' ticket response for already connected id {steamid}", Category.Steam);
			return;
		}

		// Disconnect logging
		if (status == AuthResponse.VACCheckTimedOut)
		{
			Logger.LogWarning($"The SteamID '{steamid}' left the server. ({status})", Category.Steam);
			return;
		}

		//Kick players without valid Authentication
		CustomNetworkManager.Kick(player, "Authentication failed for: " + steamid);
	}

    void Update()
    {
        // Dont run if there is no steam client to update
        if (SteamClient.IsValid)
        {
            // Makes sure the steam client gets updated
            try
            {
                UnityEngine.Profiling.Profiler.BeginSample("SteamClient Update");
                SteamClient.RunCallbacks();
            }
            catch ( System.Exception e )
            {
                // Something went wrong! Steam is closed?
                Logger.Log("Steam update error: " + e, Category.Steam);
	        
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        if(SteamServer.IsValid)
		{
            try
            {
                UnityEngine.Profiling.Profiler.BeginSample("SteamServer Update");
                SteamServer.RunCallbacks();
            }
            catch ( System.Exception e )
            {
            // Something went wrong!
                Logger.Log("Steam update error: " + e, Category.Steam);
	        
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
		}
    }

    private void OnApplicationQuit()
    {
        // disposes the steamclient when the game is shut down. OnDestroy, for some reason, leads to premature shutdown of SteamClient on scene switch
        if (SteamClient.IsValid)
        {
            SteamUser.GetAuthSessionTicket().Cancel();
            SteamClient.Shutdown();
        }

        // This code makes sure the steam server is disposed when the CNM is destroyed
		if (SteamServer.IsValid)
		{
			SteamServer.Shutdown();
		}

    }

}