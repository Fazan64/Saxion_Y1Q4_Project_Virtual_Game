﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorActivationScript : MonoBehaviour
{

    [SerializeField] private GameObject[] doorPieces;
    [SerializeField] private float maxDegreesPerSecond= 2;
    [HideInInspector] public bool isActivated = false;

    private Animator animator;

    void Start()
    {
        //Activate();
        animator = GetComponent<Animator>();
       
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.T))
        {
            isActivated = true;
        }

        if(isActivated)
        {
            ResetRotation();
        }
    }

    public void Activate()
    {
        isActivated = true;
    }

    private void ResetRotation()
    {
        foreach (GameObject doorPiece in doorPieces)
        {
            doorPiece.transform.localRotation = Quaternion.RotateTowards(doorPiece.transform.localRotation, Quaternion.identity, maxDegreesPerSecond * Time.deltaTime);
            if (doorPiece.transform.localRotation == Quaternion.identity)
            {
                animator.enabled = true;
            }
        }
    }
}
