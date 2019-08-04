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
		///Try to log user into firebase
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

		///<summary>
		///Wait for UserProfile and CharacterSettings to download
		/// Timeout if not recieved in time or on login error
		///</summary>
		static IEnumerator MonitorLogin(Action<string> successCallBack, Action<string> failedCallBack, Status status)
		{
			float timeOutTime = 8f;
			float timeOutCount = 0f;


			while (string.IsNullOrEmpty(PlayerManager.CurrentUserProfile.id) || PlayerManager.CurrentCharacterSettings.Name == "Cuban Pete")
			{
				timeOutCount += Time.deltaTime;
				if (timeOutCount >= timeOutTime || status.error)
				{
					if (!status.error)
					{

						Logger.Log("Log in timed out", Category.DatabaseAPI);
					}

					//TODO maybe throw in a log entry with the status?
					//Though it is mostly just "false" now that downloading credentials is moved.

					failedCallBack.Invoke("Check your username and password." );
					yield break;
				}

				yield return WaitFor.EndOfFrame;
			}

			successCallBack.Invoke("loggin successfull");

		}
	}
}