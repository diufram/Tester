using Tester.Services;
using System.Text;
using System.Text.RegularExpressions;
using Tester.Models;

// === Configuración simple ===
Console.OutputEncoding = Encoding.UTF8;

// Arreglo estático de OperationRequest (edítalo a tu necesidad):
var operations = new OperationRequest[]
{
    new(OperationType.GetAll,  "http://localhost:5239/api/alumno"),
    new(OperationType.GetById, "http://localhost:5239/api/alumno/1"),
    new(OperationType.Create,
        "http://localhost:5239/api/alumno",
        new {
        nombre= "JAJJAJAJJAJA",
        ppa= 0,
        telefono= 0,
        registro= 0
        }),

    new(OperationType.Update,
        "http://localhost:5239/api/alumno/4",
        new {
        nombre= "JAJJAJAJJAJA",
        ppa= 0,
        telefono= 0,
        registro= 0
        }),

    new(OperationType.Delete, "http://localhost:5239/api/alumno/1"),
};

// Delays entre requests por worker (ajustables)
TimeSpan minDelay = TimeSpan.FromMilliseconds(150);
TimeSpan maxDelay = TimeSpan.FromMilliseconds(450);

// Manager
var manager = new WorkerManager(operations, minDelay, maxDelay);

// Lanzamos 1 worker inicial
manager.AddWorkers(1);

Console.WriteLine("Testeador (.NET 8) – arreglo estático + workers dinámicos");
Console.WriteLine("Comandos:");
Console.WriteLine("  add N | + N        -> agrega N workers");
Console.WriteLine("  remove N | - N     -> quita N workers");
Console.WriteLine("  set N | = N        -> fija exactamente N workers");
Console.WriteLine("  pause | resume | toggle");
Console.WriteLine("  status             -> muestra conteo/estado");
Console.WriteLine("  quit | q           -> salir\n");

string? line;
while ((line = Console.ReadLine()) is not null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

    // Normalizamos
    var lower = line.ToLowerInvariant();

    // Regex para "+ 3", "- 2", "= 10"
    var m = Regex.Match(lower, @"^([+\-=])\s*(\d+)$");
    if (m.Success)
    {
        var op = m.Groups[1].Value;
        var n = int.Parse(m.Groups[2].Value);

        switch (op)
        {
            case "+":
                manager.AddWorkers(n);
                break;
            case "-":
                await manager.RemoveWorkersAsync(n);
                break;
            case "=":
                await manager.SetWorkersAsync(n);
                break;
        }
        continue;
    }

    // Palabras clave: add/remove/set
    m = Regex.Match(lower, @"^(add|remove|set)\s+(\d+)$");
    if (m.Success)
    {
        var verb = m.Groups[1].Value;
        var n = int.Parse(m.Groups[2].Value);

        switch (verb)
        {
            case "add":
                manager.AddWorkers(n);
                break;
            case "remove":
                await manager.RemoveWorkersAsync(n);
                break;
            case "set":
                await manager.SetWorkersAsync(n);
                break;
        }
        continue;
    }

    // Otros comandos
    switch (lower)
    {
        case "pause":
            manager.Pause();
            break;

        case "resume":
            manager.Resume();
            break;

        case "toggle":
            manager.TogglePause();
            break;

        case "status":
            Console.WriteLine($"Workers: {manager.Count} | Estado: {(manager.IsPaused ? "PAUSADO" : "EJECUTANDO")}");
            break;

        case "quit":
        case "q":
            await manager.StopAllAsync();
            return;

        default:
            Console.WriteLine("Comando no reconocido. Usa: add N | remove N | set N | (+/-/=) N | pause | resume | toggle | status | quit");
            break;
    }
}
