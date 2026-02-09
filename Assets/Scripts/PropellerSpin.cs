using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public float rotationSpeedY = 90f; // degrees per second

    void Update()
    {
        transform.Rotate(0f, rotationSpeedY * Time.deltaTime, 0f, Space.Self);
    }
}
