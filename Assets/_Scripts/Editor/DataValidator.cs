#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class DataValidator
{
    [MenuItem("Tools/Validate All Objects")]
    public static void ValidateAll()
    {
        var allObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();

        foreach (var obj in allObjects)
        {
            if (obj is RecipeSO recipe)
            {
                // Output must not be another Recipe or Blueprint
                if (recipe.output is RecipeSO || recipe.output is BlueprintSO)
                {
                    Debug.LogError($"Invalid output in Recipe {recipe.name}: Cannot output {recipe.output.name}");
                }

                // (Optional) Check inputs too
                foreach (var input in recipe.inputs)
                {
                    if (input is RecipeSO || input is BlueprintSO)
                    {
                        Debug.LogError($"Invalid input in Recipe {recipe.name}: Cannot use {input.name}");
                    }
                }
            }
        }

        Debug.Log("Validation complete.");
    }
}
#endif
