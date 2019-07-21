using UnityEngine;

public class More_Buttons : MonoBehaviour
{
	public void Patr_btn()
	{
		Application.OpenURL("https://patreon.com/Expedition13");
		SoundManager.Play("Click01");
	}
	
	public void Webs_btn()
	{
		Application.OpenURL("https://expedition13.org");
		SoundManager.Play("Click01");
	}

	public void Git_btn()
	{
		Application.OpenURL("https://github.com/expedition13/core");
		SoundManager.Play("Click01");
	}

	public void Reddit_btn()
	{
		Application.OpenURL("https://reddit.com/r/expedition13");
		SoundManager.Play("Click01");
	}

	public void Discord_btn()
	{
		Application.OpenURL("https://discord.gg/rVyqvFu");
		SoundManager.Play("Click01");
	}

	public void Issues_btn()
	{
		Application.OpenURL("https://github.com/expedition13/core/issues");
		SoundManager.Play("Click01");
	}
}