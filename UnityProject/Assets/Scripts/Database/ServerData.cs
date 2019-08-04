using System;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Lobby;
using UnityEngine;
using UnityEngine.Networking;

namespace DatabaseAPI
{
	public partial class ServerData : MonoBehaviour
	{
		///<summary>
		/// Main processor for firebase authentication
		///</summary>


		class Status { public bool error = false; public bool profileSet = false; public bool charReceived = false; }

		private static ServerData serverData;
		public static ServerData Instance
		{
			get
			{
				if (serverData == null)
				{
					serverData = FindObjectOfType<ServerData>();
				}
				return serverData;
			}
		}

		/// <summary>
		/// Firebase Defines.
		/// </summary>
		//TODO FirebaseRoot can be set back to private once "ServerData.GetData" is changed to be more universal

		public const string FirebaseRoot = "https://firestore.googleapis.com/v1/projects/expedition13/databases/(default)/documents";
		private Firebase.Auth.FirebaseAuth auth;
		public static Firebase.Auth.FirebaseAuth Auth => Instance.auth;
		private Dictionary<string, Firebase.Auth.FirebaseUser> userByAuth = new Dictionary<string, Firebase.Auth.FirebaseUser>();
		public Firebase.Auth.FirebaseUser user = null;
		private bool fetchingToken = false;
		public string token;
		public string refreshToken;
		public bool isFirstTime = false;


		void Awake()
		{
			InitializeFirebase();
		}

		// Handle initialization of the necessary firebase modules:
		protected void InitializeFirebase()
		{
			auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
			auth.StateChanged += AuthStateChanged;
			auth.IdTokenChanged += IdTokenChanged;
			AuthStateChanged(this, null);
		}

		void OnEnable()
		{
			PeriodicKeyUpdate();
			EventManager.AddHandler(EVENT.LoggedOut, OnLogOut);
		}

		void OnDisable()
		{
			EventManager.RemoveHandler(EVENT.LoggedOut, OnLogOut);
		}

		/// <summary>
		/// Refresh the users profile data
		/// </summary>
		public static void ReloadProfile()
		{
			ServerData.Auth.CurrentUser.ReloadAsync().ContinueWith(task =>
			{
				if (task.IsFaulted)
				{
					Debug.LogError("Error with profile reload");
					return;
				}
			});
		}

		// Track state changes of the auth object.
		void AuthStateChanged(object sender, System.EventArgs eventArgs)
		{
			Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
			if (senderAuth != null) userByAuth.TryGetValue(senderAuth.App.Name, out user);
			if (senderAuth == auth && senderAuth.CurrentUser != user)
			{
				bool signedIn = user != senderAuth.CurrentUser && senderAuth.CurrentUser != null;
				if (!signedIn && user != null)
				{
					Logger.Log("Signed out ", Category.DatabaseAPI);
				}
				user = senderAuth.CurrentUser;
				userByAuth[senderAuth.App.Name] = user;
				if (signedIn)
				{
					Logger.Log("Signed In ", Category.DatabaseAPI);
					//TODO: Display name stuff
					/*
					displayName = user.DisplayName ?? "";
					DisplayDetailedUserInfo(user, 1);
					*/
				}
			}
		}




		// Track ID token changes.
		void IdTokenChanged(object sender, System.EventArgs eventArgs)
		{
			Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
			if (senderAuth == auth && senderAuth.CurrentUser != null && !fetchingToken)
			{
				senderAuth.CurrentUser.TokenAsync(false).ContinueWithOnMainThread(
					task => SetToken(task.Result));
			}
		}

		/// <summary>
		/// Sets the tokens
		/// If there is a token and there is no current user and character, it grabs those.
		/// </summary>
		void SetToken(string result)
		{

			if (string.IsNullOrEmpty(token))
			{
				Instance.token = result;
			}
			else
			{
				Instance.token = result;
				Instance.refreshToken = result;
			}

			if (isFirstTime)
			{
				//Always send a character at account creation, even if it's just a default character to prevent 404 on characterlookup
				isFirstTime = false;
				string charpath = "/characters/1";
				ObjectUpdate(PlayerManager.CurrentCharacterSettings, charpath, NewCharacterSuccess, NewCharacterFailed);
			}
			else
			{
				if (!string.IsNullOrEmpty(Instance.refreshToken))
				{
					Logger.Log("Grabbing Profile and Character...", Category.DatabaseAPI);
					if (string.IsNullOrEmpty(PlayerManager.CurrentUserProfile.id))
					{
						Instance.StartCoroutine(GetUserProfile(user.UserId, GetSuccess, GetFailed));
					}

					if (string.IsNullOrEmpty(PlayerManager.CurrentCharacterSettings.Name) ||
					    PlayerManager.CurrentCharacterSettings.Name == "Cuban Pete")
					{
						Instance.StartCoroutine(GetCharacterSettings(user.UserId, GetSuccess, GetFailed));
					}
				}
			}



		}

		// Series of blackhole hooks to dump results of database interaction into
		void NewCharacterSuccess(string msg) { }

		void NewCharacterFailed(string msg) { }


/// <summary>
/// DOes the actual logout, after signing out clears every setting to be sure new user has clean slate.
/// </summary>
		public void OnLogOut()
		{
			auth.SignOut();
			token = "";
			refreshToken = "";
			PlayerManager.CurrentUserProfile = new UserProfile();
			PlayerManager.CurrentCharacterSettings = new CharacterSettings();
			PlayerPrefs.SetString("username", "");
			PlayerPrefs.SetString("cookie", "");
			PlayerPrefs.SetString("userprofile", "");
			PlayerPrefs.SetInt("autoLogin", 0);
			PlayerPrefs.Save();
		}



	}
}