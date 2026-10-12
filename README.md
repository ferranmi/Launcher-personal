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
| Comandos | Funcional | Accesos a apps, herramientas de sistema y CLI, con CMD/PowerShell, ejecución como administrador e iconos personalizables |
| Monitor | Funcional | CPU, RAM, GPU, VRAM, red y discos con sparklines |
| Procesos | Funcional | Procesos agrupados por ejecutable, con CPU/RAM/GPU/VRAM y finalizar tarea |
| Configuración | Funcional | Arranque, ventana, atajo global, tarjetas, refresco, color de acento, actualizaciones y datos |

### Iconos en Comandos

Cada comando puede tener su icono de tres formas (la imagen tiene prioridad sobre el glifo):

- **Glifo**: una lista de iconos de Segoe MDL2/Fluent (terminal, ajustes, navegador, disco…).
- **Icono de la app**: se extrae del ejecutable indicado en el comando. En comandos que son un protocolo (`ms-settings:`) no hay icono que extraer.
- **Imagen propia**: un `.ico`, `.png` o `.jpg` a elegir. Se copia a la carpeta de datos, así que sigue funcionando si mueves o borras el original.

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
| `covers/` | Caché de portadas de juegos e iconos de comandos |
| `logs/` | Registro de errores (se conservan 7 días) |

## Sincronización con Steam y Epic

No usa login: lee los archivos locales de cada launcher.

- **Steam**: `steamapps/libraryfolders.vdf` y los `appmanifest_*.acf`. Lanza con `steam://rungameid/{appid}`.
- **Epic**: manifiestos `.item` (JSON) en `ProgramData\Epic\EpicGamesLauncher\Data\Manifests`.
- Las portadas se descargan de la Steam Store (API pública, sin clave) y se guardan en caché.

## Publicar una versión y actualizar

Las actualizaciones usan [Velopack](https://velopack.io).

1. Sube `<Version>` en `Launcher.csproj` (cada release necesita una versión distinta y mayor).
2. Ejecuta `.\publish.ps1` (o `powershell -ExecutionPolicy Bypass -File .\publish.ps1` si PowerShell bloquea el script). Publica, empaqueta con `vpk` y deja el resultado en `%USERPROFILE%\LauncherReleases`.
3. La primera vez, instala con el `Setup.exe` de esa carpeta.
4. Las copias **instaladas** buscan versiones nuevas al abrir (a los 8 segundos) y cada 6 horas, y por defecto **se actualizan solas**: descargan, se reinician y quedan en la versión nueva.

En Configuración > Actualizaciones se puede desactivar la búsqueda al abrir o la actualización automática. Con esta última desactivada, la barra lateral solo avisa ("vX disponible") y se actualiza con un clic.

Ejecutando desde Visual Studio o desde `publish\Launcher.exe` la app no está instalada, así que no puede actualizarse. Para diagnosticar, el registro de errores guarda una línea `Actualizaciones:` con el resultado de cada comprobación.

## Limitaciones conocidas

- Con `AllowsTransparency="True"` (necesario para las esquinas redondeadas) el grip de resize nativo no se ve, aunque se puede redimensionar desde los bordes.
- Los iconos usan Segoe MDL2 Assets (Windows 10 y 11).
- La detección de Steam y Epic sigue el formato conocido de sus archivos y puede necesitar ajustes si cambian.