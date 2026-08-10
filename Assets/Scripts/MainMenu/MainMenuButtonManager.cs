using UnityEngine;

public class MainMenuButtonManager : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ClickedPlayButton()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(2);
    }
}
