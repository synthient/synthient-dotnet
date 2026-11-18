using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Synthient;

[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    Converters = [typeof(TimestampConverter), typeof(SecondsConverter), typeof(IPAddressConverter)])]
[JsonSerializable(typeof(Account))]
[JsonSerializable(typeof(IPInfo))]
[JsonSerializable(typeof(IPBatchRequest))]
[JsonSerializable(typeof(IPBatchResponse))]
#pragma warning disable CS0618 // Domain lookup is deprecated but still served until the next major version.
[JsonSerializable(typeof(DomainInfo))]
#pragma warning restore CS0618
[JsonSerializable(typeof(ExportPage))]
[JsonSerializable(typeof(ExportMeta))]
[JsonSerializable(typeof(ExportVolume))]
[JsonSerializable(typeof(ProxyEvent))]
[JsonSerializable(typeof(AnonymizerEvent))]
[JsonSerializable(typeof(TorrentEvent))]
[JsonSerializable(typeof(Ja4tEvent))]
[JsonSerializable(typeof(HeliosHttpEvent))]
[JsonSerializable(typeof(HeliosHttpsEvent))]
[JsonSerializable(typeof(HeliosDnsEvent))]
[JsonSerializable(typeof(HeliosAdbEvent))]
internal sealed partial class SynthientJsonContext : JsonSerializerContext;

// Models are strict: a missing required property, a null where the model has no null, and a null
// collection element all fail decoding. Properties added to 1.0 models are marked [JsonRequired]
// instead of required, so that object initializers written against 1.0 still compile.
internal static class Json
{
    internal static readonly JsonSerializerOptions Options = new(SynthientJsonContext.Default.Options)
    {
        TypeInfoResolver = SynthientJsonContext.Default.WithAddedModifier(RejectNullElements),
    };

    internal static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

    internal static T Deserialize<T>(ReadOnlySpan<byte> json, JsonTypeInfo<T> typeInfo)
    {
        T? value;
        try
        {
            value = JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException e)
        {
            throw new SynthientException($"The API returned an invalid {typeof(T).Name}.", e);
        }
        return value ?? throw new SynthientException($"The API returned null instead of a {typeof(T).Name}.");
    }

    private static void RejectNullElements(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }
        var collections = typeInfo.Properties
            .Where(property => property.Get is not null && CanHoldNull(property.PropertyType))
            .ToArray();
        if (collections.Length == 0)
        {
            return;
        }
        typeInfo.OnDeserialized = value =>
        {
            foreach (var property in collections)
            {
                var items = property.Get!(value) switch
                {
                    IDictionary dictionary => dictionary.Values,
                    IEnumerable enumerable => enumerable,
                    _ => null,
                };
                foreach (var item in items ?? Array.Empty<object>())
                {
                    if (item is null)
                    {
                        throw new JsonException($"The {property.Name} property contains a null element.");
                    }
                }
            }
        };
    }

    // Whether a property is a collection whose elements, or dictionary values, can be null.
    private static bool CanHoldNull(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }
        var element = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments()[^1] : null;
        return element is null || !element.IsValueType || Nullable.GetUnderlyingType(element) is not null;
    }
}
