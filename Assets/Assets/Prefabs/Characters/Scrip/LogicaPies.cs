using UnityEngine;

public class LogicaPies : MonoBehaviour
{
    public PlayerMove PlayerMove;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerMove.PuedoSaltar = true;
    }
    private void OnTriggerExit(Collider other)
    {
        PlayerMove.PuedoSaltar = false;
    }

}
