using UnityEngine;

public class MainMenuButtonManager : MonoBehaviour
{

    

    public void ClickedPlayButton()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }

    public void ClickedExitButton()
    {
        //exit the game
        Application.Quit();
    }
}
