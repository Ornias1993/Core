using System.Collections;
using DatabaseAPI;
using E7.Firebase;
using UnityEngine.Networking;

/// <summary>
/// This message checks the Firebase token from the player
/// If the token checks out it should download the userprofile and set the ROLE accordingly
/// This way the player has no influence over his/her role on the server
/// </summary>
public class RequestFirebaseAuthMessage : ClientMessage
{
	public static short MessageType = (short) MessageTypes.RequestFirebaseAuthMessage;
	public string Ticket;

	public override IEnumerator Process()
	{
		string verifiedUID;
		bool verified = ServerData.Verify(Ticket, out verifiedUID );

		if (verified)
		{
			Logger.Log("Verified token for UID: " + verifiedUID, Category.DatabaseAPI);
			SentByPlayer.FirebaseId = verifiedUID;

            // Once logged in, this gets triggered
			//Send a request to GET all data for the UserProfile
			var url = ServerData.FirebaseRoot + $"/users/{verifiedUID}";
				UnityWebRequest r = UnityWebRequest.Get(url);
				r.SetRequestHeader("Authorization", $"Bearer {ServerData.Instance.refreshToken}");

				yield return r.SendWebRequest();
				if (r.error != null)
				{
					//Seems we couldn't recieve a user profile
					//Defaulting to USER role
					//TODO: Implement a server account to get this data with

					Logger.Log("Failed to retrieve user Profile for user: " + verifiedUID + r.error,
						Category.DatabaseAPI);
					SentByPlayer.Role = "USER";
				}
				else
				{
					// Processes the character into an actual userprofile object
					var snapshot = new FirestormDocumentSnapshot(r.downloadHandler.text);
					UserProfile userprofile = snapshot.ConvertTo<UserProfile>();

					Logger.Log("Userprofile recieved for user: " + userprofile.username, Category.DatabaseAPI);
					SentByPlayer.Role = userprofile.role;
				}




			if (SentByPlayer.Role == "BANNED")
			{
				// Kick banned player
				CustomNetworkManager.Kick( SentByPlayer, "Firebase returned status: Banned" );
			}
		}
		else
		{
			// Here we can kick the player on authentication failure
			// authentication failure != Role=Banned, Role=Banned is an AUTHORISATION failure,

			CustomNetworkManager.Kick( SentByPlayer, "Firebase returned status: Authentication Failed ");
		}



		yield break;
	}

	public static RequestFirebaseAuthMessage Send(string ticket)
	{
		RequestFirebaseAuthMessage msg = new RequestFirebaseAuthMessage
		{
			Ticket = ticket
		};

		msg.Send();
		return msg;
	}


	public override string ToString()
	{
		return $"[RequestAuthMessage Ticket={Ticket} Type={MessageType} SentBy={SentByPlayer}]";
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		Ticket = reader.ReadString();
	}

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write(Ticket);
	}
}