using Tester.Models;

namespace Tester.Services;

public class WorkerManager
{
    private readonly List<Worker> _workers = new();
    private readonly OperationRequest[] _operations;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;

    public WorkerManager(OperationRequest[] operations, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        _operations = operations;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public void AddWorkers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var worker = new Worker(_workers.Count + 1, _operations, _minDelayMs, _maxDelayMs);
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
}
