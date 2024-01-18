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

        string json = JsonUtility.ToJson(scriptableObject);

        var jObject = JObject.Parse(json);

        RemoveFieldsWithInstanceID(jObject);

        jObject.WriteTo(writer);
    }

    private void RemoveFieldsWithInstanceID(JToken token)
    {
        var tokensToRemove = new List<JToken>();

        CollectTokensToRemove(token, tokensToRemove);

        foreach (var tok in tokensToRemove)
        {
            tok.Remove();
        }
    }

    private void CollectTokensToRemove(JToken token, List<JToken> tokensToRemove)
    {
        if (token.Type == JTokenType.Object)
        {
            var children = token.Children<JProperty>().ToList();
            foreach (var child in children)
            {
                if (child.Value is JObject childObject && childObject["instanceID"] != null)
                {
                    tokensToRemove.Add(child);
                }
                else
                {
                    CollectTokensToRemove(child.Value, tokensToRemove);
                }
            }
        }
        else if (token.Type == JTokenType.Array)
        {
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

