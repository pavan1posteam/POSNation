using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POS_Nation.Models;
using System;
using System.Collections.Generic;

public class ItemdepositConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(List<Itemdeposit>));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        if (token.Type == JTokenType.Object)
        {
            return new List<Itemdeposit> { token.ToObject<Itemdeposit>() };
        }
        else if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<Itemdeposit>>();
        }

        return new List<Itemdeposit>();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var list = value as List<Itemdeposit>;
        if (list != null && list.Count == 1)
        {
            serializer.Serialize(writer, list[0]);
        }
        else
        {
            serializer.Serialize(writer, value);
        }
    }
}
