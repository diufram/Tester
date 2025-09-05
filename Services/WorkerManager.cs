using Tester.Models;

namespace Tester.Services;

public sealed class WorkerManager
{
    private readonly List<(CancellationTokenSource cts, Worker worker)> _workers = new();
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);
    private readonly OperationRequest[] _operations;
    private readonly TimeSpan _minDelay;
    private readonly TimeSpan _maxDelay;

    public WorkerManager(OperationRequest[] operations, TimeSpan minDelay, TimeSpan maxDelay)
    {
        _operations = operations;
        _minDelay = minDelay;
        _maxDelay = maxDelay;
    }

    public int Count => _workers.Count;
    public bool IsPaused => !_pauseGate.IsSet;

    // ===== Operaciones de tamaño =====
    public void AddWorkers(int n)
    {
        if (n <= 0) { Console.WriteLine("Cantidad a agregar debe ser > 0."); return; }
        for (int i = 0; i < n; i++)
        {
            var cts = new CancellationTokenSource();
            var worker = new Worker(_workers.Count + 1, _operations, _pauseGate, cts.Token, _minDelay, _maxDelay);
            _workers.Add((cts, worker));
        }
        Console.WriteLine($"➕ Agregados {n} worker(s). Total: {Count}");
    }

    public async Task RemoveWorkersAsync(int n)
    {
        if (n <= 0) { Console.WriteLine("Cantidad a quitar debe ser > 0."); return; }
        if (_workers.Count == 0) { Console.WriteLine("No hay workers para quitar."); return; }

        n = Math.Min(n, _workers.Count);
        var toStop = new List<(CancellationTokenSource cts, Worker worker)>();

        for (int i = 0; i < n; i++)
        {
            var last = _workers[^1];
            _workers.RemoveAt(_workers.Count - 1);
            toStop.Add(last);
        }

        Console.WriteLine($"➖ Deteniendo {n} worker(s)...");
        foreach (var w in toStop) w.cts.Cancel();
        foreach (var w in toStop) { try { await w.worker.Task; } catch { } w.cts.Dispose(); }

        Console.WriteLine($"Total actual: {Count}");
    }

    public async Task SetWorkersAsync(int target)
    {
        if (target < 0) { Console.WriteLine("El tamaño objetivo no puede ser negativo."); return; }

        if (target > Count)
        {
            AddWorkers(target - Count);
        }
        else if (target < Count)
        {
            await RemoveWorkersAsync(Count - target);
        }
        else
        {
            Console.WriteLine($"Ya hay {Count} worker(s).");
        }
    }

    // ===== Control de pausa =====
    public void Pause()
    {
        if (IsPaused) { Console.WriteLine("Ya está en pausa."); return; }
        _pauseGate.Reset();
        Console.WriteLine("⏸️  Pausado.");
    }

    public void Resume()
    {
        if (!IsPaused) { Console.WriteLine("Ya está en ejecución."); return; }
        _pauseGate.Set();
        Console.WriteLine("▶️  Reanudado.");
    }

    public void TogglePause()
    {
        if (IsPaused) Resume(); else Pause();
    }

    // ===== Detener todo =====
    public async Task StopAllAsync()
    {
        Console.WriteLine("Deteniendo todos los workers...");
        var list = _workers.ToArray();
        _workers.Clear();
        foreach (var w in list) w.cts.Cancel();
        foreach (var w in list) { try { await w.worker.Task; } catch { } w.cts.Dispose(); }
        Console.WriteLine("Todos los workers detenidos.");
    }
}
