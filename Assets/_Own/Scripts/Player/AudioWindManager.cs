﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.Characters.FirstPerson;

#pragma warning disable 0649

/// Adjusts the volume of a given audioSource to play a wind sound when moving fast.
public class AudioWindManager : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] RigidbodyFirstPersonController playerController;
    [SerializeField] bool playOnlyWhenInAir = true;
    [Space]
    [Tooltip("Volume is 0 when moving slower than this.")]
    [SerializeField] float minSpeed = 1f;
    [Tooltip("Volume is `maxVolume` when moving faster than this.")]
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float maxVolumeChangePerSecond = 2f;
    [SerializeField] [Range(0f, 1f)] float maxVolume = 1f;

    // Use this for initialization
    void Start()
    {
        Assert.IsNotNull(audioSource);
        Assert.IsNotNull(playerController);

        audioSource.loop = true;
        if (!audioSource.isPlaying) audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        audioSource.volume = Mathf.MoveTowards(
            audioSource.volume, 
            GetDesiredVolume(),
            maxVolumeChangePerSecond * Time.deltaTime
        );
    }

    private float GetDesiredVolume()
    {
        if (playOnlyWhenInAir && playerController.isGrounded) return 0f;
        float speed = playerController.velocity.magnitude;

        return Mathf.Clamp01(Mathf.InverseLerp(minSpeed, maxSpeed, speed)) * maxVolume;
    }
}
