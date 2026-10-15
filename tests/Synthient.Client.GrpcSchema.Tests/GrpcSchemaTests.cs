using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Synthient.Tests;

public sealed class GrpcSchemaTests : IAsyncLifetime
{
    private WebApplication? _server;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ConcurrentQueue<string?> Keys { get; } = new();

    private string Address { get; set; } = "";

    // Starts a loopback gRPC server with reflection and the health service.
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddGrpcReflection();
        builder.Services.AddSingleton<HealthServiceImpl>();
        _server = builder.Build();
        _server.Use(async (context, next) =>
        {
            Keys.Enqueue(context.Request.Headers["x-api-key"].FirstOrDefault());
            await next(context);
        });
        _server.MapGrpcService<HealthServiceImpl>();
        _server.MapGrpcReflectionService();
        await _server.StartAsync(Token);
        var address = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Address = $"127.0.0.1:{new Uri(address).Port}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    [Fact]
    public async Task FetchesEveryService()
    {
        var schema = await GrpcSchema.FetchAsync(" test-key ", new GrpcSchemaOptions { Endpoint = Address, Plaintext = true }, Token);

        Assert.Equal(Address, schema.Endpoint);
        Assert.Equal(["grpc.health.v1.Health"], schema.Symbols);
        Assert.Contains(schema.DescriptorSet.File, file => file.Package == "grpc.health.v1");
        Assert.All(Keys, key => Assert.Equal("test-key", key));
    }

    [Fact]
    public async Task AppliesOptions()
    {
        var options = new GrpcSchemaOptions { Endpoint = "grpc://" + Address, Plaintext = true };
        options.Symbols.Add("grpc.reflection.v1.ServerReflection");

        var schema = await GrpcSchema.FetchAsync("", options, Token);

        Assert.Equal(["grpc.reflection.v1.ServerReflection"], schema.Symbols);
        Assert.Contains(schema.DescriptorSet.File, file => file.Package == "grpc.reflection.v1");
        Assert.All(Keys, Assert.Null);
    }

    [Fact]
    public async Task CleansUpSymbols()
    {
        var options = new GrpcSchemaOptions { Endpoint = Address, Plaintext = true };
        options.Symbols.Add(" grpc.health.v1.Health ");
        options.Symbols.Add("grpc.health.v1.Health");
        options.Symbols.Add(" ");

        var schema = await GrpcSchema.FetchAsync("k", options, Token);

        Assert.Equal(["grpc.health.v1.Health"], schema.Symbols);
    }

    [Fact]
    public async Task UnknownSymbolsAreNotFound()
    {
        var options = new GrpcSchemaOptions { Endpoint = Address, Plaintext = true };
        options.Symbols.Add("synthient.v1.Missing");

        var error = await Assert.ThrowsAsync<GrpcSchemaException>(() => GrpcSchema.FetchAsync("k", options, Token));

        Assert.Equal(StatusCode.NotFound, error.StatusCode);
        Assert.NotNull(GrpcSchema.Explain(error));
    }

    [Fact]
    public async Task UnreachableServersAreUnavailable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var error = await Assert.ThrowsAsync<GrpcSchemaException>(
            () => GrpcSchema.FetchAsync("k", new GrpcSchemaOptions { Endpoint = $"127.0.0.1:{port}", Plaintext = true }, Token));

        Assert.Equal(StatusCode.Unavailable, error.StatusCode);
    }

    [Fact]
    public async Task SilentServersTimeOut()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var options = new GrpcSchemaOptions { Endpoint = $"127.0.0.1:{port}", Plaintext = true, Timeout = TimeSpan.FromSeconds(1) };

        var error = await Assert.ThrowsAsync<GrpcSchemaException>(() => GrpcSchema.FetchAsync("k", options, Token));

        Assert.Equal(StatusCode.DeadlineExceeded, error.StatusCode);
        Assert.NotNull(GrpcSchema.Explain(error));
    }

    [Fact]
    public async Task CancellationThrowsOperationCanceled()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GrpcSchema.FetchAsync("k", new GrpcSchemaOptions { Endpoint = Address, Plaintext = true }, new CancellationToken(true)));
    }

    [Theory]
    [InlineData(null, "grpc.synthient.com:443")]
    [InlineData("", "grpc.synthient.com:443")]
    [InlineData("grpc.synthient.com", "grpc.synthient.com:443")]
    [InlineData("localhost:50051/", "localhost:50051")]
    [InlineData("grpc://localhost:50051", "localhost:50051")]
    [InlineData("https://grpc.synthient.com", "grpc.synthient.com:443")]
    [InlineData("http://localhost:80", "localhost:80")]
    [InlineData("https://grpc.synthient.com/path?q", "grpc.synthient.com:443")]
    [InlineData("[::1]:9000", "[::1]:9000")]
    [InlineData("[::1]", "[::1]:443")]
    [InlineData("::1", "[::1]:443")]
    public void NormalizesEndpoints(string? endpoint, string normalized)
    {
        Assert.Equal(normalized, GrpcSchema.ParseEndpoint(endpoint).Endpoint);
    }

    [Theory]
    [InlineData("grpc://")]
    [InlineData("localhost:port")]
    [InlineData(":443")]
    [InlineData("localhost:99999")]
    [InlineData("localhost:0")]
    [InlineData("[::1]junk")]
    public void RejectsInvalidEndpoints(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => GrpcSchema.ParseEndpoint(endpoint).Endpoint);
    }

    [Fact]
    public void ExplainsCommonFailures()
    {
        var explained = new[]
        {
            StatusCode.Unauthenticated, StatusCode.PermissionDenied, StatusCode.NotFound,
            StatusCode.Unimplemented, StatusCode.Unavailable, StatusCode.DeadlineExceeded,
        };
        foreach (var code in explained)
        {
            Assert.NotNull(GrpcSchema.Explain(new GrpcSchemaException(code, "x")));
            Assert.NotNull(GrpcSchema.Explain(new RpcException(new Status(code, "x"))));
        }
        Assert.Null(GrpcSchema.Explain(new GrpcSchemaException(StatusCode.Internal, "x")));
        Assert.Null(GrpcSchema.Explain(new InvalidOperationException("x")));
    }

    [Fact]
    public void OrdersFilesAfterTheirImports()
    {
        var files = new Dictionary<string, FileDescriptorProto>
        {
            ["c.proto"] = new() { Name = "c.proto", Dependency = { "b.proto", "a.proto" } },
            ["b.proto"] = new() { Name = "b.proto", Dependency = { "a.proto" } },
            ["a.proto"] = new() { Name = "a.proto" },
            ["d.proto"] = new() { Name = "d.proto", Dependency = { "missing.proto" } },
        };

        Assert.Equal(["a.proto", "b.proto", "c.proto", "d.proto"], GrpcSchema.OrderedFiles(files).Select(file => file.Name));
        Assert.Equal(["missing.proto"], GrpcSchema.MissingDependencies(files));
    }
}
