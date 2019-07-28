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
		///Tries to process any object+url send into the database
		///</summary>

		//This gets the Object that needs to be send, and the url it needs to be send to and starts sending the data to the server
		public static void ObjectUpdate(object updateObject, string path,Action<string> callBack, Action<string> errorCallBack)
		{
			//Putting the character into a single JSON object
			Instance.StartCoroutine(Instance.TryUpdate(updateObject, path, callBack, errorCallBack));
		}

		//This coroutine actually uploads the object
		IEnumerator TryUpdate(object updateSettings, string path, Action<string> callBack, Action<string> errorCallBack)
		{
			// This generates the required url suplement based on the object.
			string docPath = "";
			foreach(var property in updateSettings.GetType().GetFields()) 
    		{
				string addPath = "updateMask.fieldPaths=" + property.Name + "&";
				docPath += addPath;
    		}

			// This uses firestorm to generate a FireStore compatible JSON
			string payload = FirestormUtility.ToJsonDocument(updateSettings, "");
			// The updatemash is a requirement, skipping it in the URL leads to 403
			var url = FirebaseRoot + $"/users/{Instance.user.UserId}{path}?{docPath}";

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
	}
}

