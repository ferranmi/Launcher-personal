> **ESTADO ACTUALIZADO — 2026-11-12**
>
> La parte principal de este README conserva un analisis historico de 2026-11-06 para no perder decisiones, contexto y pruebas propuestas. Sus estados por modulo y afirmaciones sobre dependencias no representan necesariamente el estado actual. Para el estado contrastado con codigo, prevalece este bloque.
>
> | Campo | Estado actual comprobado |
> |---|---|
> | Rama de implementacion | `feature/settings-centralized-management` |
>> | Version | `0.1.54` |
> | Framework | `net10.0-windows10.0.19041.0` |
> | Modulos registrados | 13 |
> | Velopack | Declarado en Launcher.csproj, version 1.2.161 |
> | Build / pruebas | No ejecutados: el entorno de trabajo no dispone del SDK .NET ni de Windows/WPF |
>
> **Settings — implementacion inicial en la rama de trabajo:** pagina vertical con navegacion lateral, categoria activa al desplazar, sesion temporal y botones Guardar/Descartar. La sesion incluye `config.json`, `settings.json`, `imagegen.json`, `models.json`, el registro de inicio de Windows y la credencial de Civitai en el Administrador de credenciales. Los cambios se aplican a los modulos tras guardar. El intervalo compartido de Monitor/Procesos permite sobrescrituras individuales, identificadas en la interfaz. Music y Models enlazan sus ajustes con la categoria correspondiente.
>
> **Limites pendientes de validar:** el guardado coordinado entre varios destinos usa escritura atomica por archivo y rollback compensatorio ante errores detectados; no es una transaccion atomica frente a un corte de energia durante el proceso. SteamApiKey sigue almacenada en `settings.json`. No se ha compilado ni probado visualmente en Windows. El intervalo de Monitor/Procesos ya permite heredar el valor compartido o sobrescribirlo por modulo.
>
> **Módulos registrados:** Dashboard, Games, Folders, Media, Music, Network, ImageGen, Models, Commands, Devices, Monitor, Processes y Settings.
>
> **Elementos no registrados:** SearchModule implementa IModule pero no se registra en MainWindow. AssistantWindow existe como ventana independiente, sin AssistantModule.
>
> **Hallazgos estáticos prioritarios:** SteamApiKey en JSON; autologin web que permite HTTP y no fija el origen tras redirecciones; LocalSend acepta certificados sin validacion; persistencia JSON heterogenea; sesiones Playwright sin cierre explicito visible; recursos CoreAudio sin liberacion explicita; comparacion de rutas por prefijo en la biblioteca musical; muestreo de sistema potencialmente duplicado/sincrono. Ver `Docs/Launcher-Vault/03-Audit/Findings.md`.
>
> El analisis historico que sigue es contexto, no una especificacion vigente. Cuando contradiga el bloque actual o el codigo de la rama de implementacion, se considera superado y debe revalidarse antes de implementar.

---

# Launcher — README de estado y traspaso

> Documento pensado para que **otra persona o IA** pueda continuar el proyecto sin haber estado en las conversaciones anteriores.
> Sustituye al `README.md` antiguo del repositorio (que está desactualizado y contiene marcadores de conflicto de git sin resolver: `<<<<<<< HEAD`, `=======`, `>>>>>>>`).

| Campo | Valor |
|---|---|
| Fecha del documento histórico | 2026-11-06 |
| Proyecto | Launcher (aplicación de escritorio modular) |
| Stack | C# · WPF · .NET 10 (`net10.0-windows10.0.19041.0`) · solo Windows |
| Versión del snapshot histórico descrito abajo | `0.1.0` (no vigente) |
| Repositorio | https://github.com/ferranmi/Launcher-personal (rama `main`) |
| Lista TODO viva | https://claude.ai/artifact/LP2msaApaDb6yqaEFaU6p7 (artifact con base de datos compartida, colección `tasks`; puede no ser accesible para terceros) |
| Idioma de trabajo | Español (interfaz, comentarios y conversación) |

---

## 0. Cómo leer este documento (leyenda y fiabilidad)

### 0.1 Estados

| Marca | Significado |
|---|---|
| ✅ | Hecho (según código o según lo declarado por el usuario) |
| 🟡 | Parcial o con limitaciones relevantes |
| ⏳ | Pendiente / pedido pero no hecho |
| 🚫 | No se puede, o bloqueado por la plataforma |
| ❓ | No verificado: no hay evidencia en ninguna dirección |

### 0.2 Origen de cada afirmación

| Etiqueta | Fuente |
|---|---|
| **[C]** | Código del *snapshot* adjunto (lectura directa de los archivos) |
| **[M]** | Memoria del proyecto (lo que el usuario dijo en sesiones anteriores, hasta 2026-10-28). Registra **peticiones y decisiones**, no siempre su finalización |
| **[D]** | Documento adjunto (README de Lora Studio, "Asistente IA Local para Windows", exportación de una conversación de ChatGPT del 2026-11-02) |
| **[I]** | Inferencia de quien redacta este documento; hay que contrastarla |

### 0.3 Aviso importante: este documento se escribió leyendo, no ejecutando

Quien redactó este README **no pudo compilar ni ejecutar** la aplicación (es WPF/Windows-only). **Ninguna prueba descrita aquí se ha ejecutado.** Todo lo que figure como "funciona" procede de lo que el usuario reportó, no de una verificación independiente. La sección 8 contiene una batería de comprobaciones para ejecutar a mano.

### 0.4 El snapshot de código NO coincide con la memoria

El conjunto de archivos adjunto parece **más antiguo** que lo que recoge la memoria del proyecto. Antes de tocar nada, hay que pedir al usuario un ZIP/`git pull` reciente de `main`.

| Tema | La memoria [M] dice | El snapshot [C] muestra |
|---|---|---|
| Pantalla de inicio / Dashboard | Existe `Modules/Dashboard`, `DashboardView`, rejilla `DashboardBoard`/`BoardBox` 12×12, `IWidget`, `WidgetCatalog`, widgets | **No existe** nada de eso; `MainWindow` no registra ningún módulo Dashboard |
| Música | Módulo en construcción (sesiones 2026-10-22 a 2026-10-24) | Sigue como `PlaceholderModule("music")` |
| Buscar | Se empezó a construir el 2026-10-21 | Sigue como `PlaceholderModule("search")` |
| ImageGen (Stable Diffusion) | En construcción desde 2026-10-27 | No existe |
| Carpetas | Explorador en panel lateral (pedido 2026-10-23) | Solo cuadrícula de tarjetas; abre con el Explorador de Windows |
| Epic | Usa `InstallLocation + LaunchExecute` con protocolo como alternativa | Solo usa el protocolo `com.epicgames.launcher://…` |
| Configuración | Color de acento, intervalo de refresco, actualización automática… | No están en `SettingsView` ni en `AppSettingsService` |
| Velopack | vpk/Velopack 1.2.161 | `Launcher.csproj` **no incluye** `PackageReference` a Velopack (pero `Program.cs` y `UpdateService.cs` lo usan) |
| Asistente (Alt+Space) | Hotkey con hook de bajo nivel | `GlobalHotkeyService` existe pero **no se usa**; `WindowBehaviorService` usa `RegisterHotKey` con otras combinaciones |
| Fondo animado, Dispositivos, UPLINK, widgets TODO/Portapapeles/Tiempo/Ofertas/Correo | Decididos/pedidos | No existen en el snapshot |

---

## 1. Resumen ejecutivo

**Qué es:** un *launcher* de escritorio para Windows con estética de terminal oscura (verde fósforo), organizado en módulos intercambiables desde una barra lateral. Pretende ser el centro de control del PC del usuario: juegos, carpetas, multimedia, comandos, monitorización, procesos, ajustes y, a medio plazo, un asistente de IA local (Ollama), generación de imágenes (AUTOMATIC1111), música, traspaso de ficheros (LocalSend/Tailscale) y una gestión de modelos de Civitai (Lora Studio).

**Estado en una frase:** hay una base sólida y 7 módulos reales (Juegos, Carpetas, Multimedia, Comandos, Monitor, Procesos, Configuración) más actualizaciones con Velopack, bandeja, instancia única y registro de errores; el resto (Música, Buscar, Asistente, Dashboard, ImageGen, Lora Studio…) está **pedido o parcialmente hecho pero no consolidado**, y hay **al menos un error de compilación probable** en el snapshot (sección 6.1).

**Dueño y forma de trabajo:** un único usuario que desarrolla con asistencia de IA, ejecuta la app publicada (instalada con `Setup.exe`, no con `dotnet run`) y quiere controlar versiones y recibir avisos de actualización.

---

## 2. Entorno, ejecución y publicación

### 2.1 Requisitos

- Windows 10 (build 19041+) o 11. `SupportedOSPlatformVersion` = `10.0.17763.0`.
- **.NET 10 SDK** (migrado desde .NET 8 el 2026-10-10 porque el soporte de .NET 8 y 9 termina el 2026-11-10) [M].
- Visual Studio o VS Code (C# Dev Kit).
- Hardware de referencia del usuario: i5-12400F, 32 GB RAM, RTX 3060 12 GB [D][M]. Relevante para el monitor de VRAM y los modelos de IA.
- **Smart App Control desactivado** en el PC del usuario: bloqueaba el `.exe` sin firmar (error 4551) [M]. No hay firma de código.

### 2.2 Paquetes NuGet (según `Launcher.csproj` del snapshot)

| Paquete | Versión | Uso |
|---|---|---|
| NAudio | 2.2.1 | Mezclador de volumen (CoreAudio) |
| System.Diagnostics.PerformanceCounter | 8.0.0 | Contadores de GPU/disco |
| System.Management | 8.0.0 | WMI (nombre y VRAM de respaldo) |
| **Velopack** | **falta en el csproj del snapshot** | Actualizaciones (`Program.cs`, `UpdateService.cs`) |

Otros ajustes del `.csproj`: `UseWPF` + `UseWindowsForms` (para `NotifyIcon` y diálogos de carpeta), `<Using Remove="System.Windows.Forms" />` (evita ambigüedades), manifiesto DPI `PerMonitorV2`, icono `Assets\launcher.ico` incluido como `Resource`.

### 2.3 Ejecutar en desarrollo

```
dotnet run          # dentro de la carpeta del proyecto
```

En depuración el *mutex* de instancia única usa nombres con sufijo `.Debug`, de modo que una compilación de desarrollo y la instalada pueden convivir.

> ⚠️ Ejecutado desde Visual Studio o desde `publish\Launcher.exe`, el estado de actualización es `NotInstalled`. Para probar actualizaciones hay que ejecutar la copia **instalada con `Setup.exe`** (`%LOCALAPPDATA%\Launcher`).

### 2.4 Publicar una versión y actualizar

1. Subir `<Version>` en `Launcher.csproj` (cada release necesita una versión distinta).
2. Ejecutar `.\publish.ps1` (junto al `.csproj`). Hace `dotnet publish -c Release -r win-x64 --self-contained true` y luego `vpk pack` hacia `%USERPROFILE%\LauncherReleases`.
3. Primera vez: instalar con el `Setup.exe` de esa carpeta. Después, las copias instaladas detectan las versiones nuevas.
4. La app busca actualizaciones 8 s después de abrir y cada 6 h (si el ajuste está activo). Cuando hay una, el texto de versión del sidebar se vuelve verde y clicable.

Notas:
- Requiere la herramienta global `vpk` instalada (`dotnet tool`), y que `UpdateSource` apunte a la carpeta de releases (por defecto `%USERPROFILE%\LauncherReleases`).
- Al ser una **carpeta local**, solo se actualizan las instalaciones de ese mismo PC (o de un recurso compartido accesible) [I].
- Decidido el 2026-10-12: la actualización debería **aplicarse automáticamente** al detectarse (ajuste "Actualizar automáticamente"). **No está en el snapshot.** ⏳

### 2.5 Datos que genera la aplicación

Todo bajo `%AppData%\Launcher\` salvo indicación contraria. No hay base de datos; solo JSON.

| Archivo / ubicación | Lo escribe | Contenido |
|---|---|---|
| `config.json` | `AppSettingsService` | Ajustes generales (arranque, ventana, atajo, tamaño de tarjetas, actualizaciones…) |
| `settings.json` | `LibraryPathsService` | Rutas de Steam y de manifiestos de Epic (**nombre confuso: no es el mismo que `config.json`**) |
| `games.json` | `GameLibraryService` | Biblioteca de juegos |
| `ignored-games.json` | `GameLibraryService` | Claves `Steam:123` de juegos eliminados que no deben reaparecer al sincronizar |
| `folders.json` | `FolderShortcutsService` | Accesos a carpetas (se siembran 6 por defecto) |
| `commands.json` | `CommandsService` | Accesos de Comandos (se siembra Kunai) |
| `media_apps.json` | `MediaAppsService` | Accesos a apps multimedia (**sin interfaz que lo use**, ver 4.3) |
| `covers\` | `CoverArtService`, `IconExtractionService` | Portadas (`{id}.jpg` / `{id}.png`) |
| `logs\launcher-AAAAMMDD.log` | `LogService` | Errores e información; se conservan 7 días |
| Registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `Launcher` | `StartupService` | Arranque con Windows; opcionalmente con `--minimized`. **Es la fuente de verdad** de "Iniciar con Windows" (no se guarda en `config.json`) |

---

## 3. Arquitectura

### 3.1 Estructura del repositorio (según el snapshot)

```
Launcher/
├── Program.cs                  # Punto de entrada: VelopackApp.Build().Run() ANTES de WPF
├── App.xaml(.cs)               # Instancia única (Mutex + EventWaitHandle), manejadores de errores globales
├── MainWindow.xaml(.cs)        # Ventana sin bordes, sidebar, registro de módulos, indicador de actualización
├── AssemblyInfo.cs, app.manifest, Launcher.csproj, publish.ps1, .gitignore
├── Assets/launcher.ico
├── Core/
│   ├── Interfaces/   IModule, IGameProvider
│   ├── Models/       Game, GameSource, FolderShortcut, CommandShortcut, CommandCategory, TerminalHost,
│   │                 MediaAppShortcut, NowPlayingInfo, AudioSessionInfo, SystemSnapshot, DriveStat,
│   │                 ProcessLoadInfo, AssistantMessage, PlaceholderModule
│   ├── Mvvm/         ViewModelBase, RelayCommand
│   ├── Converters/   BoolToVisibility, EmptyToVisibility, PathToBitmap, DepthToIndent, PlayPauseGlyph
│   └── Services/     AppSettings, LibraryPaths, Log, Startup, Update, WindowBehavior, GlobalHotkey,
│                     ModuleCatalog, CoverArt, IconExtraction, FolderShortcuts, MediaApps, NowPlaying,
│                     AudioMixer, SystemMonitor, Ollama
├── Modules/
│   ├── Games/      (view, AddEdit, ConfigureSyncFolders, GameLibraryService, Providers/Steam+Epic)
│   ├── Folders/    (view, AddEdit)
│   ├── Media/      (view, AddEditMediaAppWindow)
│   ├── Commands/   (view, service, suggestions, AddEdit)
│   ├── Monitor/    (view)
│   ├── Processes/  (view, ProcessMonitorService, ProcessGroup, ProcessRow, ProcessSample)
│   ├── Settings/   (view)
│   ├── Assistant/  (AssistantWindow, NO registrado como módulo)
│   └── Music/ Search/   # carpetas vacías (.gitkeep)
└── UI/
    ├── Themes/Theme.Dark.xaml
    └── Controls/NavButton.cs
```

### 3.2 Patrón de módulos

- Contrato `IModule` (`Id`, `DisplayName`, `Icon` glifo Segoe, `Order`, `CreateView()`).
- Registro manual en el constructor de `MainWindow` y publicación en `ModuleCatalog` (lo usa Configuración para "Módulo inicial").
- El sidebar se genera dinámicamente; las vistas se **cachean** en `_viewCache` (no se destruyen al cambiar de módulo → ver bug de Procesos, 6.2).
- Orden de construcción de un módulo: **modelo → servicio → módulo → vista**, y registrarlo en `MainWindow.xaml.cs`.
- `PlaceholderModule` pinta "modulo en construccion_" para los que aún no existen.

| Módulo | Id | Order | Clase |
|---|---|---|---|
| Juegos | `games` | 0 | `GamesModule` |
| Carpetas | `folders` | 1 | `FoldersModule` |
| Multimedia | `media` | 2 | `MediaModule` |
| Música | `music` | 3 | `PlaceholderModule` |
| Buscar | `search` | 4 | `PlaceholderModule` |
| Procesos | `processes` | 4 | `ProcessesModule` (**empata con Buscar**) |
| Monitor | `monitor` | 6 | `MonitorModule` |
| Comandos | `commands` | 10 | `CommandsModule` (comentario en código: "ajusta este valor") |
| Configuración | `settings` | 100 | `SettingsModule` |

### 3.3 MVVM

Ligero y mixto: `ViewModelBase` (`SetField`/`OnPropertyChanged`) para los modelos observables (`NowPlayingInfo`, `AudioSessionInfo`, `AudioMixerService`); los modelos de dominio (`Game`, `CommandShortcut`…) implementan `INotifyPropertyChanged` por su cuenta; las vistas usan *code-behind* abundante. `RelayCommand` existe pero no se usa en el snapshot. No hay framework MVVM externo (deliberado).

### 3.4 Tema y estilo

- Todos los colores y fuentes son `DynamicResource` definidos en `UI/Themes/Theme.Dark.xaml` (`Brush.Background`, `Brush.Accent` = `#3ECF7E`, `Font.Mono` = Cascadia Mono/Consolas, `Font.Primary` = Segoe UI, `Font.Icons` = Segoe MDL2 Assets).
- Estilos reutilizables: `CardStyle`, `BadgeStyle`, `ChipStyle`, `PrimaryButtonStyle`, `SecondaryButtonStyle`, `TextBoxStyle` (con *placeholder* vía `Tag`), `MonoTextStyle`, `MonoHeadingStyle`, `Effect.AccentGlow`, scrollbar fina.
- Un tema claro sería crear `Theme.Light.xaml` con las mismas claves y cambiar el `MergedDictionary`.
- Los iconos son **Segoe MDL2 Assets** (el código y los comentarios hablan a veces de "Segoe Fluent Icons"; los glifos usados son del rango común a ambos).

### 3.5 Convenciones de código y estilo de trabajo

- **Texto de interfaz y comentarios sin tildes ni eñes** ("Anadir", "Configuracion", "Imagenes"…). Mantenerlo por coherencia.
- Los fallos "no críticos" se tragan con `catch` y comentario explicativo (carga de JSON corrupto, contadores de rendimiento, sesiones de audio…); los importantes van a `LogService.Error`.
- Ventanas modales propias sin borde (`WindowStyle=None`, `AllowsTransparency`), arrastrables por la cabecera, con el mismo esqueleto XAML repetido (estilos `FieldLabel`, `FieldBox`, `GhostButton`, `AccentButton` copiados en cada ventana).
- **Preferencia del usuario para las entregas de código:** archivos **completos** (no *diffs* parciales, para evitar truncados y errores de fusión); indicar siempre **la ruta de destino** y si es **archivo nuevo o reemplazo**.
- **Regla de oro:** trabajar sobre los archivos reales del repositorio, **nunca** sobre copias reconstruidas de memoria [M].
- El usuario pidió (2026-10-12) ir actualizando este README a medida que se completen pendientes, entregándolo como archivo completo.

---

## 4. Estado por módulo

> En cada módulo: **Hecho** · **Limitaciones** · **Pendiente**. Las limitaciones marcadas [I] son inferidas leyendo el código.

### 4.1 Juegos (`Modules/Games`) — ✅ funcional / 🟡 con límites

**Hecho [C]**
- Cuadrícula de tarjetas con portada (o inicial del nombre), nombre y subtítulo `Origen · tiempo jugado`.
- Búsqueda en tiempo real (`Ctrl+F` enfoca; `Esc` limpia; `Enter` lanza el primer resultado).
- Filtros (chips): Todos, Favoritos, Steam, Epic, Manuales, Ocultos. Orden: Nombre, Último jugado, Más jugado, Favoritos primero.
- Añadir/editar manual (nombre, exe, argumentos, portada) con "Detectar desde una carpeta" (busca el `.exe` principal descartando instaladores por palabras clave y usando el más pesado) y "Buscar portada automáticamente".
- Arrastrar y soltar `.exe` sobre la vista.
- Menú contextual: ejecutar como administrador, abrir carpeta, editar, favorito, ocultar, eliminar (con confirmación configurable).
- Sincronización con **Steam** (`libraryfolders.vdf` + `appmanifest_*.acf` por expresiones regulares; lanza con `steam://rungameid/{appid}`) y **Epic** (manifiestos `*.item` JSON; lanza con `com.epicgames.launcher://apps/{AppName}?action=launch&silent=true`).
- Por carpetas, no por cuenta (sin OAuth). Diálogo "Carpetas de sincronización" con auto-detección (registro de Windows / rutas típicas).
- Eliminar un juego de Steam/Epic lo guarda en `ignored-games.json` para que no reaparezca; se restaura desde Configuración.
- Portadas: API pública de búsqueda de la Steam Store + `library_600x900.jpg` del CDN; caché en disco; "Reparar faltantes".
- Tiempo jugado: se mide esperando al proceso (sesiones < 1 min se ignoran).
- Sincronizar al abrir (opcional).

**Limitaciones**
- 🚫 **No se mide el tiempo jugado** de juegos lanzados por protocolo (`steam://`, `com.epicgames.launcher://`): no hay proceso al que esperar. Tampoco se cuenta si el Launcher se cierra durante la partida.
- ❓ El propio código advierte (comentarios de `SteamGameProvider`/`EpicGameProvider`) que la lectura de `.vdf`/`.item` **se escribió siguiendo el formato conocido pero no se probó contra una instalación real**. La memoria da la detección por funcional, sin detallar pruebas. Si falla: pedir el contenido de un `.acf`/`.item`.
- 🟡 Regex sencillas para el `.vdf`: formatos futuros de Valve podrían romperlas [I].
- 🟡 La detección de Steam **no filtra aplicaciones que no son juegos** (p. ej. redistribuibles de Steamworks) [I, no verificado].
- 🟡 Durante la sincronización las portadas se descargan **una a una** (`await` en bucle): lenta con bibliotecas grandes [I].
- 🟡 La búsqueda de portada toma **el primer resultado** de la Steam Store sin validarlo: puede asignar una portada equivocada. Los juegos que no están en Steam pueden quedarse sin portada [I].
- 🟡 La portada "detectada desde carpeta" es el **icono del exe** (32×32) guardado como PNG: baja calidad como portada [I].
- Código muerto: existe `PlayMenuItem_Click` pero el menú contextual de `GamesView.xaml` no tiene opción "Jugar" (se juega con doble clic/Enter).

**Pendiente [M]**
- Ficha de juego (pedida 2026-10-12): menú "Información y logros", editable; logros de Steam (clave de Web API a guardar en Configuración); enlaces que se abren con el navegador elegido/incógnito como en Comandos; documentos locales y notas. ⏳
- Ofertas de juegos (widget del Dashboard). ⏳
- Cambios visuales pedidos el 2026-10-10: botón Sincronizar con icono de refresco y botón de carpetas con icono propio. En el snapshot ya usan glifos (`&#xE895;` y `&#xE713;`); **confirmar si en la fuente MDL2 se ven bien** (el usuario veía "[]"). ❓

### 4.2 Carpetas (`Modules/Folders`) — 🟡

**Hecho [C]**
- Tarjetas de acceso a carpetas con icono (12 glifos a elegir), alta/edición/eliminación, aviso "no encontrada" si la ruta ya no existe, control de duplicados.
- Se siembran Escritorio, Documentos, Descargas, Imágenes, Música y Vídeos (`IsBuiltIn`: no se pueden eliminar).
- Doble clic abre la carpeta con `Process.Start` → **Explorador de Windows**.

**Limitaciones**
- La propiedad `Hidden` existe en el modelo pero **no hay interfaz** para ocultar/mostrar.
- Descargas se resuelve como `%USERPROFILE%\Downloads` (no hay `SpecialFolder` propio): si el usuario la reubicó, no se detecta.

**Pendiente [M]** ⏳
- Convertirlo en **explorador integrado** con panel lateral dentro del módulo, con "todas las características del Explorador de Windows" (carpetas con muchas imágenes/vídeos navegables sin abrir uno a uno, vista previa…).
- Todo "mostrar / ir a" una ubicación (desde Buscar, asistente, etc.) debe redirigir a ese panel (`FolderNavigationService`), **sin usar el Explorador de Windows**. Hoy el código hace exactamente lo contrario.
- 🚫/⚠️ "Todas las características del Explorador" es un alcance muy grande; conviene acordar un subconjunto (navegación, miniaturas, vista previa, copiar/mover, menú contextual).

### 4.3 Multimedia (`Modules/Media`) — ✅ / 🟡

**Hecho [C]**
- *Now Playing* vía WinRT `GlobalSystemMediaTransportControlsSessionManager`: funciona con Spotify, reproductor nativo y pestañas de navegador (YouTube, Netflix…). Carátula con degradado de respaldo, título, artista, app de origen, anterior/play-pausa/siguiente, barra de progreso con *seek*, fundido al cambiar de pista, doble clic en carátula/etiqueta para traer la app al frente (Spotify, Edge, Chrome, Firefox, VLC).
- Mezclador por aplicación con NAudio/CoreAudio: volumen maestro y mute, un deslizador y mute por app, seguimiento dinámico del dispositivo de salida por defecto, filtrado de sesiones expiradas, nombres amigables.
- Prioriza la sesión que está reproduciendo; si no, la "actual" de Windows.

**Limitaciones / fallos**
- 🐞 **Probable bug:** `MasterVolumeSlider_ValueChanged` pone `_isDraggingMaster = true` y **nunca vuelve a `false`**. Tras mover el deslizador una vez, deja de sincronizarse con el volumen real del sistema (por ejemplo, teclas de volumen).
- Si no hay sesión, el estado vacío sale pero la carátula anterior no se limpia en el modelo [I].
- Solo lista sesiones del **dispositivo de salida predeterminado**; el icono falla en procesos protegidos (queda sin icono).
- *Seek*, siguiente y anterior dependen de que la app los soporte.
- 🟡 **Código huérfano:** `MediaAppShortcut`, `MediaAppsService` y `AddEditMediaAppWindow` (accesos a apps multimedia) existen pero **ninguna vista los muestra**.

**Pendiente [M]** ⏳ widget de Last.fm dentro de Multimedia; integración con el módulo Música.

### 4.4 Música (`Modules/Music`) — ⏳ no implementado en el snapshot

Placeholder. **Pedido/decidido [M]:**
- Hacerlo **desde cero** (no seguir la especificación "MusicManager" completa, aunque se compartió: Downloads + MusicLibrary, TagLib#, MusicBrainz, Cover Art Archive, FFmpeg, yt-dlp).
- Alcance: descargar o importar canciones, editar datos (título, artista, álbum, año, carátula…) y guardarlas en una carpeta.
- Todo desde el Launcher. **El usuario no tiene `yt-dlp` ni FFmpeg instalados → el Launcher debe descargarlos y gestionarlos él mismo.**
- Reproductor: de momento el de Windows ("después ya veremos").
- Metadatos lo más automáticos y correctos posible: **no fiarse del título del vídeo ni de que canal = artista**. Orígenes: URL o texto "artista - canción" (2026-10-23).
- Previsualizar la canción y sus datos (y poder escucharla) **antes** de descargar/guardar (2026-10-24).
- La memoria llegó a decir "Música se pospone"; luego (2026-10-22) se pidió empezarla. **Estado real: desconocido** (el snapshot no tiene nada).

### 4.5 Buscar (`Modules/Search`) — ⏳

Placeholder. **Decidido [M]:** una **única barra que busca todo junto** (Launcher + archivos + web), frente a las opciones de buscar solo en el Launcher, solo archivos o solo web. Pedido buscador también como **widget** del Dashboard (2026-10-21). Relación: "mostrar carpeta" debe ir al panel de Carpetas (4.2). La memoria llegó a decir que Buscar podría quedar "opacado" por el asistente de IA ([[asistente]]).

### 4.6 Comandos (`Modules/Commands`) — ✅ / 🟡

**Hecho [C]**
- Accesos a apps, periféricos, herramientas de sistema y CLI, con categorías, favoritos, orden (nombre / favoritos / uso reciente), etiquetas CMD/PS y ADMIN, UAC mediante el verbo `runas`.
- Terminal anfitrión: ninguna (lanzamiento directo), CMD o PowerShell (`-NoExit`).
- **PATH fresco** leído del registro (Machine+User) e **inyectado dentro del comando** (`set "PATH=…" &` / `$env:Path='…';`), porque `ProcessStartInfo.EnvironmentVariables` no se puede usar con `UseShellExecute=true` (necesario para `runas`).
- Validación al guardar: comprueba ruta absoluta, PATH y System32; no verifica protocolos/URIs (`steam://`, `ms-settings:`); permite guardar igualmente tras avisar.
- Ventana de Sugerencias con 9 accesos de sistema (Administrador de dispositivos, de tareas, programador de tareas, Panel de control, Configuración, Administrador de discos, Editor del registro, PowerShell, CMD).
- Se siembra **Kunai** (CLI de streaming de anime en TypeScript/Bun) por PowerShell.

**Limitaciones**
- 🐞 En los modos CMD/PowerShell la ruta del destino **no se entrecomilla**: una ruta con espacios (`C:\Program Files\…`) fallará dentro de la consola [I]. En lanzamiento directo sí funciona.
- Los argumentos se concatenan tal cual (es el propio usuario quien los define).
- La categoría y el terminal se guardan como **números** de enum en el JSON: **no reordenar** `CommandCategory` ni `TerminalHost` sin migrar `commands.json`.
- El icono es un glifo de texto libre; "si se deja vacío usa icono de terminal".

**Pendiente [M]** ⏳
- Elegir icono desde una lista o usar el icono de la propia aplicación (pedido 2026-10-10).
- Argumento/opción de **incógnito** al añadir navegadores (2026-10-10).
- **Web con inicio de sesión automático** (2026-10-19): abrir webs que requieren login iniciando sesión solos, en Brave, modo normal e incógnito, con o sin 2FA/captcha, sin intervención. Ver 5.6 sobre riesgos.

### 4.7 Monitor (`Modules/Monitor`) — ✅ / 🟡

**Hecho [C]** (muestreo cada 1 s)
- CPU (`GetSystemTimes`), RAM (`GlobalMemoryStatusEx`), GPU (contadores "GPU Engine", solo motores 3D), VRAM (contadores "GPU Adapter Memory" + tamaño total del registro), red (bytes de todas las interfaces activas excepto *loopback*), discos fijos (capacidad + lectura/escritura por unidad con `LogicalDisk`), actividad (uptime).
- Tarjetas con porcentaje grande y *sparkline* de 60 puntos.

**Limitaciones**
- 🟡 VRAM total: se lee `HardwareInformation.qwMemorySize` del registro porque **WMI `AdapterRAM` no es fiable por encima de 4 GB**. Si el driver no escribe ese valor como `long`, se cae a WMI y se vería un tope de 4 GB [I].
- 🟡 La red suma **todas** las interfaces activas (incluidas virtuales/VPN como Tailscale o Hyper-V), por lo que puede **contar tráfico dos veces** [I].
- Los contadores de GPU devuelven 0 en la primera lectura (necesitan dos muestras).
- 🟡 `SystemMonitorService.Sample()` **sigue calculando los 5 procesos con más carga** (`SampleTopProcesses`, recorriendo todos los procesos cada segundo) aunque la tarjeta "Top Processes" **se eliminó de la UI**: trabajo desperdiciado [I].
- `Uptime` usa `Environment.TickCount64`; con "Inicio rápido" de Windows puede no reflejar el último arranque real [I].
- `MonitorView` no libera `SystemMonitorService` (`Dispose`) al descargarse; la lista de discos se vacía y recrea en cada tick.

### 4.8 Procesos (`Modules/Processes`) — ✅ / 🐞

**Hecho [C]**
- Tabla agrupada: procesos del mismo `.exe` se agregan bajo el ancestro más alto con el mismo nombre (PID del padre vía `CreateToolhelp32Snapshot`). Totales de CPU, RAM, GPU y VRAM por grupo; expandir/contraer (estado guardado **por nombre**, no por PID).
- CPU normalizada por núcleos (100 % = toda la máquina); celdas ámbar a partir del 80 %; filas rojas para procesos congelados (`Responding == false`; solo fiable en procesos con ventana).
- Búsqueda (auto-expande), orden por columnas, "Finalizar" por fila / "Finalizar todo" por grupo con confirmación; refresco cada 1,5 s.

**Limitaciones / fallos**
- 🐞 **Probable bug:** el `DispatcherTimer` se arranca solo en el constructor y se detiene en `Unloaded`; como las vistas se **cachean**, al volver a Procesos tras visitar otro módulo **la lista deja de refrescarse** (solo funciona el botón "Actualizar"). `MonitorView` lo hace bien (arranca en `Loaded`).
- 🟡 Cada refresco consulta `MainModule` y `Responding` de todos los procesos: coste alto en equipos con muchos procesos [I, sin medir].
- No se pueden finalizar procesos protegidos/elevados salvo que el Launcher se ejecute como administrador; se muestra el error.
- No se bloquea la finalización de procesos críticos del sistema más allá de impedir que el Launcher se mate a sí mismo.

**Pendiente (anotado, de baja prioridad) [M]** prioridad (`PriorityClass`), afinidad de núcleos, "abrir ubicación del archivo", detalles ampliados (hilos, handles, hora de inicio, descripción), sparkline por proceso, recordar filtro/orden. Cabecera de la tabla con % y MB/GB junto al nombre de cada columna (pedido 2026-10-10) ⏳.

### 4.9 Configuración (`Modules/Settings`) — ✅ / 🟡

**Hecho [C]** (persisten en `config.json`; excepción: Iniciar con Windows → registro)
- Arranque: iniciar con Windows, iniciar minimizado (solo en arranque automático), módulo inicial (último usado u otro).
- Ventana: siempre encima, cerrar a la bandeja, recordar tamaño/posición, **atajo global** (Ninguno, `Ctrl+Alt+L`, `Ctrl+Alt+Espacio`, `Ctrl+Shift+Espacio`).
- Juegos: tamaño de tarjetas, sincronizar al abrir, confirmar al eliminar, restaurar juegos eliminados, carpetas de sincronización, portadas (abrir carpeta, reparar).
- Actualizaciones: versión, buscar ahora / actualizar, buscar al abrir, carpeta de versiones.
- Datos: carpeta de datos, registro de errores, restablecer ajustes.
- Acerca de: versión y .NET.

**Limitaciones / fallos**
- 🔴 **Error de compilación probable** en `AppSettingsService.Reset()` (ver 6.1).
- "Restablecer" llama a `Save()` **antes** de reasignar `UpdateSource`/`CheckUpdatesOnStartup`, por lo que esos dos no se persistirían [I].
- La ayuda de la interfaz dice que `Alt+Espacio` "queda libre para el futuro asistente".

**Pendiente [M]** ⏳ (decidido 2026-10-09 añadir, **no** la copia de seguridad): intervalo de refresco de Monitor/Procesos, confirmación de finalizar proceso, color de acento, ajuste "Actualizar automáticamente"; clave de Web API de Steam (para logros); ubicación del widget Tiempo; umbrales/intervalo/efectos del dashboard.

### 4.10 Asistente de IA local (`Modules/Assistant`) — 🟡 prototipo sin integrar

**Hecho [C]**
- `OllamaService`: cliente de `http://localhost:11434/api/chat` con **streaming NDJSON**, modelo por defecto `qwen3.5:4b`, comprobación `IsAvailableAsync` (`/api/tags`).
- `AssistantWindow`: ventana flotante sin borde con historial, selector `qwen3.5:4b` / `qwen3.5:9b`, `Enter` envía, `Esc` cierra.
- `GlobalHotkeyService`: hook de teclado de bajo nivel (`WH_KEYBOARD_LL`) para `Alt + tecla`. Necesario porque `RegisterHotKey` no captura de forma fiable `Alt+Space` (reservada para el menú de sistema; `AltGr+Espacio` sí funcionaba porque envía `Ctrl+Alt+Espacio`).

**Limitaciones — hoy no es utilizable desde la app**
- No hay `AssistantModule` ni nada que abra `AssistantWindow`; `GlobalHotkeyService` **no se instancia en ninguna parte**.
- Comportamiento actual contrario al pedido: la ventana **se cierra al perder el foco**, hay **un solo chat** y **no persiste**. El usuario pidió: no cerrar al perder foco, **varios chats a la vez**, **persistencia**.
- Un fallo de Ollama se guarda en el historial como si fuera respuesta del asistente.
- Solo se lee `message.content`; si se usan modelos con "pensamiento" separado, ese texto no se muestra [I, sin verificar].
- La lista de modelos está escrita a mano.
- ❓ En la sesión del 2026-10-27 se mencionó que **Ollama no estaba instalado**.

**Plan decidido [M]** — Fase 1 únicamente: `Alt+Space` global → ventana flotante → chat con Ollama. Más adelante, por este orden: vigilar sistema y optimizar (reutilizando Monitor/Procesos) → reglas/horarios de automatización → aprendizaje de hábitos; portapapeles, acciones de Windows, voz. **El roadmap no es fijo**: se integran o descartan ideas sobre la marcha. Ver también 5.1.

### 4.11 Dashboard / Pantalla de inicio — ❓ (no está en el snapshot)

La memoria describe un módulo "Inicio" dado por bueno como primera versión el 2026-10-12, con estética de "terminal de ataque nuclear" y muchas peticiones posteriores. **No consta cuáles se completaron.** Resumen de lo pedido (todo ⏳/❓):

| Fecha | Petición |
|---|---|
| 10-13 | Pantalla de inicio con grid y widgets. Empezar por `IWidget`, `WidgetCatalog`, `WidgetGridPanel` y 3 widgets (Sistema, Juegos recientes, Ahora suena) |
| 10-15 | Layout: pestañas a la izquierda, métricas arriba, reproductor a la derecha, accesos (juegos/comandos/carpetas) al centro; procesos en barra inferior |
| 10-15/16 | Etiquetas de estado: **UPLINK** (azul, dispositivo externo; también mandos/auriculares con su nombre, si hay transferencia y si es Bluetooth o cable), **ONLINE** (verde), **WARNING** (naranja/amarillo: consumo medio-alto o algo colgado), **DEFCON** (rojo: consumo extremo, o recurso constante muy alto, o fallo grave). Registro general (también Visor de eventos de Windows) |
| 10-16 | Fase 2: rendimiento, evaluador siempre activo, colores al tema, extraer `NowPlayingPanel` común, evitar instancias duplicadas, ajustes en Configuración, secuencia de arranque tipo película, ventana redimensionable, modo una columna, menú lateral colapsable a iconos, mover la ventana arrastrando el sidebar |
| 10-17 | Procesos/Monitor ocupan el hueco bajo el reproductor; etiquetas `[x]` = acceso rápido; mover y cambiar altura de contenedores; **más de un dispositivo en UPLINK** |
| 10-18 | Agrandar/reducir partes fijas; título `[ MULTIMEDIA ]` al inicio; Dispositivos con nombre, marca y modelo |
| 10-21 | Widgets añadibles/eliminables (también de terceros y desde otros menús); **buscador como widget** |
| 10-22 | Widgets disponibles en "+ widgets" (no por defecto): TODO, Portapapeles, Tiempo (junto al reloj; ubicación en Configuración), Ofertas de juegos, Correo, Asistente; Last.fm irá en Multimedia |
| 10-24 | Las tarjetas de juegos/carpetas/comandos deben verse siempre; si caben 2,5 se reparten proporcionalmente |
| 10-28 | **Se mantiene WPF** (no migrar a PulseDeck/Electron). Botón **Editar** con tiradores para redimensionar; catálogo de **todos los widgets posibles**. Ya existe `DashboardBoard`/`BoardBox` 12×12 |

### 4.12 ImageGen / Stable Diffusion — ⏳ (no está en el snapshot)

Pedido el 2026-10-27, en construcción según la memoria. Backend **AUTOMATIC1111**. Fase 1: solo **txt2img** (prompts, parámetros, modelos, LoRAs, LyCORIS). Fase 2: Clip skip, VAE, extensiones *always-on* (oficiales y propias hechas más adelante con Claude), **biblioteca de generaciones** con filtros. Requisitos: wildcards correctos en generación única y en lote (1×4 o 4×1); **rutas editables** (LoRAs, salidas…); **autocompletado `__x`** de wildcards; extensión de tags de Danbooru estilo TagComplete.

### 4.13 Servicios transversales

| Servicio | Estado | Notas |
|---|---|---|
| Instancia única (`App.xaml.cs`) | ✅ | Segunda instancia señala un `EventWaitHandle` y se cierra; la primera se muestra aunque esté en la bandeja |
| Registro de errores (`LogService`) | ✅ | Excepciones de UI (diálogo máx. 1 cada 30 s), fatales y de tareas; limpieza a 7 días |
| Bandeja y ventana (`WindowBehaviorService`) | ✅ | Cerrar a bandeja, recordar posición (ignora monitores que ya no existen), atajo global, arranque minimizado, corrección de maximizado sin bordes (`WM_GETMINMAXINFO`) |
| Actualizaciones (`UpdateService`) | 🟡 | Velopack: estados `Unknown/NotConfigured/NotInstalled/UpToDate/Available/Error`; aplica con descarga + reinicio. Ver 2.4 |
| Arranque con Windows (`StartupService`) | ✅ | Clave `Run` del usuario (sin administrador) |
| Portadas e iconos | ✅ / 🟡 | Ver 4.1 |

---

## 5. Ideas y proyectos planificados (sin implementar o sin confirmar)

### 5.1 Asistente de IA local — arquitectura de referencia [D]

Documento "Asistente IA Local para Windows": un **sistema de agentes local** (no un chatbot) sobre **Ollama**.
- Modelos: `qwen3.5:4b` residente (router/conversación), `qwen3.5:9b` de escalado, `qwen2.5-coder:14b` + **Cline** para programación, `Qwen3.6-35B-A3B` y `gemma4:26b` como experimentales (22 GB: no residente en una 3060 de 12 GB). También disponible `qwen3:8b`.
- Componentes: Core Agent, Windows Agent (UI Automation, PowerShell, OCR…), Browser Agent (Playwright), Coding Agent, **Resource Manager**, **Model Manager** (carga/descarga dinámica por `keep_alive` y VRAM), **Router**, escalado por complejidad y por fallo.
- Reglas: ciclo PLAN→EXECUTE→VERIFY→RECOVER→RETRY→ESCALATE; **anti-bucle** (`MAX_RETRIES=3`, `MAX_SAME_ACTION=1`, `MAX_IDENTICAL_ERROR=2`); niveles de permiso 0–3 (lectura / seguras / sensibles con confirmación / administración); checkpoints Git con rollback; memoria temporal/de proyecto/de usuario.
- Hoja de ruta de 18 fases (núcleo, herramientas, seguridad, resource manager, model manager, router, agentes Windows/Web/Código, anti-bucle, memoria, planificador, automatizaciones, gestión adaptativa, voz, visión, multiagente, autogestión).
- **Decisión del usuario:** no seguir el documento paso a paso; empezar por la Fase 1 sencilla de 4.10. Lo avanzado es aspiracional.

### 5.2 Lora Studio (gestor de Civitai + AUTOMATIC1111 + Dynamic Prompts) [D][M]

README maestro v1.0 de 6 secciones: Dashboard, Civitai y descargas, Biblioteca local, Gestor de Wildcards, Generador de parejas y tríos, Configuración. Reglas clave: **no mover modelos existentes**, evitar duplicados, **nunca descargar sin autorización**, no modificar wildcards sin confirmación y con copia previa, sincronización de wildcards **manual**, historial de cuenta separado de la biblioteca local, rutas editables, API key guardada de forma segura.

Conversación del 2026-11-02 (ChatGPT, solo planificación; **no hay código**):
- Integrarlo como **módulo dentro de Launcher** (no aplicación aparte), con navegación interna de 6 secciones, servicios separados (`CivitaiService`, `DownloadService`, `ModelLibraryService`, `WildcardService`, `ModelReferenceService`, `CombinationService`, `BackupService`…).
- Decisión del usuario: **implementar todos los widgets desde el principio** (Dashboard de Launcher, Dashboard de Lora Studio y widgets flotantes) y modificarlos o eliminarlos más adelante.
- Roadmap propuesto de 13 fases (auditoría → diseño → módulo base → widgets/tareas/notificaciones → configuración → biblioteca → wildcards → referencias → Civitai → descargas → combinaciones → widgets finales → pruebas → documentación).

⚠️ **Solapamientos y riesgos detectados [I] — decidir antes de empezar:**
1. **El plan de ChatGPT propone construir un sistema de widgets nuevo, pero el Launcher ya tiene uno** (`IWidget`, `WidgetCatalog`, `DashboardBoard`) según la memoria. Hay que **reutilizarlo**, no duplicarlo; y para eso hace falta el código actual (el snapshot no lo contiene).
2. **Solapa con ImageGen (4.12):** ambos necesitan rutas de A1111 (LoRA, checkpoints, LyCORIS), conocimiento de wildcards y la lista de LoRAs. Convendría un servicio común de rutas/biblioteca de modelos y de wildcards.
3. La API de Civitai (límites, campos, descargas con clave) **no se ha verificado**.
4. Alcance muy grande: conviene entregar por fases con criterio de aceptación.
5. Ese plan propone "perfiles de Dashboard", centro de notificaciones y sistema de tareas globales: útiles, pero **son proyectos propios**, no requisitos de Lora Studio.

### 5.3 Tailscale + traspaso de ficheros (módulo de red) [M]

- Módulo vinculado a la cuenta de Tailscale (nombre por decidir, para no solaparse con UPLINK): ver dispositivos y cuáles están activos; gestionar reglas/propiedades (fase 2).
- Fase 1 aprobada (2026-10-26): lista de dispositivos, estado online y **envío de ficheros**.
- Ficheros: **LocalSend con prioridad**, Tailscale (**Taildrop**) como alternativa. Casos: PC↔móvil y PC↔otro PC. El usuario usa Tailscale cuando los equipos no están en la misma red; tiene LocalSend instalado.
- **La recepción la hace siempre el Launcher** (LocalSend y Taildrop), guardando en una carpeta `transferencia` en la raíz del proyecto, **ignorada por git** (el `.gitignore` del snapshot **no la ignora todavía**).
- Al elegir "enviar a un dispositivo", abrir una sección con los archivos "preparados" y **miniaturas** de imágenes/vídeos.
- Pregunta abierta: cómo corregir el guardado "automático" de Taildrop para que lo reciba el Launcher.

### 5.4 Modo escritorio [M]

Launcher a pantalla completa como si fuera el escritorio (sin barra de tareas ni escritorio de Windows visibles), que aparezca al minimizar todo (`Win+D`). Con varios monitores: en todos, cada uno con configuración distinta. Valorar que haga de escritorio completo (menú, barra de tareas, iconos). **Sin método decidido** (se mencionó WorkerW). ⚠️ Sustituir o simular el shell de Windows es delicado: hay que preservar siempre una vía de salida.

### 5.5 Fondo animado, estilo común y Dispositivos [M]

Fondo animado/cambiable, mismo estilo en todos los menús, y módulo **Dispositivos** (ver y gestionar dispositivos conectados, app del fabricante y ajustes del sistema; relación con UPLINK). Decidido (2026-10-17) implementarlo todo, "en el orden más conveniente". Pregunta del usuario: si el Launcher puede trabajar en segundo plano con fondo animado.

### 5.6 Inicio de sesión automático en webs (Comandos) [M][I]

Brave, modo normal e incógnito; web sin 2FA ni captcha, pero se quiere resolver también con ellos. Consideraciones: guardar credenciales exige almacenamiento seguro (p. ej. DPAPI/Credential Manager) y no texto plano; captcha y 2FA están diseñados precisamente para impedir la automatización; la opción habitual es pasar un perfil de navegador ya autenticado en lugar de reescribir contraseñas.

### 5.7 Portabilidad a Linux [M]

Idea (sin decisión) de pasar a Nobara (KDE Plasma). Vías: adaptar el Launcher, reescribirlo (se valoró Python para unir Launcher y asistente) o aceptar una versión sin paridad total. **Muy dependiente de Windows:** WPF, `user32`/`kernel32` P/Invoke, registro, `PerformanceCounter`, WinRT SMTC, CoreAudio/NAudio, Velopack, bandeja WinForms. Lo portable en principio: lógica de datos (JSON), cliente de Ollama, Steam/Epic (rutas y formatos distintos), parte del monitor mediante `/proc`.

---

## 6. Limitaciones técnicas, bugs y deuda detectados

> Resultado de leer el snapshot. Ninguno está confirmado en ejecución salvo que se indique.

### 6.1 🔴 Bloqueantes de compilación (probables)

| # | Dónde | Problema | Arreglo sugerido |
|---|---|---|---|
| B1 | `Core/Services/AppSettingsService.cs` → `Reset()` | Dentro del método están pegadas declaraciones de **miembros de clase** (`public static readonly string DefaultUpdateSource`, `public string UpdateSource {get;set;}`, `public bool CheckUpdatesOnStartup {get;set;}`). No es C# válido, y esas propiedades las usan `UpdateService`, `MainWindow` y `SettingsView` | Mover las tres declaraciones a nivel de clase (junto a las demás propiedades) y dejar en `Reset()` solo las asignaciones, **antes** de `Save()` |
| B2 | `Launcher.csproj` | Falta `<PackageReference Include="Velopack" …/>` (usado en `Program.cs` y `UpdateService.cs`) | Añadir la referencia con la versión que coincida con `vpk` instalada (la memoria menciona 1.2.161) |
| B3 | `README.md` del repo | Marcadores de conflicto de git sin resolver | Sustituir el archivo por este documento |

### 6.2 🐞 Bugs probables de comportamiento

| # | Dónde | Problema |
|---|---|---|
| F1 | `ProcessesView.xaml.cs` | El temporizador solo arranca en el constructor; `Unloaded` lo detiene y vistas cacheadas no lo reanudan (ver 4.8) |
| F2 | `MediaView.xaml.cs` | `_isDraggingMaster` nunca se desactiva (ver 4.3) |
| F3 | `App.xaml` + `App.xaml.cs` | `StartupUri="MainWindow.xaml"` coexiste con `new MainWindow().Show()` en `OnStartup`. **Posible doble ventana principal** [I]. Comprobar al arrancar; si ocurre, quitar `StartupUri` |
| F4 | `CommandsService.Launch` | Rutas con espacios sin entrecomillar en CMD/PowerShell |
| F5 | `AppSettingsService.Reset` | `Save()` antes de restablecer algunos campos (ver 4.9) |
| F6 | Orden de módulos | `Procesos` y `Buscar` ambos con `Order = 4` |

### 6.3 🟡 Rendimiento y diseño

- `SystemMonitorService.SampleTopProcesses` calcula datos que nadie usa (4.7).
- `ProcessMonitorService` consulta `MainModule` y `Responding` de todos los procesos en cada tick.
- Portadas descargadas en serie durante la sincronización.
- Dos archivos de configuración con nombres confusos (`config.json` vs `settings.json`).
- Enums serializados como números (frágil ante reordenaciones).
- Código sin usar: `RelayCommand`, `GlobalHotkeyService`, `ProcessLoadInfo`/`SystemSnapshot.TopProcesses`, `MediaApps*`, `AssistantWindow`.
- Estilos duplicados en cada ventana modal (se podría mover al tema).
- Estilos `ChipStyle` definidos en el tema y de nuevo dentro de `GamesView`/`CommandsView` (los locales ganan).
- **No hay proyecto de pruebas automatizadas** (ver sección 8).
- **No hay firma de código** (Smart App Control / SmartScreen pueden bloquear).

### 6.4 Trampas de la plataforma (ya resueltas; no repetir)

- WMI no es fiable para VRAM > 4 GB → registro `HardwareInformation.qwMemorySize`.
- `System.Diagnostics.Process` no expone el PID del padre → `CreateToolhelp32Snapshot`.
- `ProcessStartInfo.EnvironmentVariables` es incompatible con `UseShellExecute=true` → inyectar en la cadena del comando.
- El PATH del proceso se queda obsoleto → leer Machine+User del registro al lanzar.
- Ambigüedades WinForms/WPF por `ImplicitUsings` → quitar el *using* global de `System.Windows.Forms`; usar alias `WinForms`/`Forms`/`Drawing`.
- `System.Drawing.Icon` vs `Window.Icon` → cualificar `System.Drawing.Icon.ExtractAssociatedIcon`; igual con `System.Windows.Point`.
- WinRT `Windows.Media.Control` exige TFM `…-windows10.0.19041.0` y `SupportedOSPlatformVersion 10.0.17763.0`.
- `AsStreamForRead()` no existe en este contexto → `DataReader` + `MemoryStream`.
- Agrupar procesos por **nombre**, no por PID (los PID cambian entre refrescos).
- `RegisterHotKey` no captura `Alt+Space` → hook `WH_KEYBOARD_LL` que consume la tecla.
- Con `AllowsTransparency="True"` el *resize grip* nativo no se ve (el redimensionado por bordes sí funciona); alternativa: `WindowChrome` + `DwmSetWindowAttribute` (Windows 11).
- Una ventana sin bordes transparente maximiza sobre toda la pantalla tapando la barra de tareas → se corrige en `WM_GETMINMAXINFO`.
- Cancelar un cierre en `Closing` bloquearía el apagado de Windows → `SessionEnding` marca `_exiting`. Y `Close()` (no `Shutdown()`) en el botón X para respetar "cerrar a bandeja".

---

## 7. Lo que NO está hecho (aún) y lo que NO se puede

### 7.1 Aún no hecho (⏳)

Resumen por prioridad (el detalle está en la sección 4 y 5):
1. Actualización automática al detectar versión nueva.
2. Asistente de IA: registrarlo como módulo, abrirlo con `Alt+Space`, chats múltiples y persistentes sin cierre por pérdida de foco.
3. Buscador unificado.
4. Carpetas como explorador integrado + redirección desde Buscar/asistente.
5. Comandos: selector de icono, incógnito, login automático.
6. Ficha de juego (logros de Steam, enlaces, documentos, notas).
7. Ajustes adicionales de Configuración.
8. Música (descarga/importación/edición/previsualización).
9. Dashboard (si el snapshot es realmente el estado actual; ver 4.11).
10. ImageGen, Tailscale/Traspaso de ficheros, Fondo animado/Dispositivos, Lora Studio, Modo escritorio.
11. Procesos: columnas con %/MB, prioridad, afinidad, etc.
12. Icono propio de la aplicación en todos los puntos (el usuario pidió que no quede el icono por defecto; el `.csproj` ya referencia `Assets\launcher.ico` y la bandeja lo carga) — comprobar visualmente. ❓
13. Carpeta `transferencia` en `.gitignore`.

### 7.2 No se puede o está bloqueado por la plataforma (🚫)

| Cosa | Motivo |
|---|---|
| Medir tiempo jugado de juegos lanzados por protocolo | No hay proceso hijo al que esperar (`steam://`, Epic) |
| Que `RegisterHotKey` capture `Alt+Space` | Está reservada por Windows (menú de sistema); hace falta hook de bajo nivel |
| Leer la VRAM total con WMI para GPU > 4 GB | `AdapterRAM` es de 32 bits |
| `Process.EnvironmentVariables` con elevación (`runas`) | Requiere `UseShellExecute=true` |
| Detectar "congelado" en procesos sin ventana | `Responding` solo es fiable con UI |
| Finalizar procesos elevados sin ser administrador | Permisos de Windows |
| Instalar/actualizar con Velopack desde la copia sin instalar | `IsInstalled` es falso fuera de `Setup.exe` |
| Ejecutar el Launcher en Linux/macOS tal cual | WPF y APIs Win32/WinRT |
| Automatizar captcha/2FA "sin intervención" de forma fiable y segura | Están diseñados para impedirlo (ver 5.6) [I] |
| Copiar todas las funciones del Explorador de Windows | Alcance desproporcionado; hay que acotar [I] |

### 7.3 Sin verificar (❓)

- Detección real de Steam/Epic con distintas instalaciones y versiones.
- Estado actual de las peticiones de Dashboard, Música, Buscar, ImageGen del apartado 4.
- Si el problema de "no detecta versiones nuevas" (2026-10-10) quedó resuelto tras instalar con `Setup.exe` y probar una 0.1.1.
- API de Civitai (campos, límites, descargas) y de Tailscale/Taildrop/LocalSend.
- Si los glifos del sidebar/botones se ven bien con la fuente instalada.

---

## 8. Pruebas y comprobaciones

### 8.1 Estado real de las pruebas

- **No existe un proyecto de pruebas** (ni xUnit/NUnit/MSTest) en el snapshot ni en el `.csproj`.
- **No se ha ejecutado ninguna prueba** en la elaboración de este documento.
- Lo conocido por la memoria [M] es manual: el usuario ejecutó la app; se diagnosticó el problema de actualizaciones el 2026-10-10 (se ejecutaba `publish\Launcher.exe` en vez de la copia instalada; el feed, `UpdateSource` y vpk/Velopack 1.2.161 estaban bien); se resolvieron ambigüedades de compilación en varias ocasiones (sección 6.4). No hay registro de resultados de una batería sistemática.

### 8.2 Comprobación de arranque (primera de todas)

| ID | Paso | Resultado esperado |
|---|---|---|
| S-01 | `dotnet build` | Compila sin errores. **Si falla: ver B1 y B2** |
| S-02 | Ejecutar la app | Aparece **una sola** ventana (comprobar F3) |
| S-03 | Abrir una segunda instancia | No se abre otra ventana; la primera se muestra/enfoca |
| S-04 | Revisar `%AppData%\Launcher\logs` | Sin errores al arrancar |
| S-05 | Navegar por todos los módulos y volver | Sin excepciones; los datos persisten |

### 8.3 Batería manual por módulo

**Juegos**
| ID | Paso | Esperado |
|---|---|---|
| T-GAM-01 | Añadir un juego manual con exe, argumentos y portada | Aparece la tarjeta; sobrevive a reiniciar |
| T-GAM-02 | "Detectar desde carpeta" con una carpeta con setup/redist | Elige el exe correcto y extrae icono |
| T-GAM-03 | Arrastrar un `.exe` | Se añade sin duplicar si se repite |
| T-GAM-04 | Configurar Steam (auto-detectar) y Sincronizar | Aparecen los juegos de **todas** las bibliotecas; revisar si salen herramientas que no son juegos |
| T-GAM-05 | Igual con Epic | Aparecen y lanzan por protocolo |
| T-GAM-06 | Eliminar un juego de Steam y volver a sincronizar | **No** reaparece; "Restaurar" lo hace reaparecer |
| T-GAM-07 | Lanzar un exe directo ≥ 1 min y cerrarlo | Suma tiempo jugado |
| T-GAM-08 | Ocultar y ver en filtro "Ocultos" | Solo aparece ahí |
| T-GAM-09 | `Ctrl+F`, escribir, `Enter`, `Esc` | Foco, lanza el primero, limpia |
| T-GAM-10 | Cambiar tamaño de tarjetas en Configuración | Se aplica al instante |
| T-GAM-11 | Corromper `games.json` a mano | La app arranca vacía y registra el error |

**Carpetas**: T-FOL-01 se siembran 6 por defecto · T-FOL-02 añadir duplicada → aviso · T-FOL-03 renombrar/mover una carpeta real → etiqueta "no encontrada" · T-FOL-04 las del sistema no se pueden eliminar.

**Multimedia**
| ID | Paso | Esperado |
|---|---|---|
| T-MED-01 | Reproducir Spotify y una pestaña de navegador a la vez | Muestra la que está en reproducción |
| T-MED-02 | Play/pausa/siguiente/anterior y *seek* | Responde; el *seek* solo si la app lo soporta |
| T-MED-03 | Mover el volumen maestro **y luego** cambiarlo con las teclas del teclado | **Debe** reflejarse (falla si persiste F2) |
| T-MED-04 | Cambiar el dispositivo de salida predeterminado | El mezclador cambia de dispositivo |
| T-MED-05 | Cerrar una app con sonido | Su fila desaparece |

**Comandos**
| ID | Paso | Esperado |
|---|---|---|
| T-CMD-01 | Ejecutar Kunai (requiere instalarlo) | Abre PowerShell con PATH fresco |
| T-CMD-02 | Comando con terminal CMD/PS y **ruta con espacios** | Debe funcionar (falla si persiste F4) |
| T-CMD-03 | "Ejecutar como administrador" y **cancelar** UAC | No se muestra error |
| T-CMD-04 | Añadir sugerencias | Excluye las ya existentes |
| T-CMD-05 | Guardar un destino inexistente | Avisa y permite guardar |

**Monitor**: T-MON-01 CPU/RAM/GPU/VRAM coherentes con el Administrador de tareas (VRAM total = 12 GB en la RTX 3060) · T-MON-02 red con una descarga grande (¿doble conteo con VPN/Tailscale?) · T-MON-03 discos con lectura/escritura al copiar un archivo grande · T-MON-04 uso de CPU del propio Launcher en reposo.

**Procesos**
| ID | Paso | Esperado |
|---|---|---|
| T-PRO-01 | Abrir Procesos, ir a otro módulo y volver | **Debe seguir refrescando** (falla si persiste F1) |
| T-PRO-02 | Expandir Discord/Chrome | Hijos agrupados bajo la raíz; el estado se conserva entre refrescos |
| T-PRO-03 | Finalizar un proceso de prueba | Confirma y desaparece |
| T-PRO-04 | Finalizar un proceso elevado sin admin | Mensaje de error |
| T-PRO-05 | Congelar una app | Fila roja "congelado" |

**Configuración y ventana**
| ID | Paso | Esperado |
|---|---|---|
| T-CFG-01 | Activar/desactivar "Iniciar con Windows" | Se refleja en el registro; "minimizado" reescribe el comando |
| T-CFG-02 | Cerrar a bandeja → X → icono → Abrir/Salir | Funciona; no quedan iconos fantasma |
| T-CFG-03 | Cada atajo global | Muestra/oculta; si otra app lo usa, avisa y revierte |
| T-CFG-04 | Recordar tamaño/posición con un monitor desconectado | Ignora la posición guardada |
| T-CFG-05 | Maximizar la ventana | No tapa la barra de tareas |
| T-CFG-06 | **Restablecer** y luego reabrir | Valores por defecto, incluidos `UpdateSource` y `CheckUpdatesOnStartup` (falla si persiste F5) |
| T-CFG-07 | Apagar/cerrar sesión con "cerrar a bandeja" activo | No bloquea el apagado |

**Actualizaciones**
| ID | Paso | Esperado |
|---|---|---|
| T-UPD-01 | Ejecutar desde VS | Estado "no instalada" |
| T-UPD-02 | Instalar con `Setup.exe` (v0.1.0), publicar 0.1.1, "Buscar ahora" | Detecta 0.1.1; el texto del sidebar se vuelve verde |
| T-UPD-03 | Aplicar | Descarga, quita el icono de bandeja, reinicia ya en 0.1.1 |
| T-UPD-04 | Carpeta de versiones inexistente | Estado "Error" con mensaje, sin colgar |

**Asistente** (cuando esté integrado): T-AST-01 Ollama apagado → mensaje claro · T-AST-02 *streaming* progresivo · T-AST-03 cambiar entre 4b y 9b · T-AST-04 `Alt+Space` global sin abrir el menú de sistema · T-AST-05 la ventana **no** se cierra al perder foco · T-AST-06 varios chats y persistencia.

### 8.4 Pruebas automatizables sugeridas (no implementadas)

Un proyecto `Launcher.Tests` (xUnit) sin dependencia de WPF cubriría con facilidad:
- `IconExtractionService.FindLikelyExecutable` (carpeta temporal con setup/redist/exe real).
- `CommandsService.TargetExists` / `LooksLikeProtocolOrUri` (rutas, PATH, protocolos, `C:\…`).
- Expresiones regulares de `SteamGameProvider` con `.acf`/`.vdf` de ejemplo y de `EpicGameProvider` con `.item` de ejemplo.
- `ProcessMonitorService.BuildGroups` (con muestras sintéticas).
- `DepthToIndentConverter`, `BoolToVisibilityConverter`, `EmptyToVisibilityConverter`.
- `FolderShortcutsService`/`GameLibraryService` con carpetas temporales (requeriría inyectar la ruta de datos; hoy es estática).
- (Futuro) generación de combinaciones de Lora Studio: parejas `N(N-1)/2` y tríos `N(N-1)(N-2)/6`, sin duplicados invertidos.

---

## 9. Registro de decisiones (cronológico) [M]

| Fecha | Decisión |
|---|---|
| 2026-09-20 | Asistente de IA local con Ollama y `Alt+Space`; empezar por lo básico. Modelos `qwen3.5:4b`/`9b` |
| 2026-10-09 | Empezar por Configuración (IA para después). Orden: caché de portadas, instancia única, que "eliminar" no reaparezca, arreglo de maximizado sin bordes, registro de errores, tiempo de juego, extras de Juegos, versión única del csproj. Añadir a Configuración todo lo propuesto **excepto copia de seguridad** |
| 2026-10-10 | Migración a **.NET 10**. Usar el ejecutable **publicado** con control de versiones. Que la app avise de versiones nuevas. Música "desde cero" |
| 2026-10-11 | Linux: solo idea |
| 2026-10-12 | Actualizar automáticamente al detectar versión. README al día. Dashboard v1 aprobado. Ficha de juego. Lista TODO como artifact |
| 2026-10-13 | Empezar la pantalla de inicio con `IWidget`/`WidgetCatalog`/`WidgetGridPanel` y 3 widgets |
| 2026-10-17 | Implementar fondo animado, estilo común, Dispositivos y los 4 ajustes del dashboard |
| 2026-10-21 | Buscador = una sola barra para todo |
| 2026-10-22 | Widgets TODO/Portapapeles/Tiempo/Ofertas/Correo/Asistente; Música: el Launcher gestiona yt-dlp/FFmpeg |
| 2026-10-23 | Carpetas: explorador integrado; nada de abrir el Explorador de Windows |
| 2026-10-26 | LocalSend primero, Taildrop después; el Launcher recibe en `transferencia/` (ignorada por git); Tailscale fase 1 = dispositivos + envío |
| 2026-10-27 | ImageGen sobre A1111, fase 1 txt2img |
| 2026-10-28 | **Se mantiene WPF** (no PulseDeck/Electron); botón Editar y catálogo completo de widgets |
| 2026-11-02 | Lora Studio como módulo de Launcher; todos los widgets desde el principio |
| 2026-11-06 | Este README de traspaso |

---

## 10. Guía para quien continúe (persona o IA)

1. **Pedir el código actual** (`git pull` de `main` o ZIP sin `bin/`, `obj/`, `publish/`). No fiarse del snapshot ni de la memoria por separado (ver 0.4).
2. **Compilar primero** y arreglar B1/B2 si siguen presentes; resolver el conflicto del README.
3. Entregar **archivos completos**, indicando **ruta** y **nuevo/reemplazo**. No reconstruir archivos de memoria.
4. Mantener las convenciones: interfaz y comentarios **sin tildes**, `DynamicResource` para todo color/fuente, `IModule` + registro en `MainWindow`, persistencia JSON bajo `%AppData%\Launcher`.
5. Respetar las reglas de seguridad ya adoptadas: **ninguna acción destructiva sin confirmación**; "Eliminar" nunca borra archivos reales del usuario; operaciones de terceros (descargas, modificaciones de ficheros externos) **solo con autorización explícita**.
6. Todo lo que dependa de APIs del sistema debe consultar 6.4 antes de implementarse.
7. Actualizar este README y la lista TODO al cerrar cada tarea.
8. El usuario prefiere decidir: ante un cambio de alcance, proponer opciones y esperar respuesta; el roadmap del asistente **no es fijo**.

### 10.1 Orden de trabajo recomendado

| Prioridad | Tarea |
|---|---|
| **P0** | Sincronizar el snapshot real · corregir B1/B2/B3 · compilar y pasar S-01…S-05 |
| **P1** | Corregir F1 (Procesos), F2 (volumen), F3 (doble ventana si ocurre), F4 (comillas), F5/F6 |
| **P2** | Actualización automática · integrar el asistente (Fase 1) · Buscador · Carpetas explorador · iconos/incógnito en Comandos · ajustes pendientes de Configuración · ficha de juego |
| **P3** | Dashboard (rejilla editable y catálogo) · Música · ImageGen |
| **P4** | Tailscale/Traspaso · Fondo/Dispositivos · Lora Studio (tras decidir 5.2) · Modo escritorio · Linux |
| Transversal | Crear `Launcher.Tests`; firmar o documentar la instalación sin firma |

---

## 11. Glosario

| Término | Significado |
|---|---|
| UPLINK / ONLINE / WARNING / DEFCON | Etiquetas de estado del Dashboard (azul, verde, ámbar, rojo) |
| SMTC | System Media Transport Controls (fuente de "Ahora suena") |
| Velopack / `vpk` | Instalador y sistema de actualizaciones; `Setup.exe` y feed en `LauncherReleases` |
| Taildrop / LocalSend | Mecanismos de traspaso de ficheros (Tailscale / app de red local) |
| Wildcards / Dynamic Prompts | Archivos de listas para AUTOMATIC1111 (`__nombre__`) |
| Kunai | CLI de streaming de anime que se lanza por PowerShell |
| `IsBuiltIn` | Carpeta sembrada por el sistema, no eliminable |

---

## 12. Apéndice — mapa de archivos del snapshot

| Archivo | Función |
|---|---|
| `Program.cs` | `VelopackApp.Build().Run()` y arranque de WPF |
| `App.xaml(.cs)` | Tema, instancia única, escucha de "mostrar", manejadores de excepciones |
| `MainWindow.xaml(.cs)` | Shell, sidebar, registro de módulos, indicador de actualización, controles de ventana |
| `Core/Services/AppSettingsService.cs` | Ajustes (`config.json`) — **contiene B1** |
| `Core/Services/LibraryPathsService.cs` | Rutas Steam/Epic y su auto-detección |
| `Core/Services/WindowBehaviorService.cs` | Bandeja, posición, hotkey, minimizado, maximizado sin bordes |
| `Core/Services/GlobalHotkeyService.cs` | Hook `WH_KEYBOARD_LL` (sin uso aún) |
| `Core/Services/UpdateService.cs` | Velopack |
| `Core/Services/StartupService.cs` | Clave `Run` |
| `Core/Services/LogService.cs` | Registro de errores |
| `Core/Services/ModuleCatalog.cs` | Lista de módulos accesible globalmente |
| `Core/Services/CoverArtService.cs` / `IconExtractionService.cs` | Portadas e iconos |
| `Core/Services/FolderShortcutsService.cs` / `MediaAppsService.cs` | Persistencia de accesos |
| `Core/Services/NowPlayingService.cs` / `AudioMixerService.cs` | SMTC y CoreAudio |
| `Core/Services/SystemMonitorService.cs` | Métricas del sistema |
| `Core/Services/OllamaService.cs` | Cliente Ollama con streaming |
| `Modules/Games/*` | Vista, diálogos, `GameLibraryService`, `Providers/Steam`, `Providers/Epic` |
| `Modules/Commands/*` | Vista, servicio, sugerencias, diálogo de alta/edición |
| `Modules/Processes/*` | Vista, servicio de muestreo, agrupación y filas |
| `Modules/Assistant/AssistantWindow.*` | Ventana de chat (sin registrar) |
| `UI/Themes/Theme.Dark.xaml` | Tema, estilos y fuentes |
| `publish.ps1` | Publicación + empaquetado `vpk` |
| `.gitignore` | Plantilla de Visual Studio (ignora `bin/`, `obj/`, `publish/`, `*.log`, `.env`, …) |

---

*Fin del documento. Si algo de lo aquí descrito contradice el código actual, manda el código: corrige este README en el mismo cambio.*