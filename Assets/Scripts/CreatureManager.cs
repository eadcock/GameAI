using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using quiet;
using System;
using System.Threading.Tasks;
using System.Linq;

public class CreatureManager : MonoBehaviour
{
    [Range(0, 10)]
    public static int startingCreatures = 10;

    [Range(0, 10)]
    public static int maxCreatures = 10;

    [Range(0, 20)]
    public static int startingPlants = 10;

    public static Dictionary<int, Creature> creatures;

    public static Stack<int> toRemove = new();
    public static Stack<IDamagable> toDamage = new();
    public static IEnumerable<GameObject> GetCreatureObjects() => creatures.Select(kv => kv.Value.obj);

    [SerializeField]
    GameObject creaturePrefab;

    [SerializeField]
    GameObject bedPrefab;

    [SerializeField]
    GameObject plantPrefab;


    // Start is called before the first frame update
    void Start()
    {
        creatures = new Dictionary<int, Creature>(maxCreatures);
        Debug.Log(creaturePrefab);
        for (int i = 0; i < startingCreatures; i++)
        {
            Creature creature = new(UnityUtils.Spawn(creaturePrefab, VectorUtils.GetRandomPoint_2D((-20.0f, 20.0f), (-20.0f, 20.0f))));
            creature.MyBed = quiet.UnityUtils.Spawn(bedPrefab, VectorUtils.GetRandomPoint_2D((-20.0f, 20.0f), (-20.0f, 20.0f)));
            creatures.Add(creature.obj.GetInstanceID(), creature);
            Debug.Log(creature.obj.GetInstanceID());
            creature.data.Association = i % 2;
        }

        for(int i = 0; i < startingPlants; i++)
        {
            UnityUtils.Spawn(plantPrefab, VectorUtils.GetRandomPoint_2D((-50.0f, 50.0f), (-50.0f, 50.0f)));
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown)
        {
            var state = creatures.First().Value.GetState();
            Debug.Log(state);
            Debug.Log(Convert.ToString((int)state, 2).PadLeft(8, '0'));
        }

        while (toDamage.Count > 0)
        {
            toDamage.Pop().Damage();
        }

        while (toRemove.Count > 0)
        {
            int i = toRemove.Pop();
            Destroy(creatures[i].MyBed);
            Destroy(creatures[i].obj);
            creatures.Remove(i);
            Debug.Log(creatures.Count);
        }

        foreach(Creature creature in creatures.Values)
        {
            if(creature != null)
            {
                creature.Update();
            }
        }
    }

    public void AgeCreature(CreatureData data)
    {
        data.Age += 1;
    }

    private void OnApplicationQuit()
    {
        foreach(Creature creature in creatures.Values)
        {
            creature.Dispose();
        }
    }

    public static void Remove(int index)
    {
        if(creatures.ContainsKey(index))
            toRemove.Push(index);
    }

    public static void Damage(IDamagable d)
    {
        toDamage.Push(d);
    }
}
