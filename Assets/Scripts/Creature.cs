using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using System.Linq;
using quiet;
using System.Timers;
using System;

#nullable enable

[System.Serializable]
public class Creature : IDisposable, IDamagable
{
    [SerializeField]
    public GameObject obj;

    [SerializeField]
    public CreatureData data;

    public Vector3 Position { get => obj.transform.position; set => obj.transform.position = value; }

    [SerializeField]
    public ActionRequirement Objective;

    [SerializeField]
    public CAction CurrentAction;

    public CAction caSleep;

    public Dictionary<string, CAction> CreatureActions { get; set; }

    private float wDelta = 0;

    [SerializeField]
    public GameObject MyBed { get; set; }

    public delegate void OnActionCleanup();

    public event OnActionCleanup ActionCleanup;

    public bool Grappled { get; set; }

    public readonly string Id;

    public Creature(GameObject obj)
    {
        this.obj = obj;
        data = new CreatureData(obj);
        var d = this.obj.AddComponent<ExposeData>();
        d.creatureData = data;
        d.metadata = this;
        plan = new();
        Grappled = false;

        Id = Guid.NewGuid().ToString();

        CreatureActions = new();

        CreatureActions.Add("wander",
            new CAction(
                new ActionRequirement(Conditions.Alive, Conditions.Tired),
                new ActionRequirement(Conditions.None, Conditions.None),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Energy, 1);
                    return costs;
                },
                Wander,
                "wander"
            ));

        CreatureActions.Add("goto(bed)",
            new CAction(
                new ActionRequirement(Conditions.Alive, Conditions.NearBed | Conditions.Exhausted),
                new ActionRequirement(Conditions.NearBed, Conditions.None),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Energy, 1);
                    return costs;
                },
                SeekBed,
                "goto(bed)"
            ));

        CreatureActions.Add("sleep",
            new CAction(
                new ActionRequirement(Conditions.Alive | Conditions.NearBed, Conditions.None),
                new ActionRequirement(Conditions.None, Conditions.Exhausted | Conditions.Tired),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Hunger, 2);
                    return costs;
                },
                Sleep,
                "sleep"
            ));
        CreatureActions.Add("goto(food)",
            new CAction(
                new ActionRequirement(Conditions.Alive, Conditions.Exhausted | Conditions.NearFood),
                new ActionRequirement(Conditions.NearFood, Conditions.None),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Energy, 5);
                    return costs;
                },
                SeekFood,
                "goto(food)"
                )
            );
        CreatureActions.Add("eat",
            new CAction(
                new ActionRequirement(Conditions.Alive | Conditions.NearFood, Conditions.Fearful),
                new ActionRequirement(Conditions.None, Conditions.Hungry),
                () => new Dictionary<Costable, int>(),
                EatFood,
                "eat"
                )
            );
        CreatureActions.Add("flee",
            new CAction(
                new ActionRequirement(Conditions.Alive | Conditions.Fearful, Conditions.Exhausted),
                new ActionRequirement(Conditions.None, Conditions.Fearful),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Energy, 5);
                    return costs;
                },
                FleeDanger,
                "flee",
                true
                )
            );
        CreatureActions.Add("die",
            new CAction(
                new ActionRequirement(Conditions.None, Conditions.Alive),
                new ActionRequirement(Conditions.Alive, Conditions.None),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    // Negative to force priority over all other actions
                    costs.Add(Costable.Energy, -1000);
                    return costs;
                },
                Die,
                "die",
                true
                )
            );
        CreatureActions.Add("meditate",
            new CAction(
                new ActionRequirement(Conditions.Alive | Conditions.Injured, Conditions.Fearful | Conditions.Hungry | Conditions.Tired),
                new ActionRequirement(Conditions.None, Conditions.Injured),
                () =>
                {
                    Dictionary<Costable, int> costs = new();
                    costs.Add(Costable.Energy, 3);
                    costs.Add(Costable.Hunger, 2);
                    return costs;
                },
                Meditate,
                "meditate"
                )
            );

        CurrentAction = CreatureActions["wander"];

        Objective = new ActionRequirement(Conditions.Alive, Conditions.Tired | Conditions.Exhausted | Conditions.Hungry | Conditions.Injured | Conditions.Fearful);
    }

    public override string ToString()
    {
        StringBuilder str = new();
        str.AppendLine($"id: {obj.GetInstanceID()}");
        str.AppendLine(data.ToString());
        return str.ToString();
    }

    public Conditions GetState()
    {
        Conditions state = Conditions.None;

        if (data.Health > 0) state |= Conditions.Alive;

        if (data.Health < data.MaxHealth) state |= Conditions.Injured;

        if (data.Energy <= 3) state |= Conditions.Tired;
        if (data.Energy == 0) state |= Conditions.Exhausted;

        if (data.Hunger >= 5) state |= Conditions.Hungry;

        GameObject[] surroundingCreatures = quiet.VectorUtils.FindCloseObjects(obj, CreatureManager.GetCreatureObjects().Where(go => go.GetInstanceID() != obj.GetInstanceID()), 10.0f).ToArray();
        
        if (surroundingCreatures.Length == 0 || data.Association == 0) 
            state |= Conditions.Lonely;
        else
        {
            foreach (GameObject cObj in surroundingCreatures)
            {
                if (CreatureManager.creatures[cObj.GetInstanceID()].data.Association != data.Association)
                    state |= Conditions.Lonely;
                if (CreatureManager.creatures[cObj.GetInstanceID()].data.Diet.HasFlag(Diet.Carnivore))
                    state |= Conditions.Fearful;
            }
        }

        return state;
    }

    public void Update()
    {
        Conditions currentState = GetState();
        if(!Grappled)
        { 
            // If our state has changed, update our plan
            if (currentState != plannedForCondition)
            {
                ActionCleanup?.Invoke();
                Plan();
            }

            // PerformAction returns true when we need to initiate the next action
            if (CurrentAction.PerformAction())
            {
                Debug.Log($"{CurrentAction} Ending.");
                // First, cleanup the previous action
                ActionCleanup?.Invoke();
                // If we have reached the end of our plan and are content, wander the area. Otherwise, onwards to the next action.
                CurrentAction = plan.Count == 0 ? CreatureActions["wander"] : plan.Pop();
            }

            // Clamp max speed
            data.velocity = Vector3.ClampMagnitude(data.velocity, 1.0f);

            obj.transform.position = obj.transform.position + (data.velocity * (Time.deltaTime * 100));

            // Collapse on the ground if we are exhausted
            if (GetState().HasFlag(Conditions.Exhausted) && CurrentAction.id != "sleep")
            {
                ActionCleanup?.Invoke();
                CurrentAction = CreatureActions["sleep"];
                plan.Clear();
            }
        }
    }

    /// <summary>
    /// pre-conditions: ~Alive
    /// post-conditions: Alive
    /// This is a special action that does not have any post conditions. The one condition it has is a psuedo-condition, so the algorithm will run this method when the creature dies.
    /// </summary>
    /// <returns></returns>
    private bool Die()
    {
        Debug.Log("Oo owie i'm dead");
        CreatureManager.Remove(obj.GetInstanceID());

        return false;
    }

    private Timer ActionTimer = new(2000);

    private bool Wander()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.white;
        if(ActionTimer.Enabled == false)
        {
            ActionTimer.Elapsed += EnergyDrain;
            ActionTimer.Interval = 1000;
            ActionTimer.Enabled = true;

            ActionCleanup += WanderCleanup;
        }

        data.velocity = Vehicle.Wander(obj.transform.position, data.velocity, ref wDelta).normalized * 0.1f;
        

        // End condition
        if (GetState() != Objective)
        {
            Plan();
            return true;
        }

        return false;
    }

    private void WanderCleanup()
    {
        try
        {

            // Clean up
            ActionTimer.Elapsed -= EnergyDrain;
            ActionTimer.Enabled = false;

            data.velocity = Vector3.zero;

            ActionCleanup -= WanderCleanup;
        } catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    private void EnergyDrain(System.Object source, ElapsedEventArgs e)
    {
        data.Energy -= 1;
        Debug.Log($"Energy Drain: {data.Energy}");
    }

    private void HungerDrain(System.Object source, ElapsedEventArgs e)
    {
        data.Hunger += 1;
    }

    private bool Sleep()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.black;
        if (ActionTimer.Enabled == false)
        {
            ActionTimer.Interval = 2000;
            ActionTimer.Elapsed += SleepOnTick;
            ActionTimer.Elapsed += HungerDrain;
            ActionTimer.Enabled = true;

            ActionCleanup += SleepCleanup;
        }

        return data.Energy == 10;
    }

    private void SleepCleanup()
    {
        ActionTimer.Elapsed -= SleepOnTick;
        ActionTimer.Elapsed -= HungerDrain;
        ActionTimer.Stop();

        ActionCleanup -= SleepCleanup;
    }

    private void SleepOnTick(System.Object source, ElapsedEventArgs e)
    {
        Debug.Log($"Zzz: {data.Energy}");
        data.Energy += 2;
    }

    private bool SeekBed()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.gray;
        if (!ActionTimer.Enabled)
        {
            ActionTimer.Interval = 1500;
            ActionTimer.Elapsed += EnergyDrain;
            ActionTimer.Enabled = true;

            ActionCleanup = WanderCleanup;
        }

        // End condition: Do cleanup and signal to do next action
        if(quiet.VectorUtils.IsCloseVec3(Position, MyBed.transform.position, 1.0f))
        {
            data.velocity = Vector3.zero;
            obj.transform.position = MyBed.transform.position;
            return true;
        }

        data.velocity = Vehicle.Seek(Position, data.velocity, MyBed.transform.position).normalized * 0.1f;
        return false;
    }

    private IEnumerable<GameObject>? cachedPlants;
    private IEnumerable<GameObject>? cachedCreatures;
    private GameObject? foodTarget;
    private IDamagable foodDamage;
    /// <summary>
    /// Pre-conditions: Alive | ~Tired | ~NearFood
    /// Post-conditions: NearFood
    /// </summary>
    /// <returns></returns>
    private bool SeekFood()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.red;
        if (ActionTimer.Enabled == false)
        {
            ActionTimer.Elapsed += EnergyDrain;
            ActionTimer.Interval = 1000;
            ActionTimer.Enabled = true;

            ActionCleanup += WanderCleanup;
        }

        if(foodTarget == null)
        {
            List<GameObject> foodList = new();
            cachedPlants = GameObject.FindGameObjectsWithTag("Plant");
            cachedCreatures ??= CreatureManager.creatures.Where(v => v.Value.data.Association != data.Association).Select(v => v.Value.obj);

            if (data.Diet.HasFlag(Diet.Herbivore))
            {
                foodList.AddRange(cachedPlants.Where(go => VectorUtils.IsCloseVec3(Position, go.transform.position, 30.0f)));
            }
            if (data.Diet.HasFlag(Diet.Carnivore))
            {
                foodList.AddRange(cachedCreatures);
            }

            if(foodList.Any())
            {
                foodTarget = VectorUtils.FindClosest(obj, foodList);
                foodDamage = CreatureManager.creatures.ContainsKey(foodTarget.GetInstanceID())
                    ? CreatureManager.creatures[foodTarget.GetInstanceID()]
                    : foodTarget.GetComponent<Plant>();
            }
            else
            {
                data.velocity = Vehicle.Wander(Position, data.velocity, ref wDelta).normalized * 0.1f;
                return false;
            }
        } 

        data.velocity = Vehicle.Seek(Position, data.velocity, foodTarget.transform.position).normalized * 0.13f;

        return VectorUtils.IsCloseVec3(Position, foodTarget.transform.position, 1.0f);
    }

    private bool EatFood()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.magenta;
        if (foodTarget == null)
        {
            return true;
        }
        if (ActionTimer.Enabled == false)
        {
            ActionTimer.Interval = 750;
            ActionTimer.Elapsed += HungerLowerTick;
            ActionTimer.Enabled = true;

            ActionCleanup += EatCleanup;

            if(foodTarget != null && foodTarget.CompareTag("Creature") && CreatureManager.creatures.ContainsKey(foodTarget.GetInstanceID()))
            {
                CreatureManager.creatures[foodTarget.GetInstanceID()].Grappled = true;
            }
        }
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (data.Hunger <= 2 || !VectorUtils.IsCloseVec3(Position, foodTarget.transform.position, 1.0f))
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        {
            if (foodTarget != null && foodTarget.CompareTag("Creature"))
            {
                CreatureManager.creatures[foodTarget.GetInstanceID()].Grappled = false;
            }

            foodTarget = null;
            return true;
        }
        return false;
    }

    private void HungerLowerTick(System.Object source, ElapsedEventArgs e)
    {
        CreatureManager.Damage(foodDamage);

        Debug.Log($"Hunger decrease: {data.Hunger}");
        data.Hunger -= 1;
    }

    private void EatCleanup()
    {
        try
        {
            ActionTimer.Enabled = false;
            ActionTimer.Elapsed -= HungerLowerTick;

        } catch(Exception e)
        {
            Debug.Log(e);
        }
    }

    IEnumerable<GameObject>? fears;
    private bool FleeDanger()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.blue;
        if (fears == null)
        {
            fears = CreatureManager.creatures.Values.Where(v => v.data.Association != data.Association).Select(v => v.obj);
        }

        if (!fears.Any())
        {
            fears = null;
            return true;
        }

        if(!ActionTimer.Enabled)
        {
            // Swap wandering direction
            wDelta += 180;
            ActionTimer.Interval = 1100;
            ActionTimer.Elapsed += EnergyDrain;
            ActionTimer.Enabled = true;

            ActionCleanup += WanderCleanup;
        }


        var considered = VectorUtils.FindCloseObjects(obj, fears, 10.0f);

        Debug.Log(considered.Count());

        if (!considered.Any())
        {
            return true;
        }

        data.velocity = considered
                .Select(v => -Vehicle.Seek(Position, data.velocity, v.transform.position))
                .Aggregate((a, b) => a + b).normalized * 0.1f;

        return false;
    }

    private bool Meditate()
    {
        obj.GetComponent<SpriteRenderer>().color = Color.green;
        if (ActionTimer.Enabled == false)
        {
            ActionTimer.Interval = 3000;
            ActionTimer.Elapsed += Heal;
            ActionTimer.Start();

            ActionCleanup += MeditateCleanup;
        }

        Conditions state = GetState();

        return state.HasFlag(Conditions.Tired) || state.HasFlag(Conditions.Hungry) || data.Health == data.MaxHealth;
    }

    private void MeditateCleanup()
    {
        ActionTimer.Elapsed -= Heal;
        ActionTimer.Enabled = false;
    }

    private void Heal(System.Object s, ElapsedEventArgs e)
    {
        data.Health += 2;
        data.Energy -= 2;
        data.Hunger -= 1;
    }

    Stack<CAction> plan;
    Conditions plannedForCondition;
    private void Plan()
    {
        System.Diagnostics.Stopwatch stopwatch = new();
        stopwatch.Start();
        Conditions currentState = GetState();
        plannedForCondition = currentState;
        Debug.Log("Creating plan...");
        ActionRequirement required = Objective.GetRequired(currentState);

        plan = new();

        Conditions workingState = currentState;
        // We should never loop through this more than like, 10 times
        for(int i = 0; i < 100; i++)
        {
            // Find the actions that either turn on or off desired conditions
            List<CAction> desirable = CreatureActions.Values.Where(v =>
            {
                return (v.post_conditions[true] & required[true]) != Conditions.None
                    || (v.post_conditions[false] & required[false]) != Conditions.None;
            }).ToList();

            if(!desirable.Any())
            {
                Debug.LogError($"There is nothing I can do! {required[true]}/{required[false]}");
                break;
            }

            var costs = desirable.Select(ca => (ca, ca.CalcCost().Values.Any() ? ca.CalcCost().Values.Aggregate((a, b) => a + b) : 0)).ToList();
            costs.Sort((x, y) =>
            {
                if (x.ca.priority)
                {
                    if(y.ca.priority)
                        return x.Item2.CompareTo(y.Item2);
                    return 1;
                } else
                {
                    if(y.ca.priority)
                        return -1;
                    return x.Item2.CompareTo(y.Item2);
                }
            });
            CAction cheapest = costs[0].ca;

            workingState |= cheapest.post_conditions;
            plan.Push(cheapest);

            required = cheapest.pre_conditions.GetRequired(workingState);

            Debug.Log($"Added {cheapest} to the plan");

            if (currentState == required) break;
        }

        stopwatch.Stop();
        //Debug.Log($"Created a plan in {stopwatch.Elapsed.TotalMilliseconds} ms");
    }

    public void Damage(int d = 1)
    {
        Debug.Log("Taking Damage");
        data.Health -= d;
    }

    public void Dispose()
    {
        ActionTimer.Dispose();
    }
}
