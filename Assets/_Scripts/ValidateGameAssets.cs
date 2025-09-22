using UnityEngine;
using UnityEditor;
using System.Linq;

public static class ValidateGameAssets
{
    [MenuItem("Tools/Validate Game Assets")]
    public static void ValidateAll()
    {
        var all = Resources.LoadAll<ScriptableObject>("");

        foreach (var so in all)
        {
            if (so is IConstructible constructible)
            {
                // check that its kind matches the type
                if (so is ResourceSO && constructible.Kind != AssetKind.Resource)
                    Debug.LogError($"{so.name} should be AssetKind.Resource");

                if (so is ItemSO && constructible.Kind != AssetKind.Item)
                    Debug.LogError($"{so.name} should be AssetKind.Item");

                if (so is StructureSO && constructible.Kind != AssetKind.Structure)
                    Debug.LogError($"{so.name} should be AssetKind.Structure");

                if (so is RecipeSO && constructible.Kind != AssetKind.Recipe)
                    Debug.LogError($"{so.name} should be AssetKind.Recipe");

                if (so is BlueprintSO && constructible.Kind != AssetKind.Blueprint)
                    Debug.LogError($"{so.name} should be AssetKind.Blueprint");
            }
        }

        Debug.Log("Validation complete.");
    }
}

public interface IConstructible
{
    AssetKind Kind { get; }
}
