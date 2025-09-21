using UnityEngine;

[CreateAssetMenu(fileName = "NewResource", menuName = "Game/Resource", order = 0)]
public class ResourceSO : StackableSO
{
    public bool isRefined;                 // bars, plastics, etc.
    public int baseValue = 1;              // trade value
    [Range(0f, 1f)] public float rarityWeight = 1f; // used for site rolls (ignore for refined)
}
