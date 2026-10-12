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
| Inicio | Funcional | Dashboard: sistema reducido, lo que suena, accesos a juegos, carpetas y comandos, y top 5 de procesos por CPU, RAM o GPU. Hueco para el futuro buscador |
| Juegos | Funcional | Biblioteca con portadas, sincronización con Steam/Epic por carpetas, favoritos, ocultos, tiempo de juego, arrastrar `.exe`, ficha editable (información, logros de Steam, enlaces, documentos y notas) |
| Carpetas | Funcional | Accesos directos a carpetas con selector de icono |
| Multimedia | Funcional | Reproductor "Now Playing" (SMTC de Windows) y mezclador de volumen por aplicación |
| Música | Placeholder | Pospuesto |
| Buscar | Placeholder | Probablemente lo sustituya el asistente de IA |
| Comandos | Funcional | Accesos a apps, herramientas de sistema y CLI, con CMD/PowerShell, ejecución como administrador, iconos personalizables e incógnito en navegadores |
| Monitor | Funcional | CPU, RAM, GPU, VRAM, red y discos con sparklines |
| Procesos | Funcional | Procesos agrupados por ejecutable, con CPU/RAM/GPU/VRAM, el total del sistema en la cabecera de cada columna y finalizar tarea |
| Configuración | Funcional | Arranque, ventana, atajo global, tarjetas, refresco, color de acento, actualizaciones y datos |

### Iconos en Comandos

Cada comando puede tener su icono de tres formas (la imagen tiene prioridad sobre el glifo):

- **Glifo**: una lista de iconos de Segoe MDL2/Fluent (terminal, ajustes, navegador, disco…).
- **Icono de la app**: se extrae del ejecutable indicado en el comando. En comandos que son un protocolo (`ms-settings:`) no hay icono que extraer.
- **Imagen propia**: un `.ico`, `.png` o `.jpg` a elegir. Se copia a la carpeta de datos, así que sigue funcionando si mueves o borras el original.

### Incógnito en navegadores

Si el destino de un comando es un navegador conocido, el formulario muestra la casilla **Abrir en ventana privada** y el Launcher añade el argumento correspondiente al lanzarlo:

| Navegador | Argumento |
|---|---|
| Chrome, Chromium, Brave, Vivaldi | `--incognito` |
| Edge | `--inprivate` |
| Firefox, LibreWolf, Waterfox | `-private-window` |
| Opera | `--private` |

En un comando nuevo la casilla viene marcada la primera vez que se reconoce un navegador; se puede desmarcar. Los navegadores que no están en el PATH (Chrome, Firefox) se localizan también por la clave `App Paths` del registro.

### Información y logros de un juego

Clic derecho sobre un juego > **Información y logros** abre su ficha, parecida a la página de un juego en la biblioteca de Steam o Epic y editable:

- **Información**: descripción, géneros, desarrollador, editor, fecha, puntuación y capturas, de la Steam Store (sin clave). Se descarga sola la primera vez. Todo se puede corregir, y lo editado se conserva al actualizar. En juegos de Epic o añadidos a mano se busca por nombre; el ID de Steam se puede escribir a mano si no acierta.
- **Logros**: los de Steam, con icono, descripción, fecha de desbloqueo y porcentaje global de jugadores. Necesitan una **clave gratuita de la Web API de Steam** (Configuración > Cuenta de Steam, o el botón de la propia pestaña). El SteamID se detecta de tu instalación de Steam. Los detalles de juego de tu perfil deben ser públicos. Epic no permite leer logros desde otras aplicaciones.
- **Enlaces y documentos**: enlaces web propios, que se abren con el navegador que elijas (incluido el modo privado, como en Comandos), y archivos o carpetas locales (guías, partidas guardadas, mods).
- **Notas**: texto libre.

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
| `settings.json` | Rutas de Steam y Epic, clave de la Web API de Steam y SteamID (la clave va en texto plano) |
| `games.json`, `ignored-games.json` | Biblioteca y juegos eliminados de la sincronización |
| `folders.json`, `commands.json`, `media_apps.json` | Accesos de cada módulo |
| `covers/` | Caché de portadas de juegos e iconos de comandos |
| `game-info/` | Ficha de cada juego: información descargada, tus ediciones, logros, enlaces, documentos y notas |
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
