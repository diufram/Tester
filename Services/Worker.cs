using Tester.Http;
using Tester.Models;
using Tester.Utils;
using System.Text;
using System.Text.Json;

namespace Tester.Services;

public sealed class Worker
{
    private readonly int _id;
    private readonly OperationRequest[] _operations;
    private readonly ManualResetEventSlim _pauseGate;
    private readonly CancellationToken _stopToken;
    private readonly Task _task;
    private readonly TimeSpan _minDelay;
    private readonly TimeSpan _maxDelay;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public Worker(
        int id,
        OperationRequest[] operations,
        ManualResetEventSlim pauseGate,
        CancellationToken stopToken,
        TimeSpan minDelay,
        TimeSpan maxDelay)
    {
        _id = id;
        _operations = operations;
        _pauseGate = pauseGate;
        _stopToken = stopToken;
        _minDelay = minDelay;
        _maxDelay = maxDelay;

        // Hilo dedicado para no saturar el thread pool:
        _task = Task.Factory.StartNew(
            () => RunAsync().GetAwaiter().GetResult(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public Task Task => _task;

    private async Task RunAsync()
    {
        while (!_stopToken.IsCancellationRequested)
        {
            // Espera cooperativa de pausa (no consume CPU):
            try { _pauseGate.Wait(_stopToken); } catch (OperationCanceledException) { break; }

            var op = _operations[Utils.RandomUtil.NextInt(0, _operations.Length)];

            try
            {
                var req = BuildHttpRequest(op);
                var started = DateTime.UtcNow;
                var resp = await HttpClientProvider.Client.SendAsync(req, _stopToken);
                var elapsed = DateTime.UtcNow - started;

                Log.Write($"[W{_id}] {op.Operation} {op.Url} => {(int)resp.StatusCode} {resp.ReasonPhrase} ({elapsed.TotalMilliseconds:N0} ms)");
            }
            catch (TaskCanceledException)
            {
                if (_stopToken.IsCancellationRequested) break; // cierre
                // si fue por pausa, el loop sigue y volverá a Wait() arriba
            }
            catch (Exception ex)
            {
                Log.Write($"[W{_id}] ERROR {op.Operation} {op.Url}: {ex.Message}");
            }

            // Delay amortiguado, rechecando pausa para que "pause" sea inmediato:
            var ms = Utils.RandomUtil.NextInt((int)_minDelay.TotalMilliseconds, (int)_maxDelay.TotalMilliseconds + 1);
            var remain = ms;
            while (remain > 0 && !_stopToken.IsCancellationRequested)
            {
                var chunk = Math.Min(remain, 50);
                try { await Task.Delay(chunk, _stopToken); } catch { }
                remain -= chunk;

                // Si se activó la pausa durante el delay, espera aquí:
                if (!_pauseGate.IsSet)
                {
                    try { _pauseGate.Wait(_stopToken); } catch (OperationCanceledException) { break; }
                }
            }
        }
    }

    private static HttpRequestMessage BuildHttpRequest(OperationRequest op)
    {
        var method = op.Operation switch
        {
            OperationType.GetAll  => HttpMethod.Get,
            OperationType.GetById => HttpMethod.Get,
            OperationType.Create  => HttpMethod.Post,
            OperationType.Update  => HttpMethod.Put,
            OperationType.Delete  => HttpMethod.Delete,
            _ => HttpMethod.Get
        };

        var msg = new HttpRequestMessage(method, new Uri(op.Url));

        if (op.Operation is OperationType.Create or OperationType.Update && op.Body is not null)
        {
            var json = JsonSerializer.Serialize(op.Body, _json);
            msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return msg;
    }
}
