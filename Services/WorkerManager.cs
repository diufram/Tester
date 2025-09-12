using Tester.Models;
using System.Text.Json;
using Tester.Utils;


namespace Tester.Services;

public class WorkerManager
{
    private readonly List<Worker> _workers = new();
    private readonly List<OperationRequest> _operations;
    private readonly Dictionary<string, int> _endpointCounts = new();
    private readonly Dictionary<string, int> _endpointErrors = new();
    private readonly Dictionary<string, int> _endpointTimeouts = new();
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly object _countLock = new();
    private readonly object _operationsLock = new();
    private readonly int _initialCount;

    public WorkerManager(OperationRequest[] operations, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        
        _operations = new List<OperationRequest>(operations);
        _initialCount = operations.Length;
    }

    public OperationRequest? GetNextOperation(Random random)
    {
        lock (_operationsLock)
        {
            if (_operations.Count == 0) return null;
            
            // Sacar operación random
            var index = random.Next(_operations.Count);
            var originalOperation = _operations[index];
            
            // Crear una COPIA de la operación para no modificar la original
            var operationCopy = new OperationRequest
            {
                Url = originalOperation.Url,
                Method = originalOperation.Method,
                Headers = originalOperation.Headers, // Las headers normalmente no cambian
                Token = originalOperation.Token,
                Body = originalOperation.Body // Inicialmente la misma referencia
            };
            
            // Si es POST, procesar el body creando una copia profunda
            var method = operationCopy.Method?.ToUpperInvariant() ?? "GET";
            if (method == "POST" && operationCopy.Body != null)
            {
                operationCopy.Body = ProcessPostBodyCopy(operationCopy.Body, operationCopy.Url);
            }
            
            return operationCopy;
        }
    }

    private object ProcessPostBodyCopy(object body, string url)
    {
        try
        {
            Dictionary<string, object>? dict;

            // Normalizar todo a Dictionary<string, object>
            if (body is string jsonString)
            {
                dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
            }
            else if (body is JsonElement element)
            {
                dict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
            }
            else if (body is Dictionary<string, object> existingDict)
            {
                // CREAR UNA COPIA PROFUNDA del diccionario existente
                var serialized = JsonSerializer.Serialize(existingDict);
                dict = JsonSerializer.Deserialize<Dictionary<string, object>>(serialized);
            }
            else
            {
                return body; // tipo no esperado → devolver tal cual
            }

            if (dict == null) return body;

            // Procesar valores recursivamente en la copia
            ProcessDictionaryFields(dict, url);

            return dict;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando body POST: {ex.Message}");
            return body;
        }
    }

    private void ProcessDictionaryFields(Dictionary<string, object> dict, string url)
    {
        foreach (var key in dict.Keys.ToList())
        {
            if (dict[key] is JsonElement element)
            {
                dict[key] = ConvertJsonElement(element, url);
            }
            else if (dict[key] is Dictionary<string, object> nestedDict)
            {
                ProcessDictionaryFields(nestedDict, url);
            }
            else if (dict[key] is string str && str.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                dict[key] = $"{url}+{RandomStringGenerator.GenerateAlphanumeric(8)}";
            }
        }
    }

    private object ConvertJsonElement(JsonElement element, string url)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when element.GetString()?.Equals("string", StringComparison.OrdinalIgnoreCase) == true
                => $"{url}+{RandomStringGenerator.GenerateAlphanumeric(8)}",

            JsonValueKind.Object =>
                JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()) is { } nestedDict
                    ? ReturnProcessedDict(nestedDict, url)
                    : element.GetRawText(),

            JsonValueKind.Array
                => element.EnumerateArray()
                        .Select(e => ConvertJsonElement(e, url))
                        .ToList(),

            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()
        };
    }
    private object ReturnProcessedDict(Dictionary<string, object> dict, string url)
    {
        ProcessDictionaryFields(dict, url);
        return dict;
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

    public void RecordEndpointTimeout(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_countLock)
        {
            _endpointTimeouts[endpoint] = _endpointTimeouts.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    public void AddWorkers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var worker = new Worker(_workers.Count + 1, _minDelayMs, _maxDelayMs, this);
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
                var timeoutsForMethod = methodGroup.Sum(kvp => _endpointTimeouts.GetValueOrDefault(kvp.Key, 0)); // 🆕
                var successRate = totalForMethod > 0 ? ((totalForMethod - errorsForMethod - timeoutsForMethod) * 100.0 / totalForMethod) : 100;
                
                Console.WriteLine($"\n🔹 {method} ({totalForMethod} requests, {errorsForMethod} errores, {timeoutsForMethod} timeouts, {successRate:F1}% éxito):");
                
                // Ordenar endpoints dentro del método por número de peticiones (descendente)
                var sortedEndpoints = methodGroup.OrderByDescending(kvp => kvp.Value);
                
                foreach (var kvp in sortedEndpoints)
                {
                    var url = kvp.Key.Substring(method.Length + 1); // Quitar "GET " del inicio
                    var errors = _endpointErrors.GetValueOrDefault(kvp.Key, 0);
                    var timeouts = _endpointTimeouts.GetValueOrDefault(kvp.Key, 0); // 🆕
                    var success = kvp.Value - errors - timeouts; // 🆕 Restar también timeouts
                    var endpointSuccessRate = kvp.Value > 0 ? (success * 100.0 / kvp.Value) : 100;
                    
                    if (errors > 0 || timeouts > 0) // 🆕 Mostrar si hay errores O timeouts
                    {
                        var statusText = "";
                        if (success > 0) statusText += $"{success} ✅";
                        if (errors > 0) statusText += $", {errors} ❌";
                        if (timeouts > 0) statusText += $", {timeouts} ⏰"; // 🆕 Emoji para timeouts
                        
                        Console.WriteLine($"   {url} - {kvp.Value} total ({statusText.TrimStart(',', ' ')}, {endpointSuccessRate:F1}%)");
                    }
                    else
                    {
                        Console.WriteLine($"   {url} - {kvp.Value} requests (100% ✅)");
                    }
                }
            }
            
            var totalRequests = _endpointCounts.Values.Sum();
            var totalErrors = _endpointErrors.Values.Sum();
            var totalTimeouts = _endpointTimeouts.Values.Sum(); // 🆕
            var totalSuccessRate = totalRequests > 0 ? ((totalRequests - totalErrors - totalTimeouts) * 100.0 / totalRequests) : 100;
            
            Console.WriteLine($"\n📈 Total general: {totalRequests} requests ({totalRequests - totalErrors - totalTimeouts} ✅, {totalErrors} ❌, {totalTimeouts} ⏰, {totalSuccessRate:F1}% éxito)");
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
