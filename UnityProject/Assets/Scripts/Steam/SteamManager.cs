using Steamworks;
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
    void Update()
    {
        // Dont run if there is no steam client to update
        if (SteamClient.IsValid)
        {
            // Makes sure the steam client gets updated
            try
            {
                UnityEngine.Profiling.Profiler.BeginSample("Steam Update");
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
    }

    private void OnApplicationQuit()
    {
        // disposes the steamclient when the game is shut down. OnDestroy, for some reason, leads to premature shutdown of SteamClient on scene switch
        if (SteamClient.IsValid)
        {
            SteamUser.GetAuthSessionTicket().Cancel();
            SteamClient.Shutdown();
        }

    }

}