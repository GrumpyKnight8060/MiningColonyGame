using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "_Data/Item", order = 0)]
public class ItemSO : StackableSO
{
    [Header("Equip")]
    public string slot;       // "Equip Slot" (text; you can swap to enum later)
    public string powerType;  // "Power Type" (text)

    [Header("Production / Carry")]
    public float oreProductionPerHour;
    public int itemCarryCapacityIncrease;
    public int fluidCarryCapacityIncrease;

    [Header("Defense / Movement")]
    public int defense;
    public float defenseMultiplier;
    public float movementSpeedMultiplier = 1f;
    public float fatigueMultiplier = 1f;

    [Header("Power Costs")]
    public float powerCostPerHour;
    public float powerCostPerUse;
    public float powerCostPerTile;

    [Header("Exploration")]
    public float poiDiscoveryMultiplier;

    [Header("Combat")]
    public float attackPower;   // "Atk Pwr"
    public float attacksPerSec; // "Atk / Sec"
    public float range;
    public float accuracy;
    public float critChance;
    public float critDamage;
    public float aoe;

    [Header("Maintenance / Structure Effects")]
    public float repairPerHour;
    public float repairPerHourMultiplier;
    public float structureDecayReductionMultiplier;
    public float structureEfficiencyMultiplier;

    [Header("Durability / Decay")]
    public int durability;
    public float decayPerHour;
    public float decayPerUse;
}
