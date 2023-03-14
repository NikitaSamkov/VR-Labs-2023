using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tilt : MonoBehaviour
{
    private bool isTilted;
    [SerializeField] private ParticleSystem particleSystemPref;

    private void Start()
    {
        particleSystemPref.Stop();
    }

    void Update()
    {

        if (!isTilted && Vector3.Dot(transform.up, Vector3.up) <= 0)
        {
            particleSystemPref.Play();
            isTilted = true;
        }
        else if (isTilted && Vector3.Dot(transform.up, Vector3.up) > 0) 
        {
            particleSystemPref.Stop();
            isTilted = false;
        }
    }
}
