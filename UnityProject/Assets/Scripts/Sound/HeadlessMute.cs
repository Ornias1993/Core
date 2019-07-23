using UnityEngine;

public class HeadlessMute : MonoBehaviour
{
    void Start()
    {
        if(GameData.IsHeadlessServer == true)
        {
         AudioListener audioListener = GetComponent<AudioListener>();
         audioListener.enabled = false;
        }
    }


}