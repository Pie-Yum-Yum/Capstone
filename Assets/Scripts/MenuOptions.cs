using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.Rendering.Universal;

public class MenuOptions : MonoBehaviour
{
    
    [Header("Volume")]
    public float volumeLvl;
    public float volumeInd;
    public Slider volumeSlider; 

    [Header("Brightness")]
    public float brightnessLvl;
    public float brightnessInd;
    public Slider brightnessSlider;
    
    [Header("Sensitivity")]
    public float sensitivityLvl;
    public float sensitivityInd;
    public Slider sensitivitySlider;
    
    [Header("Ascii")]
    public float AsciiAffector;
    public float AsciiInd;
    public Slider AsciiSlider;

    [Header("Fullscreen")]    
    public bool isFullscreen;

    //public UniversalRenderData universalRenderPipelineData;


    public void SliderValueChanged(){
        // Update the values based on user input
        // This method should be called whenever a UI element changes its value
        volumeLvl = volumeSlider.value;
        brightnessLvl = brightnessSlider.value;
        sensitivityLvl = sensitivitySlider.value;
        AsciiAffector = AsciiSlider.value;

        // Apply the changes to the indicators
        volumeInd = volumeLvl;
        brightnessInd = brightnessLvl;  
        sensitivityInd = sensitivityLvl;
        AsciiInd = AsciiAffector;


    }
       
    public void ExitToMenu()
    {
        //load next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(1); // check that the scene is the correct number
        //onValueChanged();
    }


    
    
}
