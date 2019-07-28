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

		private const string FirebaseRoot = "https://firestore.googleapis.com/v1/projects/expedition13/databases/(default)/documents";
		private Firebase.Auth.FirebaseAuth auth;
		public static Firebase.Auth.FirebaseAuth Auth => Instance.auth;
		private Dictionary<string, Firebase.Auth.FirebaseUser> userByAuth = new Dictionary<string, Firebase.Auth.FirebaseUser>();
		private Firebase.Auth.FirebaseUser user = null;
		private bool fetchingToken = false;
		public string token;
		public string refreshToken;
		public bool isFirstTime = false;
		

		void Start()
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
					PlayerPrefs.DeleteKey ("charactersettings");
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
		}

		// Series of blackhole hooks to dump results of database interaction into
		void NewCharacterSuccess(string msg) { }

		void NewCharacterFailed(string msg) { }

		//end blackhole

		public void OnLogOut()
		{
			auth.SignOut();
			token = "";
			refreshToken = "";
			PlayerPrefs.SetString("username", "");
			PlayerPrefs.SetString("cookie", "");
			PlayerPrefs.SetString("userprofile", "");
			PlayerPrefs.SetString ("charactersettings", "");
			PlayerPrefs.SetInt("autoLogin", 0);
			PlayerPrefs.Save();
		}



	}
}