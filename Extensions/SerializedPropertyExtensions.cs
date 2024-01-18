#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Magix.Editor
{
    public static class SerializedPropertyExtensions
    {
        public static bool IsTypePrimitive(this SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    break;
                case SerializedPropertyType.Boolean:
                    break;
                case SerializedPropertyType.Float:
                    break;
                case SerializedPropertyType.String:
                    break;
                case SerializedPropertyType.Enum:
                    break;
                case SerializedPropertyType.Color:
                    break;
                default:
                    return false;
            }

            return true;
        }

        public static void SetValue(this SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = (int)value;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = (int)value;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                default:
                    Logger.LogError("Type not implemented for: " + property.propertyType);
                    break;
            }

            property.serializedObject.ApplyModifiedProperties();
        }

        public static object GetValue(this SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex;
                case SerializedPropertyType.Color:
                    return property.colorValue;
                default:
                    Logger.LogError("Type not implemented for: " + property.propertyType);
                    return null;
            }
        }
    }
}
#endif
