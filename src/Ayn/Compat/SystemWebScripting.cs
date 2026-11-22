using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace System.Web.Script.Serialization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ScriptIgnoreAttribute : Attribute
{
    public bool ApplyToOverrides { get; set; }
}

public abstract class JavaScriptConverter
{
    public abstract IEnumerable<Type> SupportedTypes { get; }

    public abstract IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer);

    public abstract object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer);
}

public sealed class JavaScriptSerializer
{
    private static readonly JsonSerializerOptions Options = BuildOptions();

    public int MaxJsonLength { get; set; } = int.MaxValue;

    public int RecursionLimit { get; set; } = 100;

    private static JsonSerializerOptions BuildOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            foreach (var property in info.Properties.ToList())
            {
                if (property.AttributeProvider?.GetCustomAttributes(typeof(ScriptIgnoreAttribute), true).Any() == true)
                    info.Properties.Remove(property);
            }
        });
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
    }

    public void RegisterConverters(IEnumerable<JavaScriptConverter> converters) { }

    public string Serialize(object obj) => JsonSerializer.Serialize(obj, obj?.GetType() ?? typeof(object), Options);

    public T? Deserialize<T>(string input) => JsonSerializer.Deserialize<T>(input, Options);

    public object? DeserializeObject(string input)
    {
        using var doc = JsonDocument.Parse(input);
        return Convert(doc.RootElement);
    }

    public object? ConvertToType(object? obj, Type targetType)
    {
        if (obj is null) return null;
        var json = JsonSerializer.Serialize(obj, Options);
        return JsonSerializer.Deserialize(json, targetType, Options);
    }

    public T? ConvertToType<T>(object? obj) => (T?)ConvertToType(obj, typeof(T));

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value))!,
        JsonValueKind.Array => element.EnumerateArray().Select(Convert).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
