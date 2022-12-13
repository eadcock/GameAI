using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plant : MonoBehaviour, IDisposable, IDamagable
{
    public int health;

    private System.Timers.Timer timer = new(5000);

    public void Dispose()
    {
        timer.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        health = 15;

        timer.Elapsed += (_, _) =>
        {
            if (health < 15)
            {
                health++;
            }
        };
    }

    public void Damage(int d = 1)
    {
        health -= d;

        if(health == 0)
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }


}
