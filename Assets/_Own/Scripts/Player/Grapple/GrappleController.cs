﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityStandardAssets.Characters.FirstPerson;

#pragma warning disable 0649

/// Handles grapple-related player input. 
public class GrappleController : MonoBehaviour
{
    [SerializeField] Grapple grappleLeft;
    [SerializeField] Grapple grappleRight;

    [SerializeField] Image crosshairIndicator;
    [SerializeField] Sprite crosshairAimedSprite;
    [SerializeField] Sprite crosshairDefaultSprite;

    [SerializeField] float grappleShootSpeed = 100f;
    [SerializeField] float grappleMinRange = 0.6f;
    [SerializeField] float grappleMaxRange = 40f;
    [SerializeField] float targetDetectionSphereRadius = 0.1f;
    [SerializeField] LayerMask targetDetectionLayerMask = Physics.DefaultRaycastLayers;

    [Tooltip("Meters per second")]
    [SerializeField] float grapplePullingSpeed = 2f;

    [SerializeField] RigidbodyFirstPersonController firstPersonController;

    public Grapple leftGrapple  { get { return grappleLeft;  } }
    public Grapple rightGrapple { get { return grappleRight; } }

    void Start()
    {
        Assert.IsNotNull(grappleLeft);
        Assert.IsNotNull(grappleRight);
        Assert.IsNotNull(firstPersonController);
    }

    void Update()
    {
        RetractIfNeeded();
        ShootIfNeeded();
        PullIfNeeded();
    }

    private void RetractIfNeeded()
    {
        if (Input.GetButtonUp("Fire1"))
        {
            grappleLeft.Retract();
        }

        if (Input.GetButtonUp("Fire2"))
        {
            grappleRight.Retract();
        }
    }

    private void ShootIfNeeded()
    {
        Vector3 targetPosition;
        if (!CheckCanShootGrapple(out targetPosition)) return;

        if (Input.GetButtonDown("Fire1"))
        {
            grappleLeft.Shoot(targetPosition, grappleShootSpeed);
        }

        if (Input.GetButtonDown("Fire2"))
        {
            grappleRight.Shoot(targetPosition, grappleShootSpeed);
        }
    }

    private void PullIfNeeded()
    {
        if (firstPersonController.isGrounded) return;

        // FIXME Hardcoded keycodes
        bool shouldPullLeft  = Input.GetButton("Grapple Pull Left");// Input.GetKey(KeyCode.Q);
        bool shouldPullRight = Input.GetButton("Grapple Pull Right");// Input.GetKey(KeyCode.E);
        bool shouldPullAtLeastOne = shouldPullLeft || shouldPullRight;

        bool onlyOneGrappleConnected = grappleLeft.isConnected != grappleRight.isConnected;
        if (onlyOneGrappleConnected && shouldPullAtLeastOne) 
        {
            shouldPullLeft = shouldPullRight = true;
        }

        if (shouldPullLeft && grappleLeft.isConnected)
        {
            grappleLeft.ropeLength -= grapplePullingSpeed * Time.deltaTime;
        }

        if (shouldPullRight && grappleRight.isConnected)
        {
            grappleRight.ropeLength -= grapplePullingSpeed * Time.deltaTime;
        }
    }

    private bool CheckCanShootGrapple(out Vector3 targetPosition)
    {
        Transform cameraTransform = Camera.main.transform;
        var ray = new Ray(cameraTransform.position, cameraTransform.forward);

        RaycastHit hit;
        bool didHit = Physics.SphereCast(
            ray,
            targetDetectionSphereRadius,
            out hit,
            grappleMaxRange,
            targetDetectionLayerMask,
            QueryTriggerInteraction.Ignore
        );

        if (didHit)
        {
            if (hit.distance < grappleMinRange)
            {
                SetCrosshairMode(false);
                targetPosition = Vector3.zero;
                return false;
            }

            SetCrosshairMode(true);
            targetPosition = hit.point;
            return true;
        }

        SetCrosshairMode(false);
        targetPosition = ray.GetPoint(grappleMaxRange);
        return true;

        /*crosshairIndicator.color = Color.gray;
        targetPosition = Vector3.zero;
        return false;*/
    }

    private void SetCrosshairMode(bool isActive)
    {
        if (crosshairIndicator == null) return;
       // crosshairIndicator.sprite = isActive ? crosshairAimedSprite : crosshairDefaultSprite;
        var tempColor = crosshairIndicator.color;

        if(isActive)
        {
            crosshairIndicator.sprite = crosshairAimedSprite;
            tempColor.a = 1f;
        }
        else
        {
            crosshairIndicator.sprite = crosshairDefaultSprite;
            tempColor.a = 0.6f;
        }

        crosshairIndicator.color = tempColor;
    }
}
