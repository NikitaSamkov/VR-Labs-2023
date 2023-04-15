using System.Collections;
using System.Collections. Generic;
using UnityEngine;
using System;

public class Gun : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float speed = 50f;
    private AudioSource clip;
    
	public static Action pistolFire;
	
	private void Start()
	{
		clip = GetComponent<AudioSource>();
	}

    // Update is called once per frame
    public void Fire()
    {
		clip.Play();
		GameObject createBullet = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);
		createBullet.GetComponent<Rigidbody>().velocity = speed * spawnPoint.forward;
		Destroy(createBullet, 5f);
		pistolFire?.Invoke();
    }
}
