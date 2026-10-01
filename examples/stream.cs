#:project ../src/Synthient.Client/Synthient.Client.csproj

// Prints proxy sightings from the real-time proxies feed until interrupted,
// reconnecting whenever the server closes the stream. It needs an API key with
// the PROXY_FIREHOSE scope.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/stream.cs

using Synthient;

using var client = new SynthientClient();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

while (!stop.IsCancellationRequested)
{
    try
    {
        await foreach (var proxy in client.Proxies.StreamAsync(stop.Token))
        {
            Console.WriteLine($"{proxy.Timestamp:u} {proxy.IP} {proxy.Provider} {proxy.Type} {proxy.CountryCode}");
        }
        // The server closes each stream after about 30 minutes. Pause before
        // reconnecting so repeated failures stay under the connection rate limit.
        await Task.Delay(TimeSpan.FromSeconds(2), stop.Token);
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
    }
    catch (SynthientException e)
    {
        Console.Error.WriteLine($"stream: {e.Message}");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
