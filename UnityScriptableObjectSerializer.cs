using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// most sane json serializer in unity here
public class UnityScriptableObjectSerializer : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var scriptableObject = value as ScriptableObject;
        if (scriptableObject == null)
        {
            writer.WriteNull();
            return;
        }

        // Serialize using JsonUtility
        string json = JsonUtility.ToJson(scriptableObject);

        // Parse the JSON string with Newtonsoft.Json
        var jObject = JObject.Parse(json);

        // Traverse and remove fields containing 'instanceID'
        RemoveFieldsWithInstanceID(jObject);

        // Write the modified JSON
        jObject.WriteTo(writer);
    }

    private void RemoveFieldsWithInstanceID(JToken token)
    {
        // Collect a list of JTokens to remove
        var tokensToRemove = new List<JToken>();

        // Recursively find all JTokens that are parent objects of 'instanceID' properties
        CollectTokensToRemove(token, tokensToRemove);

        // Remove each collected JToken from its parent
        foreach (var tok in tokensToRemove)
        {
            tok.Remove();
        }
    }

    private void CollectTokensToRemove(JToken token, List<JToken> tokensToRemove)
    {
        if (token.Type == JTokenType.Object)
        {
            // Iterate through all children of the JObject
            var children = token.Children<JProperty>().ToList(); // ToList to avoid modifying the collection while iterating
            foreach (var child in children)
            {
                if (child.Value is JObject childObject && childObject["instanceID"] != null)
                {
                    // If the child is an object and has 'instanceID', add the child to the list
                    tokensToRemove.Add(child);
                }
                else
                {
                    // Otherwise, recursively search in the child
                    CollectTokensToRemove(child.Value, tokensToRemove);
                }
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            // If the token is an array, recursively search each element
            foreach (var child in token.Children())
            {
                CollectTokensToRemove(child, tokensToRemove);
            }
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var json = JToken.Load(reader).ToString();
        return JsonUtility.FromJson(json, objectType);
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(ScriptableObject).IsAssignableFrom(objectType);
    }
}

