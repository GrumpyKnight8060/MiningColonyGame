// Assets/_Scripts/RecipeSO.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Recipe")]
public class RecipeSO : ScriptableObject, IConstructible
{
    // Crafting stations (names used in CSVs/UI)
    public enum Station
    {
        BasicAssembler,
        IndustrialAssembler,
        AutomatedAssembler,
        MassAssembler,

        StoneSmelter,
        ElectricSmelter,
        BlastFurnace,
        QuantumSmelter,

        BasicRefinery,
        IndustrialRefinery,
        MassRefinery,

        ChemPlant
    }

    [System.Serializable]
    public class Input
    {
        public StackableSO item; // ResourceSO or ItemSO
        [Min(1)] public int amount = 1;
    }

    [System.Serializable]
    public class Output
    {
        // Allowed: ResourceSO, ItemSO, StructureSO
        public Object target;
        [Min(1)] public int amount = 1;
    }

    [Header("Recipe")]
    public string recipeName;
    [Range(1, 6)] public int tier = 1;

    public List<Station> stations = new List<Station>() { Station.BasicAssembler };

    public List<Input> inputs = new List<Input>();
    public List<Output> outputs = new List<Output>();

    [Min(0.1f)] public float craftTimeSeconds = 3f;
    public bool unlockedByDefault = false;

    // Optional research knobs
    public int researchTier = -1; // -1 => mirror 'tier'
    [Range(0f, 1f)] public float researchWeight = 0.5f;

    public AssetKind Kind => AssetKind.Recipe;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Mirror research tier if unset
        if (researchTier <= 0) researchTier = tier;

        // Enforce output types: ResourceSO / ItemSO / StructureSO only
        for (int i = outputs.Count - 1; i >= 0; i--)
        {
            var o = outputs[i].target;
            if (o == null) continue;
            if (o is ResourceSO || o is ItemSO || o is StructureSO) continue;

            Debug.LogWarning($"[RecipeSO] '{name}' has invalid output '{o.name}' ({o.GetType().Name}). " +
                             "Only ResourceSO, ItemSO, or StructureSO are allowed. Removing.", this);
            outputs.RemoveAt(i);
        }
    }
#endif
}
