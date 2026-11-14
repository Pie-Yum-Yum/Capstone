using UnityEngine;

// A level's baked room layout for the Eye spell map. Made by MapLayoutBaker (right-click it > Bake Map Layout).
// Texture channels: R = floor, G = wall. The texture covers worldMin to worldMin + worldSize on the X/Z plane.
[CreateAssetMenu(menuName = "Map/Map Layout")]
public class MapLayout : ScriptableObject
{
    public Texture2D texture;
    [Tooltip("World X/Z of the texture's bottom-left corner.")]
    public Vector2 worldMin;
    [Tooltip("World width (X) and depth (Z) the texture covers.")]
    public Vector2 worldSize = new Vector2(100f, 100f);
}
