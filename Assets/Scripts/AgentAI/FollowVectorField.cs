using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowVectorField : MonoBehaviour
{
    public CaveVectorField caveVectorField;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            Vector3 moveDirection = caveVectorField.GetDirection(transform.position);
            Debug.Log(moveDirection);
            transform.position += moveDirection;
        }
    }
    
}
