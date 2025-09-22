// Assets/_Scripts/ItemSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : StackableSO, IConstructible
{
    public string category; // e.g. Armor, Optics, Pack, Component, etc.

    public AssetKind Kind => AssetKind.Item;
}
