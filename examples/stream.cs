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
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
    }
    catch (ApiException e) when (e.StatusCode is 401 or 403)
    {
        // Reconnecting can't fix a rejected key or a missing scope.
        Console.Error.WriteLine($"stream: {e.Message}");
        return 1;
    }
    catch (SynthientException e)
    {
        Console.Error.WriteLine($"stream: {e.Message}");
        // Pause after a failure so a failing stream doesn't reconnect in a
        // tight loop.
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
return 0;
