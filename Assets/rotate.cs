using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rotate : MonoBehaviour
{
    public float m_angle = 1f;
    public Vector3 m_axis = Vector3.up;

    void Update()
    {
        transform.Rotate(m_axis.normalized, m_angle);
    }
}
