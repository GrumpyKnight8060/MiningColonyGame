// Assets/_Scripts/StackableSO.cs
using UnityEngine;

public class StackableSO : ScriptableObject
{
    [Header("Common")]
    public string displayName;
    [Range(1, 6)] public int tier = 1;
    [Min(1)] public int maxStack = 100;
}
