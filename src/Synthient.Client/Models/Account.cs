using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>The account that owns the API key.</summary>
public sealed class Account
{
    /// <summary>The account holder's email address.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>The account holder's first name.</summary>
    [JsonPropertyName("first_name")]
    public required string FirstName { get; init; }

    /// <summary>The account holder's last name.</summary>
    [JsonPropertyName("last_name")]
    public required string LastName { get; init; }

    /// <summary>The organization the account belongs to.</summary>
    [JsonPropertyName("organization")]
    public required Organization Organization { get; init; }

    /// <summary>The permissions granted to the API key. See <see cref="Scope"/> for known values.</summary>
    [JsonPropertyName("scopes")]
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>The remaining lookup credit balance.</summary>
    [JsonPropertyName("lookup_quota")]
    public required LookupQuota LookupQuota { get; init; }

    /// <summary>The API key used for the request.</summary>
    [JsonPropertyName("api_key")]
    public AccountApiKey? ApiKey { get; init; }
}

/// <summary>The organization an account belongs to.</summary>
public sealed class Organization
{
    /// <summary>The organization's ID, a UUID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The organization's name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The account's role in the organization. See <see cref="Synthient.Relation"/>.</summary>
    [JsonPropertyName("relation")]
    public required string Relation { get; init; }
}

/// <summary>An account's lookup credit balance.</summary>
public sealed class LookupQuota
{
    /// <summary>The number of lookup credits remaining.</summary>
    [JsonPropertyName("credits")]
    public required long Credits { get; init; }

    /// <summary>How long until the credits reset.</summary>
    [JsonPropertyName("resets_in")]
    public required TimeSpan ResetsIn { get; init; }
}

/// <summary>An API key. It never contains the secret key itself.</summary>
public sealed class AccountApiKey
{
    /// <summary>The key's ID, a UUID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The key's display name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>When the key was created.</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}
