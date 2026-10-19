using UnityEngine;

public class MainMenuButtonManager : MonoBehaviour
{

    

    public void ClickedPlayButton()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }
}
