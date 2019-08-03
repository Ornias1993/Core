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
			Action<string> successCallBack, Action<string> failedCallBack)
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
		static IEnumerator MonitorLogin(Action<string> successCallBack, Action<string> failedCallBack, Status status)
		{
			float timeOutTime = 8f;
			float timeOutCount = 0f;

			Logger.Log("charname at login" + PlayerManager.CurrentCharacterSettings.Name);
			Logger.Log("Userid at login" + PlayerManager.CurrentUserProfile.id);

			while (string.IsNullOrEmpty(PlayerManager.CurrentUserProfile.id) || PlayerManager.CurrentCharacterSettings.Name == "Cuban Pete")
			{
				timeOutCount += Time.deltaTime;
				if (timeOutCount >= timeOutTime || status.error)
				{
					if (!status.error)
					{

						Logger.Log("Log in timed out", Category.DatabaseAPI);
					}

					failedCallBack.Invoke("Check your username and password." + status.error);
					yield break;
				}

				yield return WaitFor.EndOfFrame;
			}

			successCallBack.Invoke("loggin successfull");

		}
	}
}