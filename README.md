# Launcher

Aplicación de escritorio modular para Windows, desarrollada en C# / WPF sobre .NET 10.

> Este README describe el snapshot auditado y separa código identificado de comportamiento probado. No se ha ejecutado build ni la aplicación durante esta actualización documental.

## Estado de referencia

| Campo | Valor |
|---|---|
| Repositorio | https://github.com/ferranmi/Launcher-personal |
| Rama de referencia | main |
| Commit auditado | 5c621accde912fc8dda48873fb44d4d93857901a |
| Versión | 0.1.53 |
| Target Framework | net10.0-windows10.0.19041.0 |
| Plataforma | Windows |
| UI | WPF + XAML |
| Estado de validación | Auditoría estática; build y pruebas pendientes |

## Propósito

Launcher centraliza utilidades del equipo mediante módulos: inicio, juegos, carpetas, multimedia, música, red, imágenes IA, modelos, comandos, dispositivos, monitorización, procesos y configuración.

La presencia de una vista o clase no garantiza que todas sus funciones estén probadas.

## Arquitectura

- `Program.cs`: punto de entrada propio; ejecuta Velopack antes de iniciar WPF.
- `App.xaml.cs`: instancia única, logging y manejadores globales.
- `MainWindow.xaml.cs`: registra módulos, construye navegación y cachea vistas.
- `Core/`: interfaces, modelos, servicios, MVVM y converters.
- `Modules/`: funcionalidades.
- `UI/`: temas y controles compartidos.
- `Docs/Launcher-Vault/`: documentación técnica Obsidian.

Los módulos registrados implementan `IModule` y se añaden explícitamente en MainWindow. El contrato expone `Id`, `DisplayName`, `Icon`, `Order` y `CreateView()`. La vista creada se conserva en caché por ID.

## Módulos registrados en el commit auditado

|Módulo|ID|Clase|
|---|---|---|
|Inicio|dashboard|DashboardModule|
|Juegos|games|GamesModule|
|Carpetas|folders|FoldersModule|
|Multimedia|media|MediaModule|
|Musica|music|MusicModule|
|Red|network|NetworkModule|
|Imagenes IA|imagegen|ImageGenModule|
|Modelos|models|ModelsModule|
|Comandos|commands|CommandsModule|
|Dispositivos|devices|DevicesModule|
|Monitor|monitor|MonitorModule|
|Procesos|processes|ProcessesModule|
|Configuracion|settings|SettingsModule|

**Diferencias relevantes:** `Modules/Search/SearchModule.cs` implementa IModule pero no se registra. `Modules/Assistant/AssistantWindow.xaml.cs` existe, pero no hay AssistantModule registrado. Estas decisiones están pendientes; no se ha alterado el código.

## Dependencias NuGet declaradas

|Paquete|Versión|
|---|---|
|Velopack|1.2.161|
|NAudio|2.2.1|
|System.Management|10.0.*|
|Microsoft.Playwright|1.*|
|System.Data.OleDb|9.0.0|
|TagLibSharp|2.3.0|

La declaración no implica uso efectivo de cada paquete. System.Management y Microsoft.Playwright utilizan versiones flotantes.

## Datos y configuración

Varios servicios guardan JSON bajo `%AppData%\Launcher`. Las rutas y nombres exactos deben consultarse en cada servicio; no asumir que todos los módulos comparten un único archivo. Algunos datos tienen otras fuentes: por ejemplo, MusicTrack se construye desde etiquetas de los archivos de audio.

## Compilar y ejecutar

Requisitos:
- Windows.
- .NET 10 SDK.
- Herramientas de desarrollo compatibles con WPF.

Desde la carpeta que contiene `Launcher.csproj`:

```powershell
dotnet restore
dotnet build -c Debug
dotnet build -c Release
dotnet run
```

No se afirma que estos comandos se hayan ejecutado en esta auditoría. La actualización mediante Velopack debe probarse en una instalación creada con el instalador, no solo con dotnet run.

## Publicación

Existe `publish.ps1` y el proyecto declara Velopack 1.2.161. Antes de publicar:
1. Revisar Version en Launcher.csproj.
2. Ejecutar build Release.
3. Ejecutar el script y comprobar los artefactos.
4. Probar instalación y actualización en entorno de prueba.
5. No distribuir hasta validar.

## Hallazgos prioritarios

- Autologin web admite HTTP y no fija el origen final antes de rellenar credenciales.
- SteamApiKey se serializa en settings.json.
- LocalSend desactiva la validación del certificado TLS.
- Persistencia JSON heterogénea y no atómica.
- Playwright no tiene limpieza central visible en los cierres inspeccionados.
- AudioMixerService no libera explícitamente recursos COM.
- Importación musical compara rutas mediante StartsWith.
- Muestreo de sistema puede duplicarse y ejecutarse en Dispatcher.

Detalle, condiciones y certeza: consultar `Docs/Launcher-Vault/03-Audit/Findings.md`.

## Forma de trabajo

- Mantener Windows, C#, WPF y .NET 10.
- No añadir frameworks MVVM ni dependencias sin justificar y aprobar.
- Respetar arquitectura existente.
- Cambios pequeños e incrementales.
- Mantener compatibilidad de JSON.
- No declarar pruebas realizadas sin evidencia.
- Si cambia comportamiento visible, actualizar versión y README.

## Documentación

La bóveda Obsidian está en `Docs/Launcher-Vault/`. Su entrada es `Home.md`. Incluye contexto, estado, arquitectura, mapa de directorios, catálogo de módulos, auditoría, decisiones, roadmap e instrucciones para agentes.

La auditoría estática se refiere al commit indicado arriba; revisar el HEAD de main antes de tomar decisiones de implementación.
