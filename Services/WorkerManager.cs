using Tester.Models;

namespace Tester.Services;

public class WorkerManager
{
    private readonly List<Worker> _workers = new();
    private readonly OperationRequest[] _operations;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly Dictionary<string, int> _endpointCounts = new();
    private readonly object _countLock = new();

    public WorkerManager(OperationRequest[] operations, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        _operations = operations;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public void RecordEndpointExecution(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_countLock)
        {
            _endpointCounts[endpoint] = _endpointCounts.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    public void AddWorkers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var worker = new Worker(_workers.Count + 1, _operations, _minDelayMs, _maxDelayMs, this);
            _workers.Add(worker);
            worker.Start();
        }
        Console.WriteLine($" {count} workers agregados. Total: {_workers.Count}");
    }

    public void RemoveWorkers(int count)
    {
        if (count > _workers.Count) count = _workers.Count;

        for (int i = 0; i < count; i++)
        {
            var worker = _workers[_workers.Count - 1];
            worker.Stop();
            _workers.RemoveAt(_workers.Count - 1);
        }
        Console.WriteLine($" {count} workers removidos. Total: {_workers.Count}");
    }

    public void SetWorkers(int target)
    {
        if (target < 0)
        {
            Console.WriteLine(" El número objetivo no puede ser negativo");
            return;
        }

        if (target > _workers.Count)
        {
            AddWorkers(target - _workers.Count);
        }
        else if (target < _workers.Count)
        {
            RemoveWorkers(_workers.Count - target);
        }
        else
        {
            Console.WriteLine($" Ya tienes {_workers.Count} worker(s)");
        }
    }

    public void StopAll()
    {
        foreach (var worker in _workers)
        {
            worker.Stop();
        }
        _workers.Clear();
        Console.WriteLine(" Todos los workers detenidos");
    }

    public void ShowStatus()
    {
        Console.WriteLine($" Workers activos: {_workers.Count}");
    }

    public void ShowStats()
    {
        lock (_countLock)
        {
            if (_endpointCounts.Count == 0)
            {
                Console.WriteLine(" No hay estadísticas disponibles aún");
                return;
            }

            Console.WriteLine("\n📊 Estadísticas de Endpoints:");
            
            var sortedEndpoints = _endpointCounts.OrderByDescending(kvp => kvp.Value);
            
            foreach (var kvp in sortedEndpoints)
            {
                Console.WriteLine($" {kvp.Key} - {kvp.Value}");
            }
            
            var totalRequests = _endpointCounts.Values.Sum();
            Console.WriteLine($"\n Total: {totalRequests} requests");
            Console.WriteLine();
        }
    }

}
