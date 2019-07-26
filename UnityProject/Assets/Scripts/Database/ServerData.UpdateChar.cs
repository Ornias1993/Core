using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace DatabaseAPI
{
	public partial class ServerData
	{
		//This starts updating the character and creates one if it does not yet exist
		public static void UpdateCharacterProfile(CharacterSettings updateSettings, Action<string> callBack, Action<string> errorCallBack)
		{
			//Putting the character into a single JSON object
			var json = JsonUtility.ToJson(updateSettings);
			
			Instance.StartCoroutine(Instance.TryUpdateChar(json, callBack, errorCallBack));
		}
		//This coroutine actually uploads the character settings
		IEnumerator TryUpdateChar(string jsonData, Action<string> callBack, Action<string> errorCallBack)
		{
			// The updatemash is a requirement, skipping it in the URL leads to 403
			var url = FirebaseRoot + $"/users/{Instance.user.UserId}/characters/1?updateMask.fieldPaths=character";
			var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
			{
				// It would be nicer to put the character properties into seperate fields in the future
				fields = new
				{
					character = new { stringValue = jsonData }
				}
			});

			// This is just a basic PATCH to the Firestore REST API, No unity firestore plugin available for this
			UnityWebRequest r = UnityWebRequest.Put(url, payload);
			r.method = "PATCH";
			r.SetRequestHeader("Content-Type", "application/json");
			r.SetRequestHeader("Authorization", $"Bearer {Instance.token}");

			yield return r.SendWebRequest();
			if (r.error != null)
			{
				Logger.Log("DB request failed: " + r.error, Category.DatabaseAPI);
				errorCallBack.Invoke(r.error);
			}
			else
			{
				callBack.Invoke(r.downloadHandler.text);
			}
		}
		//This starts updating the user (which contains the character in the DB) and creates one if it does not yet exist
		//Yeah it just passes a string to the Coroutine
		public static void UpdateUserProfile(string userName, Action<string> callBack, Action<string> errorCallBack)
		{
			Instance.StartCoroutine(Instance.TryUpdateUserProf(userName, callBack, errorCallBack));
		}

		//This coroutine actually uploads the user settings
		IEnumerator TryUpdateUserProf(string userName, Action<string> callBack, Action<string> errorCallBack)
		{
			// The updatemash is a requirement, skipping it in the URL leads to 403
			var userUrl = FirebaseRoot + $"/users/{Instance.user.UserId}?updateMask.fieldPaths=username";
			var userPayload = Newtonsoft.Json.JsonConvert.SerializeObject(new
			{
				fields = new
				{
					// New field in user root could easily be added, even clientside...
					// Role is prevented on Firestore, dont try it. Simply cant be set by non-admin
					username = new { stringValue = userName }
				}
			});

			//// This is just a basic PATCH to the Firestore REST API, No unity firestore plugin available for this
			UnityWebRequest r = UnityWebRequest.Put(userUrl, userPayload);
			r.method = "PATCH";
			r.SetRequestHeader("Content-Type", "application/json");
			r.SetRequestHeader("Authorization", $"Bearer {Instance.token}");

			yield return r.SendWebRequest();
			if (r.error != null)
			{
				Logger.Log("DB request failed: " + r.error, Category.DatabaseAPI);
				errorCallBack.Invoke(r.error);
			}
			else
			{
				callBack.Invoke(r.downloadHandler.text);
			}
		}
	}
}