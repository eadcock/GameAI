using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Flags]
public enum Conditions
{
    None        = 0b_0,
    Alive       = 0b_1,
    Injured     = 0b_10,
    Tired       = 0b_100,
    Exhausted   = 0b_1000,
    Hungry      = 0b_10000,
    Lonely      = 0b_100000,
    Fearful     = 0b_1000000,
    NearBed     = 0b_10000000,
    NearFood    = 0b_100000000,
}

[System.Serializable]
public struct ActionRequirement
{
    [SerializeField]
    private Conditions trueConditions;
    [SerializeField]
    private Conditions falseConditions;

    public ActionRequirement(Conditions t, Conditions f)
    {
        this.trueConditions = t;
        this.falseConditions = f;
    }

    public Conditions this[bool b]
    {
        get { return b ? trueConditions : falseConditions; }
    }

    /// <summary>
    /// Will perform an OR action with true conditions, and will set all falseCondition bits to 0 in start
    /// </summary>
    /// <param name="start"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static Conditions operator |(Conditions start, ActionRequirement action)
    {
        return start & ~action.falseConditions | action.trueConditions;
    }

    public static bool operator ==(Conditions first, ActionRequirement action)
    {
        return ((first & action.trueConditions) == action.trueConditions) && ((first & action.falseConditions) == Conditions.None);
    }

    public static bool operator !=(Conditions first, ActionRequirement action)
    {
        return !(first == action);
    }

    public static bool operator ==(ActionRequirement first, ActionRequirement second)
    {
        return ((first[true] & second[true]) == first[true]) && ((first[false] & second[false]) == first[false]);
    }

    public static bool operator !=(ActionRequirement first, ActionRequirement second)
    {
        return !(first == second);
    }

    public ActionRequirement Combine(ActionRequirement a2)
    {
        return new ActionRequirement(trueConditions | a2.trueConditions, falseConditions | a2.falseConditions);
    }

    public ActionRequirement GetRequired(Conditions c)
    {
        ActionRequirement ar = new();
        foreach(Conditions test in System.Enum.GetValues(typeof(Conditions))) {
            if (trueConditions.HasFlag(test) && !c.HasFlag(test))
                ar.trueConditions |= test;
            else if (falseConditions.HasFlag(test) && c.HasFlag(test))
                ar.falseConditions |= test;
        }

        return ar;
    }
}