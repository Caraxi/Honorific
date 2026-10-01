using System;
using System.Threading;

namespace Honorific;

public class RateLimit(string label, TimeSpan rate, int maxBurst = 5) : IDisposable {
    private CancellationTokenSource? cts;
    private double invokesAvailable = maxBurst;
    private DateTime lastRefill = DateTime.Now;

    public void Invoke(Action action) {
        var now = DateTime.Now;
        var refreshTime = now - lastRefill;
        if (refreshTime > TimeSpan.Zero) {
            invokesAvailable = Math.Min(maxBurst, invokesAvailable + refreshTime.TotalSeconds / rate.TotalSeconds);
            lastRefill = now;
        }
        
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();;
        
        if (invokesAvailable >= 1) {
            invokesAvailable -= 1;
            PluginService.Framework.RunOnTick(action, cancellationToken: cts.Token);
        } else {
            var delay = TimeSpan.FromSeconds((1 - invokesAvailable) * rate.TotalSeconds);
            PluginService.Log.Debug($"Delaying {label}: {delay} seconds.");
            PluginService.Framework.RunOnTick(action, delay * 2, cancellationToken: cts.Token);
        }
    }
    
    public void Dispose() {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
