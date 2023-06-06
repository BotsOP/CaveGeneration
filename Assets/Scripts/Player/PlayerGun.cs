using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlayerGun : MonoBehaviour
{
    [SerializeField] private GameObject laserObject;
    private CavePhysicsManager cavePhysicsManager;

    private void Awake()
    {
        cavePhysicsManager = FindObjectOfType<CavePhysicsManager>();
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            laserObject.SetActive(true);
            if(cavePhysicsManager.Raycast(transform.position, transform.forward * 100, out RayOutput rayOutput))
            {
                float scaleY = Vector3.Distance(transform.position, rayOutput.position);
                laserObject.transform.LookAt(rayOutput.position);
                laserObject.transform.localScale = new Vector3(laserObject.transform.localScale.x, scaleY,
                    laserObject.transform.localScale.z);
            }
        }
        else
        {
            //laserObject.SetActive(false);
        }
    }
}
