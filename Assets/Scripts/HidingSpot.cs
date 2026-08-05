using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    public GameObject Top;

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnTriggerEnter(Collider other){
        if(other.CompareTag("Player")){
            Top.GetComponent<BoxCollider>().enabled = false;
        }
    }
    void OnTriggerExit(Collider other){
        if(other.CompareTag("Player")){
            Top.GetComponent<BoxCollider>().enabled = true;
        }
    }
}
