using System.Collections;
using System.Collections.Generic;
using Managers;
using UnityEngine;
using EventType = Managers.EventType;

public class PlayerCarver : MonoBehaviour
{
    [SerializeField, Range(0.1f, 16f)] private float carveSize;
    [SerializeField, Range(0.001f, 0.1f)] private float carveSpeed;
    [SerializeField, Range(0, 100)] private float carveDistance;
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            MyRay ray = new MyRay(transform.position, transform.forward * carveDistance);
            EventSystem<MyRay, float, float>.RaiseEvent(EventType.CARVE_TERRAIN, ray, carveSize, carveSpeed);
        }
    }
}

public struct MyRay
{
    public Vector3 origin;
    public Vector3 direction;

    public MyRay(Vector3 _origin, Vector3 _direction)
    {
        origin = _origin;
        direction = _direction;
    }
}

