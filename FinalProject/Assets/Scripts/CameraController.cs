using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float sensitivity = 1.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 position = transform.position;
        if(Input.GetKey(KeyCode.W))
        {
            position.y += sensitivity * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            position.y -= sensitivity * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.A))
        {
            position.x -= sensitivity * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            position.x += sensitivity * Time.deltaTime;
        }

        transform.position = position;
    }
}
