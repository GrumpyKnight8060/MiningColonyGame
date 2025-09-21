#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RecipeInput))]
public class RecipeInputDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var ingProp = property.FindPropertyRelative("ingredient");
        var amtProp = property.FindPropertyRelative("amount");

        // Split line: 75% for object, 25% for int
        var objRect = new Rect(position.x, position.y, position.width * 0.75f - 2, position.height);
        var amtRect = new Rect(objRect.xMax + 4, position.y, position.width * 0.25f - 2, position.height);

        EditorGUI.ObjectField(objRect, ingProp, GUIContent.none);
        amtProp.intValue = EditorGUI.IntField(amtRect, amtProp.intValue < 1 ? 1 : amtProp.intValue);
    }
}
#endif
