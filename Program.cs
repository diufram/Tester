using Tester.Services;
using System.Text;
using System.Text.RegularExpressions;
using Tester.Models;
using Tester.Utils;
using DotNetEnv;

// Cargar variables de entorno desde el archivo .env
Env.Load();

// Leer configuración desde variables de entorno
var token = Environment.GetEnvironmentVariable("TOKEN") ?? "Tu-Token";
int minDelayMs = int.Parse(Environment.GetEnvironmentVariable("MIN_DELAY_MS") ?? "150");
int maxDelayMs = int.Parse(Environment.GetEnvironmentVariable("MAX_DELAY_MS") ?? "450");

// === Configuración simple ===
Console.OutputEncoding = Encoding.UTF8;

// Definir las operaciones de prueba (equivalente a tu arreglo original)
var operations = new OperationRequest[]
{
    new()
    {
        Url = "tu-url",
        Method = "GET",
        Token = token  // Token Bearer opcional
    },
    new()
    {
        Url = "tu-url",
        Method = "GET",
    },
    new() 
    { 
        Url = "tu-url", 
        Method = "POST",
        Body = new { 
            nombre = RandomStringGenerator.GenerateAlphanumeric(8),  // dsgnsjkgdnskdjgsdgd
            codigo = RandomStringGenerator.GenerateCode(6)  //jghskhg           
         },
        Token = token,
    },
    new() 
    { 
        Url = "tu-url", 
        Method = "POST",
        Body = new { 
            nombre = RandomStringGenerator.GenerateAlphanumeric(8),  // dsgnsjkgdnskdjgsdgd
            codigo = RandomStringGenerator.GenerateCode(6)  //jghskhg           
        },
    },
    new() 
    { 
        Url = "tu-url", 
        Method = "PATCH",
        Body = new { 
            nombre = RandomStringGenerator.GenerateAlphanumeric(8),  // dsgnsjkgdnskdjgsdgd
            codigo = RandomStringGenerator.GenerateCode(6)  //jghskhg           
         },
        Token = token,
    },
    new() 
    { 
        Url = "tu-url", 
        Method = "PUT",
        Body = new { 
            nombre = RandomStringGenerator.GenerateAlphanumeric(8),  // dsgnsjkgdnskdjgsdgd
            codigo = RandomStringGenerator.GenerateCode(6)  //jghskhg           
         },
        Token = token,
    },

};



// Manager simplificado
var manager = new WorkerManager(operations, minDelayMs, maxDelayMs);

// Lanzamos 1 worker inicial
manager.AddWorkers(1);

Console.WriteLine("🚀 Testeador de API (.NET) – Versión Simplificada");
Console.WriteLine("Comandos disponibles:");
Console.WriteLine("  add N | + N        -> agrega N workers");
Console.WriteLine("  remove N | - N     -> quita N workers");
Console.WriteLine("  set N | = N        -> fija exactamente N workers");
Console.WriteLine("  status             -> muestra conteo de workers");
Console.WriteLine("  stop               -> detiene todos los workers");
Console.WriteLine("  quit | q           -> salir\n");

string? line;
while ((line = Console.ReadLine()) is not null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

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
                manager.RemoveWorkers(n);
                break;
            case "=":
                manager.SetWorkers(n);
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
                manager.RemoveWorkers(n);
                break;
            case "set":
                manager.SetWorkers(n);
                break;
        }
        continue;
    }

    // Otros comandos
    switch (lower)
    {
        case "status":
            manager.ShowStatus();
            break;

        case "stop":
            manager.StopAll();
            break;

        case "quit":
        case "q":
            manager.StopAll();
            return;

        default:
            Console.WriteLine("❌ Comando no reconocido.");
            Console.WriteLine("Usa: add N | remove N | set N | (+/-/=) N | status | stop | quit");
            break;
    }
}
