# Tester – Cargador HTTP con Workers en .NET

Este proyecto es un **tester de carga** escrito en **.NET 8**, que lanza múltiples *workers* concurrentes para simular tráfico HTTP.  
Permite agregar, quitar o pausar workers en tiempo real mediante comandos de consola.

## 🚀 Levantar el proyecto

En la raíz del proyecto, ejecuta:

dotnet run

## 🚀 Comandos una vez corre el proyecto

add N       | + N    -> agrega N workers
remove N    | - N    -> quita N workers
set N       | = N    -> fija exactamente N workers
pause                -> pausa los workers (soft pause)
resume               -> reanuda la ejecución
toggle               -> alterna entre pausar y reanudar
status               -> muestra el conteo de workers y estado
quit        | q      -> detiene todos los workers y sale
