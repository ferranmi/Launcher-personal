# Launcher

Plataforma de escritorio modular en C# / WPF sobre **.NET 10**, con tema oscuro
de acento verde y toques retro-terminal. Cada modulo (Juegos, Carpetas,
Multimedia, Comandos, Monitor, Procesos, Configuracion...) es una pieza
independiente que se registra en `MainWindow.xaml.cs`.

## Requisitos

- Windows 10 (1809 o superior) o Windows 11.
- **SDK de .NET 10** (`dotnet --version` debe empezar por `10`).

## Ejecutar en desarrollo

1. Abre la carpeta del proyecto en VS Code o Visual Studio.
2. `F5` (o `Ctrl+F5` sin depurador). Por terminal: `dotnet run`.

Notas:

- Desde `dotnet run` / Visual Studio la app **no esta instalada**, asi que la
  comprobacion de actualizaciones queda en "no instalada" (es lo esperado).
- Si Windows bloquea el `.exe` con el error 4551 ("Una directiva de Control de
  aplicaciones bloqueo este archivo"), es Smart App Control: el ejecutable no
  esta firmado.
- Las versiones Debug y Release usan nombres de instancia unica distintos, asi
  que se puede desarrollar con la version instalada abierta.

## Modulos

| Modulo | Estado |
| --- | --- |
| Juegos | Biblioteca con portadas, Steam/Epic por carpetas, favoritos, ocultos, tiempo de juego, arrastrar `.exe` |
| Carpetas | Accesos directos a carpetas |
| Multimedia | Reproduccion actual del sistema y mezclador de volumen por aplicacion |
| Comandos | Accesos a apps, herramientas de sistema y comandos de terminal |
| Monitor | CPU, RAM, GPU/VRAM, red y discos en tiempo real |
| Procesos | Lista agrupada con CPU, RAM, GPU y VRAM; finalizar tareas |
| Configuracion | Arranque, ventana, bandeja, atajo global, juegos, actualizaciones, datos |
| Musica, Buscar | Placeholders pendientes |

El asistente de IA local (Ollama) esta en preparacion: la ventana y el cliente
existen en `Modules/Assistant` y `Core/Services/OllamaService.cs`, pero todavia
no estan integrados.

## Datos del usuario

Todo se guarda en `%AppData%\Launcher` y no se toca al actualizar:

- `config.json`: ajustes de la aplicacion.
- `settings.json`: carpetas de Steam y Epic.
- `games.json`, `ignored-games.json`, `folders.json`, `commands.json`,
  `media_apps.json`.
- `covers\`: portadas descargadas.
- `logs\`: registro de errores (se conservan 7 dias).

## Publicar y actualizar

Las actualizaciones usan [Velopack](https://velopack.io).

Una sola vez:

```powershell
dotnet tool install -g vpk
```

Para cada version nueva:

1. Sube `<Version>` en `Launcher.csproj`.
2. Haz commit y crea la etiqueta (`git tag v0.2.0`).
3. Ejecuta `.\publish.ps1`. Publica en modo autocontenido y empaqueta en
   `%USERPROFILE%\LauncherReleases`.

La primera vez, instala con el `Setup.exe` de esa carpeta. Despues, la app
instalada busca versiones nuevas al abrir y cada 6 horas; si hay una, el texto
de version del sidebar se vuelve verde y al pulsarlo se actualiza. La carpeta
de versiones se cambia en Configuracion > Actualizaciones.

## Estructura

```
Launcher/
├── Program.cs                  # Main propio (Velopack antes que WPF)
├── App.xaml(.cs)               # instancia unica y manejo de errores
├── MainWindow.xaml(.cs)        # ventana, sidebar y registro de modulos
├── publish.ps1                 # publica y empaqueta una version nueva
├── Assets/launcher.ico
├── Core/
│   ├── Interfaces/             # IModule, IGameProvider
│   ├── Models/
│   ├── Services/               # ajustes, ventana, actualizaciones, registro...
│   ├── Converters/
│   └── Mvvm/
├── UI/
│   ├── Themes/Theme.Dark.xaml  # colores, estilos, tipografia
│   └── Controls/NavButton.cs
└── Modules/                    # un modulo por carpeta
```

## Tema

- Colores y estilos centralizados en `UI/Themes/Theme.Dark.xaml`.
- `Segoe UI` para texto normal y `Cascadia Mono` para el toque retro.
- Iconos con **Segoe MDL2 Assets** (Windows 10 y 11).

Para un tema claro bastaria crear `Theme.Light.xaml` con las mismas claves y
cambiar el `MergedDictionary` en `App.xaml`; hoy varios estilos usan
`StaticResource`, asi que habria que pasarlos a `DynamicResource` para
cambiarlo en caliente.