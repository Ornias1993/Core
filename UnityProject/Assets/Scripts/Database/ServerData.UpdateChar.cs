using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace DatabaseAPI
{
	public partial class ServerData
	{
		public static void UpdateCharacterProfile(CharacterSettings updateSettings, Action<string> callBack, Action<string> errorCallBack)
		{
			var userName = updateSettings.username;
			var json = JsonUtility.ToJson(updateSettings);
			var url = FirebaseRoot + $"/users/{Instance.user.UserId}?updateMask.fieldPaths=username&updateMask.fieldPaths=role&updateMask.fieldPaths=character";
			Instance.StartCoroutine(Instance.TryUpdateChar(url, json, userName, callBack, errorCallBack));
		}

		IEnumerator TryUpdateChar(string url, string jsonData, string newusername, Action<string> callBack, Action<string> errorCallBack)
		{
			var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
			{
				fields = new
				{
					username = new { stringValue = newusername },
					role = new { stringValue = ""},
					character = new { stringValue = jsonData }
				}
			});

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
	}
}