using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using E7.Firebase;

namespace DatabaseAPI
{
	public partial class ServerData
	{

		// executes till it reaches the point of sending a callback to AttemptLogin
		static IEnumerator GetUserProfile(string UserId, Action<string> successCallBack, Action<string> failedCallBack)
		{



			// Once logged in, this gets triggered
			//Send a request to GET all data for the UserProfile
			var url1 = FirebaseRoot + $"/users/{UserId}";
			Logger.Log("request url: " + url1);
			UnityWebRequest r1 = UnityWebRequest.Get(url1);
			r1.SetRequestHeader("Authorization", $"Bearer {Instance.refreshToken}");

			yield return r1.SendWebRequest();
			if (r1.error != null)
			{
				failedCallBack.Invoke("Failed to retrieve user Profile: " + r1.error);
			}
			else
			{
				// Processes the character into an actual userprofile object
				var snapshot = new FirestormDocumentSnapshot(r1.downloadHandler.text);
				Logger.Log("User profile string: " + r1.downloadHandler.text);
				UserProfile userprofile = snapshot.ConvertTo<UserProfile>();

				Logger.Log("Userprofile recieved: " + userprofile.username, Category.DatabaseAPI);
				PlayerManager.CurrentUserProfile = userprofile;
				successCallBack.Invoke("Received Profile for user " + UserId);
			}

		}


		static IEnumerator GetCharacterSettings(string UserId, Action<string> successCallBack, Action<string> failedCallBack)
		{

			//Send a request to GET all data for a certain (default) character
				var url2 = FirebaseRoot + $"/users/{UserId}/characters/1";
				UnityWebRequest r2 = UnityWebRequest.Get(url2);
				r2.SetRequestHeader("Authorization", $"Bearer {Instance.refreshToken}");

				yield return r2.SendWebRequest();
				if (r2.error != null)
				{
					failedCallBack.Invoke("Failed to retrieve user character settings: " + r2.error);
				}
				else
				{
					// Processes the character into an actual character object
					var snapshot = new FirestormDocumentSnapshot(r2.downloadHandler.text);
					CharacterSettings charset = snapshot.ConvertTo<CharacterSettings>();
					PlayerManager.CurrentCharacterSettings = charset;
					successCallBack.Invoke("Received Character for user " + UserId);
				}
			}

		void GetSuccess(string msg)
		{
			Logger.Log("GET Success: " + msg, Category.DatabaseAPI);
		}
		void GetFailed(string msg)
		{
			Logger.Log("GET failure: " + msg, Category.DatabaseAPI);
		}


		}
	}