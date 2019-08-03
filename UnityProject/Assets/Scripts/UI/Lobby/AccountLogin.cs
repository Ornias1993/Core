using System;
using System.Collections;
using System.Collections.Generic;
using DatabaseAPI;
using UnityEngine;
using UnityEngine.UI;

namespace Lobby
{
	public class AccountLogin : MonoBehaviour
	{
		///<summary>
		///Processor for AcountLogin data in the GUI input fields
		///</summary>


		//Account login screen:
		public InputField userNameInput;
		public InputField passwordInput;
		public Toggle autoLoginToggle;

		void Start()
		{
			if (PlayerPrefs.HasKey("lastLogin"))
			{
				userNameInput.text = PlayerPrefs.GetString("lastLogin");
			}
		}

		// Gets triggered by the GUI, grabs Login details from the AccountLoggin script and send it to the actual login script
		public void TryLogin(Action<string> successAction, Action<string> errorAction)
		{
			ServerData.AttemptLogin(userNameInput.text, passwordInput.text,
				successAction, errorAction);

			PlayerPrefs.SetString("lastLogin", userNameInput.text);
			PlayerPrefs.Save();
		}

		public bool ValidLogin()
		{
			//Missing username or password
			if (string.IsNullOrEmpty(userNameInput.text) || string.IsNullOrEmpty(passwordInput.text))
			{
				return false;
			}
			return true;
		}
	}
}