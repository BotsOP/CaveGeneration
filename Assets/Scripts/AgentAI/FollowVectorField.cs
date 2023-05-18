using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowVectorField : MonoBehaviour
{
    public CaveVectorField caveVectorField;
    public bool follow;
    public int speed = 20;
    private void Update()
    {
        if (Input.GetKey(KeyCode.M) && Time.frameCount % speed == 0)
        {
            Vector3 moveDirection = caveVectorField.GetDirection(transform.position);
            transform.position += moveDirection;
        }

        if (Time.frameCount % speed == 0 && follow)
        {
            Vector3 moveDirection = caveVectorField.GetDirection(transform.position);
            transform.position += moveDirection;
        }
    }
    
}
