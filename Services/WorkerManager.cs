using Tester.Models;
using System.Text.Json;
using Tester.Utils;

namespace Tester.Services;

public class WorkerManager
{
    private readonly List<Worker> _workers = new();
    private readonly List<OperationRequest> _operations;
    
    // 🔥 NUEVAS ESTADÍSTICAS MÁS PRECISAS
    private readonly Dictionary<string, int> _requestsSent = new();      // Peticiones enviadas
    private readonly Dictionary<string, int> _requestsSuccess = new();   // Respuestas exitosas
    private readonly Dictionary<string, int> _requestsError = new();     // Respuestas con error
    private readonly Dictionary<string, int> _requestsTimeout = new();   // Timeouts
    
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly object _statsLock = new();
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

            var index = random.Next(_operations.Count);
            var originalOperation = _operations[index];

            var operationCopy = new OperationRequest
            {
                Url = originalOperation.Url,
                Method = originalOperation.Method,
                Headers = originalOperation.Headers,
                Token = originalOperation.Token,
                Body = originalOperation.Body
            };

            var method = operationCopy.Method?.ToUpperInvariant() ?? "GET";
            if ((method == "POST" || method == "PUT") && operationCopy.Body != null)
            {
                operationCopy.Body = ProcessPostBodyCopy(operationCopy.Body, operationCopy.Url);
            }

            return operationCopy;
        }
    }

    // 🔥 MÉTODOS ESPECÍFICOS PARA DIFERENTES TIPOS DE REGISTROS
    public void RecordRequestSent(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_statsLock)
        {
            _requestsSent[endpoint] = _requestsSent.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    public void RecordRequestSuccess(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_statsLock)
        {
            _requestsSuccess[endpoint] = _requestsSuccess.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    public void RecordRequestError(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_statsLock)
        {
            _requestsError[endpoint] = _requestsError.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    public void RecordRequestTimeout(string method, string url)
    {
        var endpoint = $"{method.ToUpperInvariant()} {url}";
        lock (_statsLock)
        {
            _requestsTimeout[endpoint] = _requestsTimeout.GetValueOrDefault(endpoint, 0) + 1;
        }
    }

    // 🔥 MANTENER COMPATIBILIDAD CON CÓDIGO EXISTENTE
    [Obsolete("Usar RecordRequestSent, RecordRequestSuccess, RecordRequestError en su lugar")]
    public void RecordEndpointExecution(string method, string url, bool isSuccess = true)
    {
        RecordRequestSent(method, url);
        if (isSuccess)
            RecordRequestSuccess(method, url);
        else
            RecordRequestError(method, url);
    }

    [Obsolete("Usar RecordRequestTimeout en su lugar")]
    public void RecordEndpointTimeout(string method, string url)
    {
        RecordRequestTimeout(method, url);
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
        lock (_statsLock)
        {
            if (_requestsSent.Count == 0)
            {
                Console.WriteLine(" No hay estadísticas disponibles aún");
                return;
            }

            Console.WriteLine("\n📊 Estadísticas de Endpoints:");

            // Obtener todos los endpoints únicos
            var allEndpoints = _requestsSent.Keys
                .Union(_requestsSuccess.Keys)
                .Union(_requestsError.Keys)
                .Union(_requestsTimeout.Keys)
                .ToHashSet();

            // Agrupar por método HTTP
            var groupedByMethod = allEndpoints
                .GroupBy(endpoint => endpoint.Split(' ')[0])
                .OrderBy(g => GetMethodOrder(g.Key))
                .ToList();

            foreach (var methodGroup in groupedByMethod)
            {
                var method = methodGroup.Key;
                var sentForMethod = methodGroup.Sum(endpoint => _requestsSent.GetValueOrDefault(endpoint, 0));
                var successForMethod = methodGroup.Sum(endpoint => _requestsSuccess.GetValueOrDefault(endpoint, 0));
                var errorForMethod = methodGroup.Sum(endpoint => _requestsError.GetValueOrDefault(endpoint, 0));
                var timeoutForMethod = methodGroup.Sum(endpoint => _requestsTimeout.GetValueOrDefault(endpoint, 0));
                var pendingForMethod = sentForMethod - successForMethod - errorForMethod - timeoutForMethod;

                var successRate = sentForMethod > 0 ? (successForMethod * 100.0 / sentForMethod) : 0;

                Console.WriteLine($"\n🔹 {method} ({sentForMethod} enviadas, {successForMethod} éxito, {errorForMethod} error, {timeoutForMethod} timeout, {pendingForMethod} pendientes, {successRate:F1}% éxito):");

                // Ordenar endpoints por número de peticiones enviadas
                var sortedEndpoints = methodGroup
                    .OrderByDescending(endpoint => _requestsSent.GetValueOrDefault(endpoint, 0));

                foreach (var endpoint in sortedEndpoints)
                {
                    var url = endpoint.Substring(method.Length + 1);
                    var sent = _requestsSent.GetValueOrDefault(endpoint, 0);
                    var success = _requestsSuccess.GetValueOrDefault(endpoint, 0);
                    var errors = _requestsError.GetValueOrDefault(endpoint, 0);
                    var timeouts = _requestsTimeout.GetValueOrDefault(endpoint, 0);
                    var pending = sent - success - errors - timeouts;

                    if (sent > 0)
                    {
                        var endpointSuccessRate = success * 100.0 / sent;
                        var statusParts = new List<string>();
                        
                        if (success > 0) statusParts.Add($"{success} ✅");
                        if (errors > 0) statusParts.Add($"{errors} ❌");
                        if (timeouts > 0) statusParts.Add($"{timeouts} ⏰");
                        if (pending > 0) statusParts.Add($"{pending} ⏳");

                        var statusText = string.Join(", ", statusParts);
                        Console.WriteLine($"   {url} - {sent} enviadas ({statusText}, {endpointSuccessRate:F1}%)");
                    }
                }
            }

            // Totales generales
            var totalSent = _requestsSent.Values.Sum();
            var totalSuccess = _requestsSuccess.Values.Sum();
            var totalErrors = _requestsError.Values.Sum();
            var totalTimeouts = _requestsTimeout.Values.Sum();
            var totalPending = totalSent - totalSuccess - totalErrors - totalTimeouts;
            var totalSuccessRate = totalSent > 0 ? (totalSuccess * 100.0 / totalSent) : 0;

            Console.WriteLine($"\n📈 Total general: {totalSent} enviadas ({totalSuccess} ✅, {totalErrors} ❌, {totalTimeouts} ⏰, {totalPending} ⏳, {totalSuccessRate:F1}% éxito)");
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

    // Métodos privados existentes (sin cambios)
    private object ProcessPostBodyCopy(object body, string url)
    {
        try
        {
            Dictionary<string, object>? dict;

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
                var serialized = JsonSerializer.Serialize(existingDict);
                dict = JsonSerializer.Deserialize<Dictionary<string, object>>(serialized);
            }
            else
            {
                return body;
            }

            if (dict == null) return body;
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
            else if (dict[key] is string str)
            {
                if (str.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    dict[key] = $"{url}+{RandomStringGenerator.GenerateAlphanumeric(8)}";
                }
                else if (str.Equals("numero", StringComparison.OrdinalIgnoreCase))
                {
                    dict[key] = int.Parse(RandomStringGenerator.GenerateNumbers(8));
                }
            }
        }
    }

    private object ConvertJsonElement(JsonElement element, string url)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when element.GetString()?.Equals("string", StringComparison.OrdinalIgnoreCase) == true
                => $"{url}+{RandomStringGenerator.GenerateAlphanumeric(8)}",
            JsonValueKind.String when element.GetString()?.Equals("numero", StringComparison.OrdinalIgnoreCase) == true
                => int.Parse(RandomStringGenerator.GenerateNumbers(8)),
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
}