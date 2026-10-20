using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synthient.Tests;

// Checks the SDK against the OpenAPI spec it was last updated to, .sdkgen/openapi.json.
public sealed class ConformanceTests
{
    private static readonly JsonElement Spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.json"))).RootElement;

    private static readonly JsonElement Schemas = Spec.GetProperty("components").GetProperty("schemas");

    private static readonly Dictionary<string, Type> Models = new()
    {
        ["ASNEntry"] = typeof(AsnCount),
        ["AccountAPIKey"] = typeof(AccountApiKey),
        ["AccountInfoResponse"] = typeof(Account),
        ["ActivityStatsBlock"] = typeof(ActivityStats),
        ["AnonymizerStreamEntry"] = typeof(AnonymizerEvent),
        ["CountryEntry"] = typeof(CountryCount),
        ["DeviceInfo"] = typeof(Device),
        ["DomainLookupResponse"] = typeof(DomainInfo),
        ["ExportListItem"] = typeof(Export),
        ["ExportListResponse"] = typeof(ExportPage),
        ["ExportMetaResponse"] = typeof(ExportMeta),
        ["HoneypotADBStreamEntry"] = typeof(HeliosAdbEvent),
        ["HoneypotDNSStreamEntry"] = typeof(HeliosDnsEvent),
        ["HoneypotHTTPSStreamEntry"] = typeof(HeliosHttpsEvent),
        ["HoneypotHTTPStreamEntry"] = typeof(HeliosHttpEvent),
        ["HoneypotMeta"] = typeof(HeliosMeta),
        ["IntelligenceInfo"] = typeof(Intelligence),
        ["IpBatchRequest"] = typeof(IPBatchRequest),
        ["IpBatchResponse"] = typeof(IPBatchResponse),
        ["IpProvidersResponse"] = typeof(IPInfo),
        ["JA4TStreamEntry"] = typeof(Ja4tEvent),
        ["LocationInfo"] = typeof(Location),
        ["LookupQuota"] = typeof(LookupQuota),
        ["NetworkInfo"] = typeof(Network),
        ["Organization"] = typeof(Organization),
        ["PortEntry"] = typeof(PortCount),
        ["ProjectedColumn"] = typeof(ParquetColumn),
        ["ProviderEntry"] = typeof(Provider),
        ["ProxyStreamEntry"] = typeof(ProxyEvent),
        ["RecentEvent"] = typeof(DomainEvent),
        ["Schema"] = typeof(ParquetSchema),
        ["Stats"] = typeof(DomainStats),
        ["SubdomainEntry"] = typeof(SubdomainCount),
        ["TLSClientHello"] = typeof(TlsClientHello),
        ["TLSCodeName"] = typeof(TlsCodeName),
        ["TLSExtension"] = typeof(TlsExtension),
        ["TimeSeriesEntry"] = typeof(TimeSeriesPoint),
        ["TorrentFileEntry"] = typeof(TorrentFile),
        ["TorrentPeerEntry"] = typeof(TorrentPeer),
        ["TorrentStreamEntry"] = typeof(TorrentEvent),
        ["UniqueIPsBlock"] = typeof(UniqueIPCounts),
    };

    // Problem bodies are parsed by hand so that malformed errors still produce an ApiException.
    private static readonly string[] Unmodeled = ["ProblemDetails", "ValidationProblemDetails"];

    private static readonly Dictionary<string, Type> ValueSets = new()
    {
        ["AccountInfoResponse.scopes"] = typeof(Scope),
        ["ActivityStatsBlock.cadence"] = typeof(Cadence),
        ["AnonymizerStreamEntry.type"] = typeof(ProxyType),
        ["DeviceInfo.os"] = typeof(DeviceOS),
        ["DomainLookupResponse.status"] = typeof(DomainStatus),
        ["ExportListItem.kind"] = typeof(ExportKind),
        ["ExportMetaResponse.kind"] = typeof(ExportKind),
        ["JA4TStreamEntry.type"] = typeof(ProxyType),
        ["NetworkInfo.type"] = typeof(NetworkType),
        ["Organization.relation"] = typeof(Relation),
        ["ProjectedColumn.type"] = typeof(ParquetType),
        ["ProxyStreamEntry.type"] = typeof(ProxyType),
    };

    public static TheoryData<string> ModeledSchemas => [.. Models.Keys];

    [Fact]
    public void UsesTheSpecServer()
    {
        var server = Spec.GetProperty("servers")[0].GetProperty("url").GetString();

        Assert.Equal(server, SynthientClient.DefaultBaseAddress.AbsoluteUri);
    }

    [Fact]
    public void EverySchemaIsModeled()
    {
        var names = Schemas.EnumerateObject().Select(schema => schema.Name).Order();

        Assert.Equal(names, Models.Keys.Concat(Unmodeled).Order());
    }

    [Theory]
    [MemberData(nameof(ModeledSchemas))]
    public void ModelsMatchTheirSchemas(string name)
    {
        CheckObject(name, Schemas.GetProperty(name), Models[name]);
    }

    [Fact]
    public void ValueSetsMatchTheSpec()
    {
        var found = new Dictionary<string, string[]>();
        foreach (var schema in Schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }
            foreach (var property in properties.EnumerateObject())
            {
                var values = EnumValues(property.Value);
                if (values.Length >= 2)
                {
                    found[$"{schema.Name}.{property.Name}"] = values;
                }
            }
        }

        Assert.Equal(found.Keys.Order(), ValueSets.Keys.Order());
        foreach (var (property, values) in found)
        {
            var constants = ValueSets[property]
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue()!);
            Assert.True(values.Order().SequenceEqual(constants.Order()), $"{ValueSets[property].Name} does not match {property}");
        }
    }

    [Fact]
    public async Task EveryOperationIsCovered()
    {
        var handler = new StubHandler(_ => Fixtures.Json(404, """{"type":"t","title":"Not Found","status":404,"detail":"d"}"""));
        using var client = Fixtures.StubClient(handler);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ApiException>(() => client.GetAccountAsync(token));
        await Assert.ThrowsAsync<ApiException>(() => client.CheckHealthAsync(token));
        await Assert.ThrowsAsync<ApiException>(() => client.LookupIPAsync(IPAddress.Loopback, token));
        await Assert.ThrowsAsync<ApiException>(() => client.LookupIPsAsync([IPAddress.Loopback], token));
        await Assert.ThrowsAsync<ApiException>(() => client.LookupDomainAsync("example.com", cancellationToken: token));
        await ExerciseAsync(client.Proxies);
        await ExerciseAsync(client.Anonymizers);
        await ExerciseAsync(client.Torrents);
        await ExerciseAsync(client.Ja4t);
        await ExerciseAsync(client.HeliosHttp);
        await ExerciseAsync(client.HeliosHttps);
        await ExerciseAsync(client.HeliosDns);
        await ExerciseAsync(client.HeliosAdb);

        var operations = Spec.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(method => (Method: method.Name.ToUpperInvariant(), Template: path.Name)))
            .ToList();
        var covered = handler.Requests
            .Select(request => (Method: request.Method.Method, Template: TemplateOf(request.Path["/api/v4".Length..], operations)))
            .ToHashSet();

        Assert.Equal(53, operations.Count);
        Assert.Equal(operations.Order(), covered.Order());

        async Task ExerciseAsync<T>(Feed<T> feed)
        {
            var hour = ExportId.Hourly(new DateOnly(2026, 5, 3), 7);
            var day = ExportId.Daily(new DateOnly(2026, 5, 3));
            await Assert.ThrowsAsync<ApiException>(() => feed.ListExportsAsync(cancellationToken: token));
            await Assert.ThrowsAsync<ApiException>(() => feed.GetExportMetaAsync(day, token));
            await Assert.ThrowsAsync<ApiException>(() => feed.GetExportMetaAsync(hour, token));
            await Assert.ThrowsAsync<ApiException>(() => feed.GetDownloadUrlAsync(day, token));
            await Assert.ThrowsAsync<ApiException>(() => feed.DownloadAsync(hour, token));
            await Assert.ThrowsAsync<ApiException>(async () =>
            {
                await foreach (var _ in feed.StreamAsync(token))
                {
                }
            });
        }
    }

    // A daily meta path also matches the {date}/{hour} template, so the most literal template wins.
    private static string TemplateOf(string path, List<(string Method, string Template)> operations) => operations
        .Select(operation => operation.Template)
        .Where(template => Regex.IsMatch(path, "^" + Regex.Replace(template, @"\{[^}]+\}", "[^/]+") + "$"))
        .OrderByDescending(template => template.Split('/').Count(segment => !segment.StartsWith('{')))
        .FirstOrDefault() ?? "unmatched " + path;

    private static string[] EnumValues(JsonElement property)
    {
        var target = property.TryGetProperty("items", out var items) ? items : property;
        var values = new List<string>();
        foreach (var key in new[] { "enum", "x-extensible-enum" })
        {
            if (target.TryGetProperty(key, out var list))
            {
                values.AddRange(list.EnumerateArray().Select(value => value.GetString()!));
            }
        }
        return [.. values];
    }

    private static void CheckObject(string name, JsonElement schema, Type type)
    {
        var info = Json.Options.GetTypeInfo(type);
        var properties = schema.GetProperty("properties");
        var required = schema.TryGetProperty("required", out var list)
            ? list.EnumerateArray().Select(value => value.GetString()!).ToHashSet()
            : [];

        Assert.Equal(
            properties.EnumerateObject().Select(property => property.Name).Order(),
            info.Properties.Select(property => property.Name).Order());
        foreach (var property in info.Properties)
        {
            var spec = properties.GetProperty(property.Name);
            var path = $"{name}.{property.Name}";
            var nullable = !required.Contains(property.Name) || (spec.TryGetProperty("nullable", out var flag) && flag.GetBoolean());
            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            var isNullable = property.PropertyType.IsValueType ? underlying is not null : property.IsSetNullable && property.IsGetNullable;

            Assert.True(required.Contains(property.Name) == property.IsRequired, $"{path} requiredness differs from the spec");
            Assert.True(nullable == isNullable, $"{path} nullability differs from the spec");
            CheckType(path, property.Name, spec, underlying ?? property.PropertyType);
        }
    }

    private static void CheckType(string path, string name, JsonElement spec, Type type)
    {
        if (spec.TryGetProperty("$ref", out var reference))
        {
            Assert.True(Models[reference.GetString()!.Split('/')[^1]] == type, $"{path} has the wrong model type {type.Name}");
            return;
        }
        if (spec.TryGetProperty("allOf", out var allOf))
        {
            CheckType(path, name, allOf[0], type);
            return;
        }

        var format = spec.TryGetProperty("format", out var value) ? value.GetString() : null;
        Type[] allowed = spec.GetProperty("type").GetString() switch
        {
            "string" when format == "date-time" => [typeof(DateTimeOffset)],
            "string" when format == "byte" => [typeof(byte[])],
            "string" => name switch
            {
                "ip" => [typeof(string), typeof(IPAddress)],
                "date" => [typeof(DateOnly)],
                _ => [typeof(string)],
            },
            "integer" => name switch
            {
                "timestamp" or "last_seen" or "created_at" or "date" => [typeof(DateTimeOffset)],
                "resets_in" => [typeof(TimeSpan)],

                // The spec says int64, but the API sends uint64 tunnel IDs above long.MaxValue
                // (openapi-issues.md #31).
                "tunnel_id" => [typeof(ulong)],
                _ when format == "int64" => [typeof(long)],
                _ => [typeof(int), typeof(long)],
            },
            "number" => [typeof(double)],
            "boolean" => [typeof(bool)],
            "array" when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) => [type],
            "array" => [],
            "object" => [type],
            var other => throw new InvalidOperationException($"{path} has unknown type {other}"),
        };
        Assert.True(allowed.Contains(type), $"{path} should not be {type.Name}");

        switch (spec.GetProperty("type").GetString())
        {
            case "array":
                CheckType(path + "[]", name, spec.GetProperty("items"), type.GetGenericArguments()[0]);
                break;
            case "object" when spec.TryGetProperty("additionalProperties", out var values):
                Assert.True(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>), $"{path} should be a dictionary");
                Assert.Equal(typeof(string), type.GetGenericArguments()[0]);
                CheckType(path + "{}", name, values, type.GetGenericArguments()[1]);
                break;
            case "object":
                CheckObject(path, spec, type);
                break;
        }
    }
}
