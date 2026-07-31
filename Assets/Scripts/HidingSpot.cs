using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    public GameObject Table;

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnTriggerEnter(Collider other){
        if(other.CompareTag("Player")){
            Table.GetComponent<BoxCollider>().enabled = false;
        }
    }
    void OnTriggerExit(Collider other){
        if(other.CompareTag("Player")){
            Table.GetComponent<BoxCollider>().enabled = true;
        }
    }
}
