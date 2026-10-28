# Launcher

Plataforma de escritorio **modular** para Windows, escrita en **C# / WPF (.NET 10)**, con estética de terminal oscura (verde fósforo). Reúne en una sola ventana: biblioteca de juegos, accesos a carpetas, multimedia con mezclador de volumen, comandos y herramientas, monitor del sistema, gestor de procesos, configuración y, en camino, un asistente de IA local (Ollama).

> Última revisión: 2026-10-26.
> Leyenda: ✅ hecho · 🟡 parcial / sin verificar · ⬜ pendiente · 💡 idea sin decidir · ⚠️ error o riesgo conocido

---

## Índice

1. [Stack y requisitos](#1-stack-y-requisitos)
2. [Ejecutar, publicar y actualizar](#2-ejecutar-publicar-y-actualizar)
3. [Arquitectura y estructura](#3-arquitectura-y-estructura)
4. [Tema y estilos](#4-tema-y-estilos)
5. [Estado por módulo](#5-estado-por-módulo)
6. [Datos y persistencia](#6-datos-y-persistencia)
7. [Pendiente (hoja de ruta)](#7-pendiente-hoja-de-ruta)
8. [Ideas y decisiones abiertas](#8-ideas-y-decisiones-abiertas)
9. [Límites conocidos](#9-límites-conocidos)
10. [Errores y deuda técnica](#10-errores-y-deuda-técnica)
11. [Trampas de plataforma (gotchas)](#11-trampas-de-plataforma-gotchas)
12. [Convenciones de trabajo](#12-convenciones-de-trabajo)

---

## 1. Stack y requisitos

| Elemento | Detalle |
|---|---|
| Lenguaje / runtime | C#, **.NET 10** (migrado desde .NET 8 el 2026-10-10; el soporte de .NET 8 y 9 termina el 2026-11-10) |
| UI | WPF + una pizca de WinForms (bandeja, diálogos de carpeta) |
| Target framework | `net10.0-windows10.0.19041.0`, `SupportedOSPlatformVersion 10.0.17763.0` (necesario para WinRT `Windows.Media.Control`) |
| NuGet | `NAudio 2.2.1`, `System.Diagnostics.PerformanceCounter 8.0.0`, `System.Management 8.0.0`, `Velopack` (ver [error E2](#e2-falta-la-referencia-a-velopack-en-el-csproj)) |
| Empaquetado / actualizaciones | Velopack (`vpk`) |
| Sistema operativo | Windows 10 1809+ / Windows 11 |
| IA local (opcional) | [Ollama](https://ollama.com) en `http://localhost:11434` |

Requisitos para compilar: **.NET 10 SDK**.

---

## 2. Ejecutar, publicar y actualizar

### Desarrollo
```powershell
dotnet run
```
o `F5` en Visual Studio / VS Code (C# Dev Kit). En modo `DEBUG` la instancia única usa un mutex distinto, así que puedes tener a la vez la versión instalada y la de desarrollo.

### Publicar una versión
1. Sube `<Version>` en `Launcher.csproj` (cada release necesita una versión distinta).
2. Ejecuta desde la carpeta del proyecto:
   ```powershell
   .\publish.ps1
   ```
   Hace `dotnet publish -c Release -r win-x64 --self-contained` y empaqueta con `vpk pack` en `%USERPROFILE%\LauncherReleases` (parámetro `-Releases` para cambiarlo).
3. **Primera vez:** instala con el `Setup.exe` de esa carpeta. Después, las copias instaladas detectan las versiones nuevas.

### Actualizaciones (UpdateService)
- Solo funcionan en una copia **instalada con Setup.exe**. Ejecutando `publish\Launcher.exe` o desde el IDE el estado es `NotInstalled`.
- Origen por defecto: `%USERPROFILE%\LauncherReleases` (cambiable en Configuración).
- Comprueba al abrir (tras 8 s) y cada 6 h; si hay versión nueva, el texto de versión del sidebar se vuelve verde y clicable.
- ⬜ Pendiente: aplicar automáticamente al detectar versión nueva (ajuste "Actualizar automáticamente").

### Smart App Control
El `.exe` no está firmado. Con Smart App Control activo Windows lo bloquea (error 4551). En el PC del autor está desactivado.

---

## 3. Arquitectura y estructura

- **Módulos** que implementan `IModule` (`Id`, `DisplayName`, `Icon`, `Order`, `CreateView()`).
- `MainWindow` genera el sidebar dinámicamente desde una lista de módulos y **cachea las vistas** ya creadas.
- `PlaceholderModule` es el relleno de los módulos aún sin construir.
- `ModuleCatalog` expone la lista de módulos al resto de la app (p. ej. "Módulo inicial" en Configuración).
- MVVM ligero: `ViewModelBase` + `RelayCommand`, sin frameworks externos.
- Servicios singleton o por vista según el caso (`GameLibraryService.Instance`, `AppSettingsService.Instance`, `UpdateService.Instance`…).
- Orden de construcción de un módulo: **modelo → servicio → módulo → vista**. Se registra en `MainWindow.xaml.cs`.

```
Launcher/
├── App.xaml(.cs)                 # instancia única, manejo global de errores
├── Program.cs                    # Main + VelopackApp.Build().Run()
├── MainWindow.xaml(.cs)          # ventana sin bordes, sidebar, registro de módulos
├── Launcher.csproj · app.manifest · publish.ps1
├── Assets/launcher.ico
├── Core/
│   ├── Interfaces/               # IModule, IGameProvider
│   ├── Models/                   # Game, FolderShortcut, CommandShortcut, MediaAppShortcut,
│   │                             # NowPlayingInfo, AudioSessionInfo, SystemSnapshot, DriveStat,
│   │                             # ProcessLoadInfo, AssistantMessage, enums (GameSource,
│   │                             # CommandCategory, TerminalHost), PlaceholderModule
│   ├── Services/                 # AppSettings, LibraryPaths, GameLibrary*, CoverArt, IconExtraction,
│   │                             # FolderShortcuts, MediaApps, NowPlaying, AudioMixer, SystemMonitor,
│   │                             # WindowBehavior, GlobalHotkey, Startup, Update, Log, Ollama, ModuleCatalog
│   ├── Converters/               # BoolToVisibility, EmptyToVisibility, PathToBitmap,
│   │                             # PlayPauseGlyph, DepthToIndent
│   └── Mvvm/                     # ViewModelBase, RelayCommand
├── UI/
│   ├── Themes/Theme.Dark.xaml    # colores, tipografía y estilos
│   └── Controls/NavButton.cs
└── Modules/
    ├── Games/      (+ Providers/Steam, Epic)
    ├── Folders/    Media/    Commands/    Monitor/    Processes/    Settings/
    ├── Assistant/  (ventana de chat, aún sin enlazar)
    └── Music/ · Search/        # vacíos (placeholders)
```

\* `GameLibraryService` vive en `Modules/Games/`.

### Comportamiento de la ventana (`WindowBehaviorService`)
- Cerrar a la bandeja, icono de bandeja (con icono generado en código como respaldo).
- Recordar tamaño y posición (se ignora si el monitor guardado ya no existe).
- Atajo global (`RegisterHotKey`): `Ctrl+Alt+L`, `Ctrl+Alt+Espacio`, `Ctrl+Shift+Espacio`.
- Arranque minimizado (`--minimized`), siempre encima, arreglo de maximizar sin bordes (respeta la barra de tareas vía `WM_GETMINMAXINFO`).
- **Instancia única:** una segunda ejecución avisa a la primera para que se muestre (aunque esté en la bandeja).
- **Errores:** excepciones no controladas → `LogService` + aviso (máx. 1 cada 30 s).

---

## 4. Tema y estilos

- Fondo casi negro con tinte verde; acento `#3ECF7E`; peligro `#E5484D`; aviso `#E0B84C`.
- `Segoe UI` para texto legible; `Cascadia Mono` / Consolas para el toque retro (títulos, nav, badges, datos).
- Iconos con **Segoe MDL2 Assets** (glifos `\uE…`).
- Todo con `DynamicResource` → un tema claro sería crear `Theme.Light.xaml` con las mismas claves y cambiar el `MergedDictionary`.
- Estilos reutilizables: `CardStyle`, `BadgeStyle`, `ChipStyle`, `PrimaryButtonStyle`, `SecondaryButtonStyle`, `TextBoxStyle` (con placeholder vía `Tag`), `MonoTextStyle`, `MonoHeadingStyle`, `Effect.AccentGlow`, scrollbar fina.
- Los textos de la interfaz están escritos **sin tildes ni eñes** ("Anadir", "Configuracion"); es la convención actual de las cadenas de UI.

---

## 5. Estado por módulo

| Módulo | Id | Order | Estado |
|---|---|---|---|
| Juegos | `games` | 0 | ✅ funcional |
| Carpetas | `folders` | 1 | ✅ básico · ⬜ explorador integrado |
| Multimedia | `media` | 2 | ✅ funcional |
| Música | `music` | 3 | ⬜ placeholder |
| Buscar | `search` | 4 | ⬜ placeholder (se va a construir) |
| Procesos | `processes` | 4 ⚠️ | ✅ funcional |
| Monitor | `monitor` | 6 | ✅ funcional |
| Comandos | `commands` | 10 | ✅ funcional |
| Configuración | `settings` | 100 | ✅ funcional · ⬜ ajustes pendientes |
| Asistente IA | — | — | 🟡 ventana de chat creada, sin enlazar |
| Inicio / Dashboard | — | — | 🟡 primera versión dada por buena (ver §5.10) |

### 5.1 Juegos ✅
**Hecho**
- Cuadrícula de tarjetas con portada (o inicial si no hay), nombre y subtítulo (origen · tiempo jugado).
- Búsqueda en tiempo real, `Ctrl+F` para enfocar, `Esc` limpia, `Enter` lanza el primer resultado.
- Filtros: Todos / Favoritos / Steam / Epic / Manuales / Ocultos. Orden: Nombre, Último jugado, Más jugado, Favoritos primero.
- Añadir/editar con diálogo propio: ejecutable, argumentos, portada (manual, automática o icono del .exe), "Detectar desde una carpeta".
- **Arrastrar y soltar `.exe`** para añadir juegos (con portada automática después).
- Clic derecho: ejecutar como administrador, abrir carpeta del juego, editar, favorito, ocultar, eliminar.
- Doble clic lanza. Soporta rutas `.exe` y protocolos (`steam://`, `com.epicgames.launcher://`).
- **Tiempo jugado** (solo si se tiene el proceso; sesiones < 1 min se ignoran).
- **Sincronización Steam/Epic por carpetas** (sin login): Steam lee `libraryfolders.vdf` + `appmanifest_*.acf`; Epic lee los `.item`. Fusiona sin pisar ediciones (match por `Source` + `ExternalId`).
- Los juegos de Steam/Epic eliminados **no reaparecen** al sincronizar (`ignored-games.json`); se restauran desde Configuración.
- Sincronizar al abrir (opcional), tamaño de tarjetas (pequeño/mediano/grande), reparar portadas faltantes.
- Portadas: API pública de búsqueda de Steam Store + CDN; caché local.

**Pendiente**
- ⬜ Botón Sincronizar con icono refresh y botón de carpetas de sincronización con icono propio (hoy salen como `[]` en algunos equipos).
- ⬜ **Ficha de juego** ("Información y logros"): editable, con información, logros de Steam (clave Web API en Configuración), enlaces que abran con el navegador elegido/incógnito, documentos locales y notas.
- 🟡 Probar los providers contra instalaciones reales de Steam/Epic (los comentarios del código admiten que se escribieron sin verificar; el formato de `libraryfolders.vdf` ha cambiado entre versiones de Steam).

### 5.2 Carpetas ✅ / ⬜
**Hecho:** tarjetas con persistencia JSON, diálogo con selector de 12 iconos, carpetas del sistema sembradas la primera vez (Escritorio, Documentos, Descargas, Imágenes, Música, Vídeos) que no se pueden eliminar, aviso "no encontrada" si la ruta ya no existe, detección de duplicados.

**Pendiente (pedido 2026-10-23)**
- ⬜ Convertir el módulo en un **explorador** con panel lateral dentro del propio módulo.
- ⬜ Cualquier acción de "mostrar / ir a" una carpeta o archivo (desde Buscar, asistente, etc.) debe abrirse en ese panel, **no** en el Explorador de Windows (`FolderNavigationService`).
- ⬜ Con las funciones del Explorador de Windows: navegar carpetas con muchas imágenes/vídeos, vista previa, etc.
- ⬜ UI para **ocultar** las carpetas del sistema (el modelo tiene `Hidden` y el diálogo dice "solo ocultar", pero no hay opción).

### 5.3 Multimedia ✅
- "Ahora suena" vía `GlobalSystemMediaTransportControlsSessionManager` (Spotify, reproductor nativo, pestañas de navegador con Media Session…): carátula (con degradado de respaldo), título, artista, app de origen, play/pausa, anterior/siguiente, barra de progreso con *seek*.
- Se prioriza la sesión que está reproduciendo; fundido al cambiar de pista; doble clic en carátula/badge trae la app al frente.
- **Mezclador** (NAudio/CoreAudio): volumen maestro + mute, volumen y mute por aplicación con su icono, seguimiento del dispositivo de salida por defecto, descarte de sesiones expiradas.
- Existe `MediaAppsService`/`MediaAppShortcut`/`AddEditMediaAppWindow` (accesos a apps multimedia) **sin conectar a la vista actual**.
- ⬜ Last.fm dentro de Multimedia (idea).

### 5.4 Música ⬜ (pospuesta)
Decidido: hacerlo **desde cero** (sin seguir entera la spec "MusicManager").
- ⬜ Descargar (URL o texto "artista - canción") o importar canciones, editar metadatos (título, artista, álbum, año, carátula) y guardarlas en una carpeta.
- ⬜ Metadatos lo más automáticos y correctos posible: no fiarse del título del vídeo ni de "canal = artista".
- ⬜ **Previsualizar** la canción y sus datos (y poder escucharla) antes de guardar.
- ⬜ El Launcher debe **descargar y gestionar él mismo `yt-dlp` y `FFmpeg`** (no están instalados).
- Reproductor: por ahora el de Windows; "después ya veremos".
- Candidatos de la spec: TagLib#, MusicBrainz, Cover Art Archive.

### 5.5 Buscar ⬜ (decidido empezar el 2026-10-21)
- ⬜ **Una sola barra** que busca todo junto (Launcher + archivos + web).
- ⬜ Los resultados de carpetas/archivos deben redirigir al panel lateral del módulo Carpetas (ver §5.2).
- Puede quedar integrado/solapado con el asistente de IA.

### 5.6 Comandos ✅
- Hub de accesos: aplicaciones, periféricos, utilidades de sistema y herramientas CLI (Kunai viene sembrado, vía PowerShell).
- Persistencia JSON, filtro por categoría, búsqueda, orden (nombre / favoritos / uso reciente), favoritos.
- Terminal: ninguna / CMD / PowerShell (`-NoExit`), carpeta de trabajo, **ejecutar como administrador** (UAC, verbo `runas`).
- Validación del destino (ruta, PATH refrescado, System32, protocolos) con aviso si no se encuentra.
- Ventana de **sugerencias** de utilidades de Windows (Administrador de dispositivos/tareas/discos, regedit, PowerShell, cmd…).
- Etiquetas CMD/PS y ADMIN en la tarjeta.

**Pendiente**
- ⬜ Elegir iconos desde una lista o usar el icono de la aplicación (hoy el glifo se escribe a mano).
- ⬜ Al añadir navegadores: opción/argumento para **iniciar en incógnito**.
- 💡 **Webs con inicio de sesión automático** (Brave, modo normal e incógnito; con y sin 2FA/captcha). Ver §8.

### 5.7 Monitor ✅
CPU, RAM, GPU y VRAM con porcentaje grande + sparkline (60 muestras, 1 s), red (↓/↑), actividad (uptime) y por unidad de disco: espacio usado y velocidad de lectura/escritura. VRAM total desde el registro (no WMI); GPU/VRAM por contadores de rendimiento. La tarjeta "Top procesos" se retiró para ajustar el diseño.
- ⬜ Intervalo de refresco configurable (decidido, no implementado).

### 5.8 Procesos ✅
- Procesos **agrupados** (mismo ejecutable bajo el ancestro de igual nombre, p. ej. Discord) con totales de CPU/RAM/GPU/VRAM; filas hijas expandibles (el estado se guarda por nombre, no por PID).
- Orden por columna, búsqueda (auto-expande), resaltado ámbar ≥ 80 %, filas en rojo si "no responde", botón por fila "Finalizar" / "Finalizar todo" con confirmación y árbol de procesos.
- CPU normalizada por núcleos (100 % = toda la máquina). Protege contra cerrarse a sí mismo.

**Pendiente**
- ⬜ Cabecera de la tabla con **% y MB/GB de consumo** junto al nombre de cada columna.
- 💡 Prioridad (`PriorityClass`), afinidad de núcleos, "abrir ubicación del archivo", detalles extendidos (hilos, handles, hora de inicio, `FileVersionInfo`), sparkline por proceso, persistir filtro/orden.

### 5.9 Configuración ✅
**Hecho:** iniciar con Windows (clave `Run` de HKCU) e iniciar minimizado, módulo inicial (o "último usado"), siempre encima, cerrar a la bandeja, recordar tamaño/posición, atajo global, tamaño de tarjetas, sincronizar al abrir, confirmar eliminación de juegos, restaurar juegos eliminados, carpetas de sincronización, abrir/reparar portadas, actualizaciones (buscar ahora, aplicar, buscar al abrir, carpeta de versiones), abrir carpeta de datos y registro de errores, restablecer ajustes, acerca de.

**Decidido pero no implementado**
- ⬜ Intervalo de refresco de Monitor/Procesos.
- ⬜ Confirmación al finalizar procesos (como opción).
- ⬜ Color de acento.
- ⬜ Clave de Web API de Steam (para logros).
- ⬜ "Actualizar automáticamente".
- ⬜ Ajustes del dashboard (umbrales, intervalo, efectos) y posición del widget Tiempo.
- (La copia de seguridad se descartó expresamente.)

### 5.10 Inicio / Dashboard 🟡
Primera versión dada por buena el 2026-10-12 (existe en el repo de GitHub; los archivos de esta instantánea no lo incluyen).
- ✅ Idea de partida: IWidget, WidgetCatalog, WidgetGridPanel y 3 widgets (Sistema, Juegos recientes, Ahora suena).
- ✅ Estados tipo terminal: azul **UPLINK** (dispositivo externo), verde **ONLINE**, naranja **WARNING**, rojo **DEFCON** (también si un recurso se mantiene muy alto de forma constante).

**Pendiente**
- ⬜ Rejilla de widgets **móviles y redimensionables** (posición, alto, ancho) sin scroll dentro de las cajas; añadir y eliminar widgets (de la app y de terceros).
- ⬜ Monitor/procesos deben ocupar el hueco bajo multimedia; etiquetas `[x]` como acceso rápido al menú; título `[ MULTIMEDIA ]` al inicio de su caja.
- ⬜ UPLINK con **varios dispositivos** (hoy solo muestra los auriculares), nombre/marca/modelo, auriculares/micro activos, transferencia de archivos, Bluetooth vs. cable; desplegable.
- ⬜ Registro general del PC (leer Visor de eventos de Windows) con las mismas categorías de estado; evaluador siempre activo en segundo plano.
- ⬜ Poder agrandar/reducir las partes fijas (monitor, reproductor, menú lateral) con presentación siempre ordenada.
- ⬜ Tarjetas de juegos/carpetas/comandos **siempre visibles** aunque el recuadro sea pequeño (reparto proporcional si caben 2,5 → 2…).
- ⬜ Fase 2: optimizar rendimiento, mover colores al tema, extraer `NowPlayingPanel` común, evitar instancias duplicadas, estética de película (esquinas de marco, secuencia de arranque), ventana maximizable y redimensionable a mano, modo de una columna en ventanas estrechas, menú lateral colapsable a solo iconos, arrastrar la ventana desde cualquier parte de la barra lateral.
- ⬜ **Widgets nuevos** (disponibles en "+ widgets", no añadidos por defecto): TODO, Portapapeles, Tiempo, Ofertas de juegos, Correo, Asistente y **Buscador**.

### 5.11 Asistente de IA local 🟡
**Hecho (en el código):** `AssistantWindow` (chat flotante con streaming token a token, selector `qwen3.5:4b` / `qwen3.5:9b`, `Esc` cierra, historial en memoria), `OllamaService` (cliente de `/api/chat` en NDJSON y `/api/tags`), `GlobalHotkeyService` (hook de teclado de bajo nivel para `Alt+Espacio`).

**Pendiente**
- ⬜ **Fase 1:** enlazar `Alt+Espacio` con `AssistantWindow` (hoy nadie la abre).
- ⬜ Cambios de comportamiento pedidos: **no cerrarse al perder el foco**, permitir **varios chats a la vez** y **persistir los chats**.
- ⬜ Comprobar Ollama al abrir (`IsAvailableAsync`) y avisar con una causa clara.
- ⬜ Más adelante: portapapeles/texto seleccionado, buscar/trabajar con archivos, acciones de Windows, abrir apps y comandos, voz.
- ⬜ **Segundo plano** (orden decidido): (1) vigilar el sistema y optimizar reutilizando Monitor/Procesos, (2) reglas de automatización/horarios, (3) aprender hábitos y sugerir.
- El roadmap del asistente no es fijo: se integran o descartan ideas sobre la marcha. Hardware objetivo: i5-12400F, 32 GB, RTX 3060 12 GB (rápido, discreto, bajo impacto).

---

## 6. Datos y persistencia

Todo en `%AppData%\Launcher\` (JSON, sin base de datos):

| Archivo | Contenido |
|---|---|
| `config.json` | Ajustes generales (`AppSettingsService`) |
| `settings.json` | Rutas de Steam y Epic (`LibraryPathsService`) |
| `games.json` | Biblioteca de juegos |
| `ignored-games.json` | Juegos de Steam/Epic eliminados (no se re-añaden) |
| `folders.json` | Accesos a carpetas |
| `commands.json` | Accesos de Comandos |
| `media_apps.json` | Apps multimedia (sin UI conectada) |
| `covers\` | Caché de portadas (`.jpg` / `.png`) |
| `logs\launcher-AAAAMMDD.log` | Registro de errores (se conservan 7 días) |

- "Iniciar con Windows" **no** se guarda en `config.json`: la fuente de verdad es el registro (`HKCU\...\Run`).
- Los archivos corruptos no tumban la app: se arranca con valores por defecto (los de comandos re-siembran Kunai).

---

## 7. Pendiente (hoja de ruta)

**Inmediato**
1. ⚠️ Arreglar los errores de compilación E1 y E2 y la mezcla de conflicto de este README (E3).
2. Instalar con `Setup.exe`, publicar una `0.1.1` y verificar que la app instalada detecta la actualización (problema abierto desde 2026-10-10: se ejecutaba `publish\Launcher.exe`, que no está instalado).
3. Actualización automática al detectar versión nueva.

**Corto plazo**
4. Buscar (barra única) + redirección al explorador de Carpetas.
5. Carpetas como explorador con panel lateral.
6. Asistente Fase 1 (hotkey + chat con Ollama persistente, multi-chat).
7. Ficha de juego.
8. Iconos de Sincronizar/Carpetas en Juegos; cabeceras de Procesos; iconos e incógnito en Comandos.
9. Ajustes de Configuración pendientes.

**Medio plazo**
10. Dashboard: widgets libres, UPLINK completo, registro general, fase 2.
11. Música (descarga/importación con yt-dlp + FFmpeg gestionados).
12. Fondo animado/cambiable y estilo común en todos los menús.
13. Módulo **Dispositivos** (ver y gestionar hardware conectado, app del fabricante, ajustes de sistema).
14. Asistente en segundo plano (vigilar → reglas → hábitos).

**Hecho recientemente (histórico)**: caché de portadas, instancia única, "eliminar" sin reaparición, arreglo de maximizar sin bordes, registro de errores, tiempo de juego, extras de Juegos (arrastrar .exe, abrir carpeta, admin, atajos), versión única en el csproj, icono propio, actualizaciones con Velopack, migración a .NET 10.

---

## 8. Ideas y decisiones abiertas

- 💡 **Modo escritorio:** Launcher a pantalla completa como si fuera el escritorio (sin ver barra de tareas ni escritorio de Windows; al minimizar todo con `Win+D` aparece el launcher). Con varios monitores, uno por pantalla con configuración distinta; con uno solo, configuración estándar. Incluso con su propio menú, barra de tareas e iconos. Vías a valorar: ventana WorkerW, ventana sin bordes a pantalla completa, o sustituir el shell. **Sin decisión.**
- 💡 **Linux (Nobara KDE Plasma):** posible cambio de Windows. Tres vías: adaptar el Launcher actual, reescribirlo (p. ej. en Python, con el asistente "Jarvis" como pestaña) o aceptar una versión Linux sin paridad total. Los módulos muy ligados a WinRT/CoreAudio/PerformanceCounter/registro serían los más costosos. **Sin decisión.**
- 💡 **Comandos web con login automático** (Brave, normal e incógnito, con y sin 2FA/captcha). Hay que decidir dónde se guardan las credenciales (almacén seguro de Windows, no en JSON en claro).
- 💡 Fondo animado y trabajo en segundo plano del Launcher.
- 💡 TODO compartida como Artifact: https://claude.ai/artifact/LP2msaApaDb6yqaEFaU6p7 (colección `tasks`).

---

## 9. Límites conocidos

**Plataforma**
- **Solo Windows** (WPF, WinRT, CoreAudio, registro, P/Invoke).
- Con `AllowsTransparency="True"` (esquinas redondeadas) el *grip* de redimensionar de WPF no se ve, aunque se puede redimensionar arrastrando los bordes. Alternativa: `WindowChrome` + `DwmSetWindowAttribute` (Windows 11).
- La app no está firmada: Smart App Control puede bloquearla.
- Las actualizaciones no funcionan fuera de una copia instalada con Velopack.

**Monitor y procesos**
- GPU/VRAM dependen de los contadores "GPU Engine" / "GPU Adapter Memory" del driver (WDDM); en equipos sin ellos se muestra 0.
- La VRAM total sale del registro (`HardwareInformation.qwMemorySize`); con varias GPUs se toma el valor más alto. WMI `AdapterRAM` no es fiable por encima de 4 GB.
- Procesos de sistema o elevados: sin acceso a CPU, módulo principal o `Responding`; no se pueden finalizar sin ejecutar el Launcher como administrador.
- "Congelado" solo es fiable en procesos con ventana.
- La agrupación es **por nombre de ejecutable** a lo largo de la cadena de padres; procesos sin padre vivo del mismo nombre quedan como raíz propia.

**Multimedia**
- Solo ve apps que registran sesión en Windows (SMTC). Una app que no lo haga no aparece en "Ahora suena".
- El mezclador trabaja sobre el **dispositivo de salida por defecto**; "traer al frente" solo reconoce Spotify, Edge, Chrome, Firefox y VLC.
- Si dos sesiones de audio comparten PID, se muestran como una.

**Juegos**
- El tiempo jugado solo se mide con ejecutables propios (no con `steam://` ni otros protocolos) y se pierde si el Launcher se cierra durante la partida.
- Las portadas dependen de internet y de que el nombre exista en la Steam Store; para Epic y manuales la coincidencia puede ser errónea (se toma el primer resultado).
- La sincronización exige indicar/autodetectar las carpetas de Steam/Epic; no hay login ni soporte para GOG, EA, Ubisoft, etc.

**Comandos**
- El refresco de PATH se inyecta en el propio comando (no se puede usar `EnvironmentVariables` junto con `runas`).
- Los protocolos/URI (`ms-settings:`, `steam://`) no se pueden verificar localmente.

**Atajos**
- `RegisterHotKey` no captura `Alt+Espacio` (lo reserva Windows); por eso el asistente usa un hook de bajo nivel. Solo hay tres combinaciones fijas para mostrar/ocultar el Launcher.

**Otros**
- Sin pruebas automáticas ni integración continua.
- Interfaz solo en español, sin sistema de localización.
- Tema claro inexistente (solo `Theme.Dark.xaml`).
- La app no almacena ninguna credencial hoy; cuando exista login automático habrá que resolver el almacenamiento seguro.

---

## 10. Errores y deuda técnica

### Errores que impiden compilar o publicar

#### E1. `AppSettingsService.Reset()` tiene código de clase pegado dentro del método
Dentro de `Reset()` hay un bloque de "Actualizaciones" con un campo `static readonly` y dos propiedades (`UpdateSource`, `CheckUpdatesOnStartup`), y además `Save();` queda **en mitad** del método. En C# no se pueden declarar miembros de clase dentro de un método → **error de compilación**, y `UpdateSource = ...` después del `Save()` no se guardaría.

Corrección: sacar el bloque fuera del método y dejar el `Save()` al final.

```csharp
// ----- Actualizaciones (nivel de clase) -----
public static readonly string DefaultUpdateSource = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LauncherReleases");

public string UpdateSource { get; set; } = DefaultUpdateSource;
public bool CheckUpdatesOnStartup { get; set; } = true;

public void Reset()
{
    StartMinimized = false;
    StartModule = "games";
    AlwaysOnTop = false;
    CloseToTray = false;
    RememberWindowBounds = true;
    WindowLeft = WindowTop = WindowWidth = WindowHeight = null;
    Hotkey = "None";
    SyncOnStartup = false;
    ConfirmDelete = true;
    CardSize = "medium";
    UpdateSource = DefaultUpdateSource;
    CheckUpdatesOnStartup = true;
    Save();
}
```

> `DefaultUpdateSource` es `static`; `System.Text.Json` no lo serializa, así que no afecta al `config.json`.

#### E2. Falta la referencia a Velopack en el `.csproj`
`Program.cs` (`VelopackApp`) y `UpdateService` (`UpdateManager`) usan Velopack, pero `Launcher.csproj` solo lista NAudio, PerformanceCounter y Management. Añadir:

```xml
<PackageReference Include="Velopack" Version="(la misma que la herramienta vpk instalada)" />
```
Conviene que la versión del paquete coincida con la del CLI `vpk` para evitar incompatibilidades de feed.

#### E3. Este README tenía marcadores de conflicto de Git
La versión anterior contenía `<<<<<<< HEAD`, `=======` y `>>>>>>>` (mezcla del README del proyecto con el de "Launcher-personal"). Este archivo es una versión limpia que sustituye a ambas.

### Errores de comportamiento / inconsistencias

| # | Dónde | Problema | Propuesta |
|---|---|---|---|
| B1 | `MainWindow` | `search` y `processes` tienen `Order = 4`; el desempate es indefinido. | Dar órdenes únicos (p. ej. Procesos 5, Buscar 4). |
| B2 | `Assistant` | `AssistantWindow` no se abre desde ninguna parte; `GlobalHotkeyService` no se usa. | Enlazarlos (Fase 1). |
| B3 | `AssistantWindow` | Se cierra en `Deactivated`, contradice lo pedido (no cerrar al perder foco, multi-chat, persistencia). | Quitar `Deactivated`, persistir chats. |
| B4 | `OllamaService` / ventana | El error de red se muestra siempre como "no se ha podido contactar con Ollama", aunque sea un 404 por modelo inexistente; no hay cancelación desde la UI; no se envía `keep_alive`; el modelo elegido no se recuerda. | Distinguir causas, botón de parar, `keep_alive` configurable. |
| B5 | `SettingsView.LoadFromState` | `CheckUpdatesToggle.IsChecked` y `RefreshUpdateRow()` se ejecutan **antes** de `_loading = true`; al recargar (restablecer, error de arranque) dispara `Toggle_Changed` y guarda. Inofensivo, pero incorrecto. | Poner `_loading = true` al principio. |
| B6 | `SettingsView.BuildStartModuleChips` | Suscribe `UpdateService.Instance.Changed` cada vez que se crea la vista y nunca se desuscribe (fuga/duplicados). | Mover la suscripción al constructor con `Unloaded` para quitarla. |
| B7 | Configuración | Dos archivos de ajustes distintos (`config.json` y `settings.json`) para cosas del mismo tipo. | Unificar en una migración. |
| B8 | `SystemMonitorService` | `SampleTopProcesses()` itera todos los procesos cada segundo, pero la tarjeta "Top procesos" ya no existe. | Eliminar o hacer opcional. |
| B9 | `ProcessMonitorService` | Cada refresco (1,5 s) lee `MainModule`, `Responding` y `TotalProcessorTime` de **todos** los procesos y crea contadores GPU por instancia → coste alto de CPU. | Cachear la ruta por PID, leer `Responding` solo de procesos con ventana, intervalo configurable. |
| B10 | `MonitorView.Tick` | `Drives.Clear()` + volver a añadir en cada tick recrea los elementos (parpadeo, pérdida de scroll). | Actualizar en sitio. |
| B11 | `FoldersView` / `AddEditFolderWindow` | Las carpetas del sistema "solo se pueden ocultar", pero no hay forma de ocultarlas desde la UI. | Añadir opción de ocultar/mostrar. |
| B12 | `GamesView` | `PlayMenuItem_Click` existe pero el menú contextual no tiene la entrada "Jugar". | Añadirla o borrar el manejador. |
| B13 | `MediaApps*` | Servicio, modelo y diálogo completos sin vista que los use. | Conectarlos o retirarlos. |
| B14 | `MainWindow.xaml` | El texto de versión inicial (`v0.1.0`) está fijo en XAML; se sobrescribe en código. | Dejar solo el del código. |
| B15 | `CommandsService.Launch` | El texto de comando y argumentos se concatena tal cual en `cmd /k` o `powershell -Command`. Al ser datos del propio usuario no es una vulnerabilidad externa, pero un argumento con comillas puede romper el comando. | Escapar o usar `ArgumentList` donde se pueda. |
| B16 | Providers de Juegos | Los comentarios dicen "no probado con instalación real"; la instantánea del Epic provider lanza por protocolo, mientras que lo decidido en el proyecto usa `InstallLocation + LaunchExecute` con respaldo por protocolo. | Verificar qué versión está en `main`. |

### Observaciones sobre la instantánea de archivos
Los archivos de esta revisión **no incluyen** Dashboard, Música ni Buscar (según las notas, el Dashboard existe en el repo de GitHub). Los cambios deben hacerse sobre los archivos reales del repo, no sobre copias reconstruidas de memoria. Algunas decisiones registradas (p. ej. "Actualizar automáticamente", color de acento, intervalo de refresco) tampoco aparecen todavía en el código adjunto.

---

## 11. Trampas de plataforma (gotchas)

- **WMI y VRAM > 4 GB:** poco fiable → usar el valor del registro `HardwareInformation.qwMemorySize`.
- **PID del padre:** `System.Diagnostics.Process` no lo expone → `CreateToolhelp32Snapshot`.
- **`ProcessStartInfo.EnvironmentVariables` + `UseShellExecute=true`:** incompatibles → inyectar variables dentro del comando.
- **PATH obsoleto** en procesos de larga vida → leer Machine + User del registro al lanzar.
- **Ambigüedades WinForms/WPF** (`ImplicitUsings`): quitar el `using` global `System.Windows.Forms` en el csproj; calificar `System.Drawing.Icon.ExtractAssociatedIcon` y `new System.Windows.Point()`; alias `WinForms = System.Windows.Forms`.
- **WinRT `Windows.Media.Control`:** requiere TFM `…-windows10.0.19041.0` con `SupportedOSPlatformVersion 10.0.17763.0`.
- **`AsStreamForRead()`** no existe en proyectos .NET normales → `DataReader` + `MemoryStream`.
- **Agrupar procesos por nombre**, no por PID (los PID cambian entre refrescos).
- **`Alt+Espacio`:** `RegisterHotKey` no lo captura de forma fiable (Windows lo reserva para el menú de sistema; `AltGr+Espacio` sí funcionaba porque envía `Ctrl+Alt+Espacio`) → hook `WH_KEYBOARD_LL` consumiendo la tecla; guardar el delegado en un campo para que el GC no lo recoja.
- **Ventana sin bordes y transparente** maximiza sobre todo el monitor y tapa la barra de tareas → `WM_GETMINMAXINFO` con el área de trabajo.
- **`Application.Shutdown()`** ignora `Cancel` en `Closing` y rompe "Cerrar a la bandeja" → usar `Close()`.
- **Cancelar el cierre** bloquea el apagado de Windows → marcar `_exiting` en `SessionEnding`.
- **UAC cancelado** (error nativo 1223) no es un fallo real.

---

## 12. Convenciones de trabajo

- **Entrega de código:** archivos **completos** (no diffs parciales), indicando siempre la ruta de destino y si es archivo nuevo o reemplazo.
- Los cambios se hacen sobre el proyecto real de GitHub (`ferranmi/Launcher-personal`, rama `main`).
- Este README se actualiza a medida que se completan pendientes y se entrega como archivo completo.
- Errores: se registran en `%AppData%\Launcher\logs\`; la app nunca debe reventar por un JSON corrupto o un proveedor roto.
- Al crear un módulo real, se sustituye su `PlaceholderModule` por su propia clase `…Module : IModule` en `MainWindow.xaml.cs`.
