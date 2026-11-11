---
title: "Architecture"
type: project-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, documentation]
---

# Architecture

## 1. Objetivo

Documentar la arquitectura técnica de Launcher, sus componentes principales, los contratos entre módulos y los flujos de inicialización y navegación.

El documento diferencia arquitectura comprobada e información todavía pendiente de investigación.

## 2. Visión general

Launcher es una aplicación de escritorio Windows construida con C# y WPF sobre .NET 10.

La arquitectura observada combina:

- Una aplicación principal WPF.
- Un conjunto de servicios centrales.
- Un sistema de módulos basado en `IModule`.
- Vistas WPF independientes por módulo.
- Componentes visuales compartidos.
- Modelos y servicios de persistencia.
- Servicios de integración con Windows y aplicaciones externas.

La ventana principal actúa como coordinadora de navegación y composición de los módulos.

## 3. Arquitectura lógica

mermaid

```
flowchart TD
    P["Program.cs"]
    V["Velopack"]
    A["App.xaml / App.xaml.cs"]
    M["MainWindow"]
    I["IModule"]
    C["ModuleCatalog"]
    S["Core Services"]
    U["UI Controls / Themes"]
    MOD["Functional Modules"]
    VIEW["Module Views"]

    P --> V
    V --> A
    A --> M
    M --> I
    M --> C
    M --> S
    M --> U
    I --> MOD
    MOD --> VIEW
    VIEW --> S
    VIEW --> U
```

El diagrama representa las relaciones arquitectónicas identificadas. Las dependencias exactas entre cada servicio y módulo todavía deben completarse.

## 4. Capa de arranque

### 4.1 Program.cs

Archivo: `Program.cs`

Responsabilidad:

- Punto de entrada personalizado.
- Ejecución inicial de Velopack.
- Inicialización de la aplicación WPF.

Flujo observado:

1. Windows ejecuta `Launcher.Program.Main()`.
2. Se ejecuta `VelopackApp.Build().Run()`.
3. Se crea una instancia de `App`.
4. Se llama a `InitializeComponent()`.
5. Se ejecuta `app.Run()`.

El orden es deliberado: Velopack debe gestionar sus operaciones de instalación, actualización o desinstalación antes de que WPF arranque.

### 4.2 App.xaml

Define los recursos y la configuración global de la aplicación WPF.

Pendiente de inspección detallada de:

- Diccionarios de recursos.
- Recursos compartidos.
- Estilos globales.
- Configuración de ventanas.

### 4.3 App.xaml.cs

Responsabilidad comprobada:

- Control de instancia única.
- Inicialización de servicios de logging.
- Registro de gestores de excepciones.
- Inicialización del tema.
- Creación de la ventana principal.

#### Instancia única

Se utiliza un mutex y un evento de Windows.

En DEBUG se utilizan identificadores diferentes de los utilizados en producción.

Cuando se inicia una segunda instancia:

1. Detecta que ya existe otra instancia.
2. Señala el evento de comunicación.
3. La instancia original recibe la señal.
4. La ventana original solicita mostrarse.
5. La segunda instancia termina.

#### Gestión de errores

Se registran tres categorías de excepciones:

- `DispatcherUnhandledException`: excepciones no controladas en la interfaz.
- `AppDomain.CurrentDomain.UnhandledException`: excepciones no controladas del dominio.
- `TaskScheduler.UnobservedTaskException`: excepciones no observadas de tareas asíncronas.

Los errores se envían a `LogService`.

## 5. Ventana principal y navegación

Archivo: `MainWindow.xaml.cs`

`MainWindow` es el coordinador principal de la interfaz.

Sus responsabilidades observadas son:

- Crear y registrar los módulos.
- Construir el menú lateral.
- Crear los botones de navegación.
- Gestionar el módulo inicial.
- Mostrar las vistas.
- Mantener una caché de vistas.
- Recordar el último módulo utilizado.
- Gestionar actualizaciones.
- Controlar el comportamiento visual de la ventana.

### 5.1 Registro de módulos

Se utiliza una lista:

`List<IModule>`

Cada módulo se instancia explícitamente en `MainWindow`.

El registro actual observado contiene:

|Módulo|Clase registrada|
|---|---|
|Dashboard|`DashboardModule`|
|Games|`GamesModule`|
|Folders|`FoldersModule`|
|Media|`MediaModule`|
|Music|`MusicModule`|
|Network|`NetworkModule`|
|ImageGen|`ImageGenModule`|
|Models|`ModelsModule`|
|Commands|`CommandsModule`|
|Devices|`DevicesModule`|
|Monitor|`MonitorModule`|
|Processes|`ProcessesModule`|
|Settings|`SettingsModule`|

**Observación importante:** Assistant no aparece en el registro explícito de esta versión del código.

Esto no significa que deba excluirse del inventario del proyecto. Se mantiene como módulo oficial del alcance funcional y deberá investigarse su implementación y estado de integración.

### 5.2 Contrato IModule

Archivo identificado:

`Core/Interfaces/IModule.cs`

El contrato establece cinco propiedades o métodos:

|Elemento|Tipo|Responsabilidad|
|---|---|---|
|`Id`|string|Identificador estable|
|`DisplayName`|string|Nombre visible|
|`Icon`|string|Icono del menú|
|`Order`|int|Orden de navegación|
|`CreateView()`|UserControl|Creación de la vista|

El contrato permite que `MainWindow` gestione módulos sin conocer sus detalles internos.

La ventana necesita conocer la identidad, presentación y vista de cada módulo, pero no su implementación funcional.

### 5.3 ModuleCatalog

Archivo: `Core/Services/ModuleCatalog.cs` (ubicación declarada en README; pendiente de inspección directa).

Responsabilidad documentada:

- Exponer el conjunto de módulos registrados.
- Permitir que otros componentes consulten los módulos.
- Facilitar la configuración del módulo inicial.

`MainWindow` registra la lista mediante:

`ModuleCatalog.Register(_modules)`

Pendiente: verificar implementación, almacenamiento interno y comportamiento ante registros duplicados.

### 5.4 Navegación

La navegación funciona mediante los siguientes pasos:

1. `BuildSidebar()` recorre los módulos.
2. Se crea un `NavButton` por módulo.
3. El identificador del módulo se almacena en `Tag`.
4. El evento `Checked` llama a `NavigateTo(module)`.
5. Se obtiene o crea la vista.
6. Se asigna a `ContentArea.Content`.

### 5.5 Caché de vistas

`MainWindow` mantiene:

`Dictionary<string, UserControl> _viewCache`

La primera vez que se abre un módulo:

- Se ejecuta `CreateView()`.
- Se guarda la vista.
- Se muestra en el área de contenido.

Las siguientes visitas reutilizan la misma instancia.

Consecuencia arquitectónica:

Los módulos deben gestionar correctamente el ciclo de vida de sus vistas y suscripciones a eventos. No se debe asumir que navegar fuera de un módulo destruye su vista.

## 6. Capa Core

Directorio: `Core/`

Según la estructura documentada del repositorio, contiene las siguientes áreas:

### Interfaces

Contratos compartidos.

Ejemplo comprobado:

- `IModule`
- `IGameProvider` (declarado en README; pendiente de inspección directa).

### Models

Modelos compartidos y estructuras de datos.

La documentación identifica:

- Game.
- FolderShortcut.
- CommandShortcut.
- MediaAppShortcut.
- NowPlayingInfo.
- AudioSessionInfo.
- SystemSnapshot.
- DriveStat.
- ProcessLoadInfo.
- AssistantMessage.
- Enumeraciones de dominio.

La ubicación y utilización efectiva de cada modelo deberá contrastarse con los archivos actuales.

### Services

Servicios centrales y compartidos.

La documentación identifica:

- AppSettingsService.
- LibraryPathsService.
- CoverArtService.
- IconExtractionService.
- FolderShortcutsService.
- MediaAppsService.
- NowPlayingService.
- AudioMixerService.
- SystemMonitorService.
- WindowBehaviorService.
- GlobalHotkeyService.
- StartupService.
- UpdateService.
- LogService.
- OllamaService.
- ModuleCatalog.

`GameLibraryService` está situado en `Modules/Games/`, no en Core, según el README.

### Converters

Conversores utilizados por la interfaz WPF.

### Mvvm

Infraestructura MVVM ligera:

- `ViewModelBase`.
- `RelayCommand`.

No se declara el uso de un framework MVVM externo.

## 7. Capa UI

Directorio: `UI/`

Responsabilidad:

- Componentes visuales reutilizables.
- Temas.
- Controles compartidos.

Elementos identificados en README:

- `Themes/Theme.Dark.xaml`.
- `Controls/NavButton.cs`.

El tema utiliza recursos dinámicos y define estilos visuales compartidos.

Pendiente:

- Inventario completo.
- Dependencias visuales de los módulos.
- Recursos XAML adicionales.
- Comprobación de estilos sin referencias.

## 8. Arquitectura de módulos

Los módulos siguen el contrato `IModule`.

El patrón documentado para crear un módulo es:

text

```
Model
  |
  v
Service
  |
  v
Module
  |
  v
View
```

Este es el orden conceptual de construcción descrito en el README, no necesariamente el orden de inicialización real de todos los módulos.

La vista es devuelta mediante `CreateView()`.

Los módulos pueden utilizar servicios compartidos de Core y componentes de UI.

### Inventario funcional obligatorio

Todos los módulos deben tener documentación propia:

- [[../02-Modules/ASSISTANT|Assistant]]
- [[../02-Modules/COMMANDS|Commands]]
- [[Dashboard|Dashboard]]
- [[../02-Modules/DEVICES|Devices]]
- [[../02-Modules/FOLDERS|Folders]]
- [[../02-Modules/GAMES|Games]]
- [[../02-Modules/IMAGEGEN|ImageGen]]
- [[../02-Modules/MEDIA|Media]]
- [[../02-Modules/MODELS|Models]]
- [[../02-Modules/MONITOR|Monitor]]
- [[../02-Modules/MUSIC|Music]]
- [[../02-Modules/NETWORK|Network]]
- [[../02-Modules/PROCESSES|Processes]]
- [[../02-Modules/SETTINGS|Settings]]

## 9. Persistencia

La documentación del repositorio describe persistencia mediante archivos JSON.

Se deben investigar los siguientes aspectos:

- Servicios responsables de lectura y escritura.
- Ubicación de los archivos.
- Modelos serializados.
- Compatibilidad entre versiones.
- Gestión de errores.
- Migraciones.
- Integridad de los datos.

No se debe introducir persistencia adicional en los módulos sin estudiar primero los servicios existentes.

## 10. Integraciones externas

Las dependencias declaradas indican posibles integraciones con:

- Velopack: instalación y actualización.
- NAudio: procesamiento y control de audio.
- System.Management: información y gestión del sistema.
- Microsoft.Playwright: automatización de navegador.
- System.Data.OleDb: acceso a fuentes de datos compatibles.
- TagLibSharp: lectura y manipulación de metadatos multimedia.

La utilización efectiva de cada dependencia deberá documentarse en DEPENDENCIES.md.

La integración de Ollama está descrita en el README, pero debe verificarse mediante la inspección de `OllamaService` y los componentes del módulo Assistant.

## 11. Decisiones arquitectónicas observadas

|ID|Decisión|Estado|
|---|---|---|
|ARC-001|WPF como tecnología de interfaz|Confirmada|
|ARC-002|Módulos basados en IModule|Confirmada|
|ARC-003|Registro explícito en MainWindow|Confirmada|
|ARC-004|Caché de vistas por identificador|Confirmada|
|ARC-005|MVVM ligero propio|Declarada; pendiente de inspección|
|ARC-006|Servicios singleton en determinados casos|Declarada; pendiente de inventario|
|ARC-007|Persistencia JSON|Declarada; pendiente de verificación|

## 12. Riesgos arquitectónicos que investigar

### ARC-R01 — Registro manual de módulos

La lista de módulos se construye explícitamente en `MainWindow`.

Riesgo potencial:

- El registro puede quedar desincronizado de los módulos disponibles.
- Cada alta o baja requiere modificar la ventana principal.

No se considera necesariamente un defecto: puede ser una decisión deliberada para un proyecto de este tamaño.

### ARC-R02 — Ciclo de vida de vistas cacheadas

Las vistas se mantienen en memoria después de crearse.

Riesgo potencial:

- Suscripciones a eventos persistentes.
- Recursos que no se liberan.
- Temporizadores activos al abandonar una vista.
- Estado compartido inesperado.

### ARC-R03 — Servicios compartidos

Los servicios singleton pueden simplificar la comunicación, pero también crear dependencias implícitas.

Se investigarán las responsabilidades, concurrencia y gestión de recursos.

### ARC-R04 — Ausencia de Assistant en el registro

El módulo Assistant forma parte del alcance funcional definido, pero no aparece en la lista explícita observada en `MainWindow.xaml.cs`.

Se debe determinar si existe una implementación independiente, si está incompleta o si falta integrarla.

## 13. Próximos pasos

- [ ] Inspeccionar todos los archivos de `Core/Interfaces`.
- [ ] Inspeccionar todos los modelos.
- [ ] Inventariar servicios y sus dependencias.
- [ ] Analizar los 14 módulos individualmente.
- [ ] Reconstruir los flujos de navegación.
- [ ] Analizar la persistencia.
- [ ] Identificar relaciones entre servicios.
- [ ] Generar el grafo real de dependencias.
- [ ] Verificar el ciclo de vida de las vistas.
- [ ] Comprobar la inicialización y liberación de recursos.

## 14. Documentos relacionados

- [[../../Home|Home]]
- [[../00-Project/PROJECT_CONTEXT|Project Context]]
- [[../00-Project/CURRENT_STATE|Current State]]
- [[DIRECTORY_MAP|Directory Map]]
- [[DEPENDENCIES|Dependencies]]
- [[../02-Modules/MODULES|Module Catalog]]
- [[../03-Decisions/DECISIONS|Technical Decisions]]

## 15. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Primera reconstrucción arquitectónica basada en archivos fuente|
|Pendiente|Completar dependencias internas y arquitectura de módulos|

## Reconciliación de arquitectura — 2026-11-11

MainWindow registra explícitamente 13 módulos. ModuleCatalog recibe esa lista; SearchModule no forma parte de ella. AssistantWindow es una ventana independiente, no una implementación de IModule.

MainWindow cachea las vistas creadas por ID y las reutiliza. Por ello, Unloaded no equivale a destrucción: los recursos y eventos deben gestionarse según el ciclo de vida real.

El contrato IModule no incluye métodos de activación/desactivación ni IDisposable. Los riesgos se detallan en [[../03-Audit/Findings|Findings]].
