using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using quiet;
using System.Text;
using System.ComponentModel;

[System.Flags]
public enum Diet
{
    Herbivore,
    Carnivore,
}

public enum Age
{
    Infant = 0,
    Child,
    YoungAdult,
    Adult,
    Elderly
}

public enum Costable
{
    Health,
    MaxHealth,
    Hunger,
    Energy
}

[System.Serializable]
public class CreatureData
{
    // Movement
    public Vector3 velocity;
    public Transform transform;

    // General stats
    [SerializeField]
    private int health;
    public int Health { get => health; set { health = Mathf.Clamp(value, 0, maxHealth + 1); } }
    [SerializeField]
    private int maxHealth;
    public int MaxHealth { get => maxHealth; set { maxHealth = value < 0 ? 0 : value; } }
    [SerializeField]
    public Diet Diet { get; set; }
    [SerializeField]
    public Age Age { get; set; }

    // Organizational tag
    [SerializeField]
    public int Association { get; set; }

    [SerializeField]
    private int hunger;
    public int Hunger { get => hunger; set { hunger = Mathf.Clamp(value, 0, 10); } }
    [SerializeField]
    private int energy;
    public int Energy { get => energy; set { energy = Mathf.Clamp(value, 0, 10); } }

    public CreatureData(GameObject obj) : this()
    {
        transform = obj.transform;
    }

    public CreatureData()
    {
        velocity = Vector3.zero;
        Health = 5;
        MaxHealth = 5;
        Diet = Diet.Herbivore | Diet.Carnivore;
        Age = Age.Infant;
        Association = -1;
        Hunger = 0;
        Energy = 10;
    }

    public CreatureData(CreatureData source)
    {
        velocity = source.velocity;
        Health = source.Health;
        MaxHealth = source.MaxHealth;
        Diet = source.Diet;
        Age = source.Age;
        Association = source.Association;
        Hunger = source.Hunger;
        Energy = source.Energy;
        transform = source.transform;
    }

    public override string ToString()
    {
        StringBuilder str = new();
        foreach(PropertyDescriptor desc in TypeDescriptor.GetProperties(this))
        {
            str.AppendLine($"{desc.Name} = {desc.GetValue(this)}");
        }
        return str.ToString();
    }
}
