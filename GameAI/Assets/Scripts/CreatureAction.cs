using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CAction
{
    public string id;

    public readonly ActionRequirement pre_conditions;
    public readonly ActionRequirement post_conditions;

    public delegate bool Action();

    public Action PerformAction { get; }

    public delegate Dictionary<Costable, int> CalculateCost();

    public CalculateCost CalcCost { get; }

    public bool priority;

    public CAction(ActionRequirement pre_conditions, ActionRequirement post_conditions, CalculateCost calcCost, Action performAction, string id = "unidentified", bool priority = false)
    {
        this.id = id;
        this.pre_conditions = pre_conditions;
        this.post_conditions = post_conditions;
        this.PerformAction = performAction;
        CalcCost = calcCost;
        this.priority = priority;
    }

    public override string ToString()
    {
        return id;
    }
}
