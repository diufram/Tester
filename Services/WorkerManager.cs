using Tester.Models;

namespace Tester.Services;

public class WorkerManager
{
    private readonly List<Worker> _workers = new();
    private readonly OperationRequest[] _operations;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly Dictionary<string, int> _endpointCounts = new();
    private readonly Dictionary<string, int> _endpointErrors = new();
    private readonly object _countLock = new();

    public WorkerManager(OperationRequest[] operations, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        _operations = operations;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public void RecordEndpointExecution(string method, string url, bool isSuccess = true)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_countLock)
        {
            _endpointCounts[endpoint] = _endpointCounts.GetValueOrDefault(endpoint, 0) + 1;
            
            if (!isSuccess)
            {
                _endpointErrors[endpoint] = _endpointErrors.GetValueOrDefault(endpoint, 0) + 1;
            }
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
            
            // Agrupar por método HTTP
            var groupedByMethod = _endpointCounts
                .GroupBy(kvp => kvp.Key.Split(' ')[0]) // Extraer el método (GET, POST, etc.)
                .OrderBy(g => GetMethodOrder(g.Key))   // Ordenar por prioridad de método
                .ToList();
            
            foreach (var methodGroup in groupedByMethod)
            {
                var method = methodGroup.Key;
                var totalForMethod = methodGroup.Sum(kvp => kvp.Value);
                var errorsForMethod = methodGroup.Sum(kvp => _endpointErrors.GetValueOrDefault(kvp.Key, 0));
                var successRate = totalForMethod > 0 ? ((totalForMethod - errorsForMethod) * 100.0 / totalForMethod) : 100;
                
                Console.WriteLine($"\n🔹 {method} ({totalForMethod} requests, {errorsForMethod} errores, {successRate:F1}% éxito):");
                
                // Ordenar endpoints dentro del método por número de peticiones (descendente)
                var sortedEndpoints = methodGroup.OrderByDescending(kvp => kvp.Value);
                
                foreach (var kvp in sortedEndpoints)
                {
                    var url = kvp.Key.Substring(method.Length + 1); // Quitar "GET " del inicio
                    var errors = _endpointErrors.GetValueOrDefault(kvp.Key, 0);
                    var success = kvp.Value - errors;
                    var endpointSuccessRate = kvp.Value > 0 ? (success * 100.0 / kvp.Value) : 100;
                    
                    if (errors > 0)
                    {
                        Console.WriteLine($"   {url} - {kvp.Value} total ({success} ✅, {errors} ❌, {endpointSuccessRate:F1}%)");
                    }
                    else
                    {
                        Console.WriteLine($"   {url} - {kvp.Value} requests (100% ✅)");
                    }
                }
            }
            
            var totalRequests = _endpointCounts.Values.Sum();
            var totalErrors = _endpointErrors.Values.Sum();
            var totalSuccessRate = totalRequests > 0 ? ((totalRequests - totalErrors) * 100.0 / totalRequests) : 100;
            Console.WriteLine($"\n📈 Total general: {totalRequests} requests ({totalRequests - totalErrors} ✅, {totalErrors} ❌, {totalSuccessRate:F1}% éxito)");
            Console.WriteLine();
        }
    }

    private static int GetMethodOrder(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => 1,
            "POST" => 2,
            "PUT" => 3,
            "PATCH" => 4,
            "DELETE" => 5,
            _ => 6
        };
    }

}
