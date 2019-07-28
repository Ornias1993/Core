using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using E7.Firebase;

namespace DatabaseAPI
{
	public partial class ServerData
	{
		///<summary>
		///Tries to log user into firebase and grab all userdata from the DB
		///</summary>

		// This processes the login credentials you enter
		public static void AttemptLogin(string username, string _password,
			Action<CharacterSettings> successCallBack, Action<string> failedCallBack)
		{
			var status = new Status();
			Instance.auth.SignInWithEmailAndPasswordAsync(username, _password).ContinueWith(task =>
			{
				if (task.IsFaulted)
				{
					failedCallBack.Invoke("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
					status.error = true;
					return;
				}
			});

			Instance.StartCoroutine(MonitorLogin(successCallBack, failedCallBack, status));
		}

		// executes till it reaches the point of sending a callback to AttemptLogin
		static IEnumerator MonitorLogin(Action<CharacterSettings> successCallBack, Action<string> failedCallBack, Status status)
		{
			float timeOutTime = 8f;
			float timeOutCount = 0f;

			while (Auth.CurrentUser == null || string.IsNullOrEmpty(Instance.refreshToken))
			{
				timeOutCount += Time.deltaTime;
				if (timeOutCount >= timeOutTime || status.error)
				{
					if (!status.error)
					{
						Logger.Log("Log in timed out", Category.DatabaseAPI);
					}
					failedCallBack.Invoke("Check your username and password.");
					yield break;
				}
				yield return WaitFor.EndOfFrame;
			}
			// Once logged in, this gets triggered
			//Send a request to GET all data for the UserProfile
			var url1 = FirebaseRoot + $"/users/{Auth.CurrentUser.UserId}";
			UnityWebRequest r1 = UnityWebRequest.Get(url1);
			r1.SetRequestHeader("Authorization", $"Bearer {Instance.refreshToken}");

			yield return r1.SendWebRequest();
			if (r1.error != null)
			{
				Logger.Log("Failed to retrieve user Profile: " + r1.error, Category.DatabaseAPI);
				failedCallBack.Invoke(r1.error);
			}
			else
			{
				// Processes the character into an actual userprofile object
				var snapshot = new FirestormDocumentSnapshot(r1.downloadHandler.text);
				UserProfile userprofile = snapshot.ConvertTo<UserProfile>();
                
				Logger.Log("Userprofile recieved: " + userprofile.username, Category.DatabaseAPI);
				PlayerManager.CurrentUserProfile = userprofile;
			}

			//Send a request to GET all data for a certain (default) character
			var url2 = FirebaseRoot + $"/users/{Auth.CurrentUser.UserId}/characters/1";
			UnityWebRequest r2 = UnityWebRequest.Get(url2);
			r2.SetRequestHeader("Authorization", $"Bearer {Instance.refreshToken}");

			yield return r2.SendWebRequest();
			if (r2.error != null)
			{
				Logger.Log("Failed to retrieve user character settings: " + r2.error, Category.DatabaseAPI);
				failedCallBack.Invoke(r2.error);
			}
			else
			{
				// Processes the character into an actual character object
				var snapshot = new FirestormDocumentSnapshot(r2.downloadHandler.text);
				CharacterSettings charset = snapshot.ConvertTo<CharacterSettings>();
                successCallBack.Invoke(charset);
			}
		}

	}
}