using UnityEngine;

// Base for anything that can live in inventory (resource OR item)
public abstract class StackableSO : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    [Range(1, 9)] public int tier = 1;
    public int maxStack = 9999;
}
