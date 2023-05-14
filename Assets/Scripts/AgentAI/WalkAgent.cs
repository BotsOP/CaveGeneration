using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkAgent : MonoBehaviour
{
    public GameObject squire;
    public Transform target;
    private List<Vector3Int> currentPath;
    private List<GameObject> previousObjects = new List<GameObject>();
    
    private void OnPathFound(List<Vector3Int> waypoints, bool pathSuccessful) 
    {
        if (pathSuccessful) 
        {
            for (int i = 0; i < previousObjects.Count; i++)
            {
                var tempObject = previousObjects[i];
                Destroy(tempObject);
            }
            previousObjects.Clear();

            foreach (var location in waypoints)
            {
                previousObjects.Add(Instantiate(squire, location, Quaternion.identity).gameObject);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            CavePathfinding.RequestPath(new PathRequest(transform.position, target.position, OnPathFound));
        }
    }
}
