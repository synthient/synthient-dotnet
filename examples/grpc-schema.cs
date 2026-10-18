#:project ../src/Synthient.Client.GrpcSchema/Synthient.Client.GrpcSchema.csproj

// Fetches the protobuf schema of the Synthient gRPC API with server reflection
// and prints its services and files.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/grpc-schema.cs

using Synthient;

GrpcSchemaResult schema;
try
{
    schema = await GrpcSchema.FetchAsync(Environment.GetEnvironmentVariable("SYNTHIENT_API_KEY"));
}
catch (GrpcSchemaException e)
{
    var explanation = GrpcSchema.Explain(e);
    Console.Error.WriteLine(explanation is null ? e.Message : $"{explanation} {e.Message}");
    return 1;
}

Console.WriteLine($"endpoint: {schema.Endpoint}");
foreach (var symbol in schema.Symbols)
{
    Console.WriteLine($"service: {symbol}");
}
foreach (var file in schema.DescriptorSet.File)
{
    Console.WriteLine($"file: {file.Name} (package {file.Package})");
}
return 0;
