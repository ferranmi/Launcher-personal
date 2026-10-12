# Launcher

Aplicación de escritorio modular en C# / WPF (.NET 10) con tema oscuro estilo terminal
(verde fósforo sobre casi negro). Cada módulo es independiente y se registra en
`MainWindow.xaml.cs`.

## Cómo ejecutarlo

1. Instala el **.NET 10 SDK** (`dotnet --version` para comprobarlo).
2. Abre la carpeta del proyecto en Visual Studio o VS Code.
3. `F5` para depurar, o `dotnet run` desde la carpeta del proyecto.

## Módulos

| Módulo | Estado | Qué hace |
|---|---|---|
| Juegos | Funcional | Biblioteca con portadas, sincronización con Steam/Epic por carpetas, favoritos, ocultos, tiempo de juego, arrastrar `.exe` |
| Carpetas | Funcional | Accesos directos a carpetas con selector de icono |
| Multimedia | Funcional | Reproductor "Now Playing" (SMTC de Windows) y mezclador de volumen por aplicación |
| Música | Placeholder | Pospuesto |
| Buscar | Placeholder | Probablemente lo sustituya el asistente de IA |
| Comandos | Funcional | Accesos a apps, herramientas de sistema y CLI, con CMD/PowerShell y ejecución como administrador |
| Monitor | Funcional | CPU, RAM, GPU, VRAM, red y discos con sparklines |
| Procesos | Funcional | Procesos agrupados por ejecutable, con CPU/RAM/GPU/VRAM y finalizar tarea |
| Configuración | Funcional | Arranque, ventana, atajo global, tarjetas, actualizaciones y datos |

## Arquitectura

```
Launcher/
├── Core/
│   ├── Interfaces/   # IModule, IGameProvider
│   ├── Models/       # Game, FolderShortcut, CommandShortcut...
│   ├── Services/     # AppSettings, Log, Update, Startup, Audio, Ollama...
│   ├── Converters/   # Conversores de bindings
│   └── Mvvm/         # ViewModelBase, RelayCommand
├── UI/
│   ├── Themes/       # Theme.Dark.xaml (colores, tipografía, estilos)
│   └── Controls/     # NavButton
├── Modules/          # Un módulo por carpeta (vista + servicio + modelos propios)
├── MainWindow.xaml(.cs)
├── App.xaml(.cs)
└── Launcher.csproj
```

- Cada módulo implementa `IModule` y se construye en este orden: modelo → servicio → módulo → vista.
- Los módulos aún sin implementar usan `PlaceholderModule`.
- La persistencia son archivos JSON en `%AppData%\Launcher\` (sin base de datos externa).
- Los colores y fuentes son `DynamicResource`, así que un tema nuevo solo necesita las mismas claves.

## Datos en `%AppData%\Launcher\`

| Archivo / carpeta | Contenido |
|---|---|
| `config.json` | Ajustes generales |
| `settings.json` | Rutas de Steam y Epic |
| `games.json`, `ignored-games.json` | Biblioteca y juegos eliminados de la sincronización |
| `folders.json`, `commands.json`, `media_apps.json` | Accesos de cada módulo |
| `covers/` | Caché de portadas |
| `logs/` | Registro de errores (se conservan 7 días) |

## Sincronización con Steam y Epic

No usa login: lee los archivos locales de cada launcher.

- **Steam**: `steamapps/libraryfolders.vdf` y los `appmanifest_*.acf`. Lanza con `steam://rungameid/{appid}`.
- **Epic**: manifiestos `.item` (JSON) en `ProgramData\Epic\EpicGamesLauncher\Data\Manifests`.
- Las portadas se descargan de la Steam Store (API pública, sin clave) y se guardan en caché.

## Publicar una versión y actualizar

Las actualizaciones usan [Velopack](https://velopack.io).

1. Sube `<Version>` en `Launcher.csproj` (cada release necesita una versión distinta).
2. Ejecuta `.\publish.ps1`: publica, empaqueta con `vpk` y deja el resultado en `%USERPROFILE%\LauncherReleases`.
3. La primera vez, instala con el `Setup.exe` de esa carpeta.
4. Las copias **instaladas** detectan las versiones nuevas solas (al abrir y cada 6 horas) y se actualizan desde el número de versión de la barra lateral.

Ejecutando desde Visual Studio o desde `publish\Launcher.exe` la app no está instalada, así que no puede actualizarse.

## Limitaciones conocidas

- Con `AllowsTransparency="True"` (necesario para las esquinas redondeadas) el grip de resize nativo no se ve, aunque se puede redimensionar desde los bordes.
- Los iconos usan Segoe MDL2 Assets (Windows 10 y 11).
- La detección de Steam y Epic sigue el formato conocido de sus archivos y puede necesitar ajustes si cambian.