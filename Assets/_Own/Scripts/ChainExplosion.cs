﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChainExplosion : MonoBehaviour
{
    [SerializeField] private float explosionForce = 5;
    [SerializeField] private float explosionRadius = 8;
    [SerializeField] private float upwardsModifier = 3;
    [SerializeField] private float torque = 7;
    private Rigidbody rb;
   
    public void ExplodeChains()
    {
        foreach (Transform child in transform)
        {
            if(child.CompareTag("CheckpointChain"))
            {
                rb = child.gameObject.AddComponent<Rigidbody>();
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardsModifier, ForceMode.Impulse);
                rb.AddTorque(transform.up * torque,ForceMode.Impulse);
                Destroy(child.gameObject, 2);
            }
            //if(child.CompareTag("CheckpointHolder"))
            //{
            //    child.gameObject.AddComponent<Rigidbody>().useGravity = true;
            //}
        }
    }
}
