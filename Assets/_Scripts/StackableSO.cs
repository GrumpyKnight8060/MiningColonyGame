using UnityEngine;

//
// Common base for anything that can exist in stacks and appear in recipes
// (resources, items, etc). Keep this lightweight.
//
public class StackableSO : ScriptableObject
{
    [Header("Identity")]
    public string itemName;      // also used as a key/display fallback
    public string displayName;
    [Range(1, 6)] public int tier = 1;
    public string rarity;        // free text unless you later swap to an enum
    public string category;      // free text grouping

    [Header("Stacking & Tags")]
    public int maxStack = 100;
    public string[] usableBy;    // e.g., Miner/Soldier/Transporter/Technician/Explorer
    public string[] tags;        // arbitrary tags (importer appends Equip Slot here)
}
