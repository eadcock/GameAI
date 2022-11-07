using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class Vehicle
{
    public static Vector3 Seek(Vector3 pos, Vector3 vel, Vector3 target)
    {
        Vector3 steer = Vector3.zero;
        Vector3 dis_vec = target - pos;
        if (dis_vec.magnitude > 0)
        {
            steer = dis_vec.normalized - vel;
        }
        return steer;
    }

    public static Vector3 Wander(Vector3 pos, Vector3 vel, ref float wDelta)
    {
        Vector3 dir = vel.normalized;
        Vector3 center = pos + (dir * 2.0f);

        wDelta += Random.Range(-0.1f, 0.1f);

        Vector3 offset = new(Mathf.Cos(wDelta), Mathf.Sin(wDelta));

        return Seek(pos, vel, center + offset);
    }

    public static Vector3 AvoidObstacles(Vector3 pos, Vector3 vel)
    {
        IEnumerable<GameObject> barriers = GameObject.FindGameObjectsWithTag("barrier");
        IEnumerable<float> weights = quiet.VectorUtils.CalcSqDistances(pos, barriers.Select(x => x.transform.position)).Select(x => 1.0f / (x / 2));

        var barIter = barriers.GetEnumerator();
        var weightIter = weights.GetEnumerator();

        Vector3 steer = Vector3.zero;

        while (barIter.MoveNext() && weightIter.MoveNext())
        {
            steer += -1*Mathf.Clamp(weightIter.Current, 0.0f, 5.0f)*Seek(pos, vel, barIter.Current.transform.position);
        }


        return steer;
    }

    public static Vector3 AvoidY(Vector3 pos, Vector3 vel, float y)
    {
        return -1*Mathf.Clamp(Mathf.Abs(1.0f / (y - pos.y)), 0.0f, 5.0f)*Seek(pos, vel, new Vector3(pos.x, y, pos.z));
    }
    
    public static Vector3 AvoidX(Vector3 pos, Vector3 vel, float x)
    {
        return -1*Mathf.Clamp(Mathf.Abs(1.0f / (x - pos.x)), 0.0f, 5.0f)*Seek(pos, vel, new Vector3(x, pos.y, pos.z));
    }

    public static Vector3 AvoidBounds(Vector3 pos, Vector3 vel, Vector2 max)
    {
        Vector3 steer = AvoidY(pos, vel, -max.y) + AvoidY(pos, vel, -max.y) + AvoidX(pos, vel, max.x) + AvoidX(pos, vel, -max.x);
        if (steer.sqrMagnitude < Mathf.Pow(0.08f, 2))
        {
            steer = Vector3.zero;
        }
        return steer;
    }
}
