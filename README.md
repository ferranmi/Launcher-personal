# Launcher — Esqueleto base

Plataforma de escritorio modular en C# / WPF (.NET 8). Esta es la base: ventana,
navegación entre módulos y sistema de temas. Los 9 módulos son placeholders vacíos
para validar que la arquitectura funciona antes de implementarlos uno a uno.

## Cómo ejecutarlo

1. Necesitas el **.NET 8 SDK** instalado (`dotnet --version` para comprobarlo).
2. Abre la carpeta `Launcher/` en VS Code.
3. `F5` (con C# Dev Kit) o `Ctrl+F5` para ejecutar sin debugger.
   - Si prefieres terminal: `dotnet run` dentro de la carpeta.

## Tema

Dark UI moderna con acento verde y toques retro-terminal, sin caer en el CRT completo:

- **Colores**: fondo casi negro con tinte verde (`Color.Background`), verde fósforo
  atenuado como acento (`Color.Accent` = `#3ECF7E`), todo centralizado en
  `UI/Themes/Theme.Dark.xaml`.
- **Tipografía**: `Segoe UI` para texto normal/legible; `Cascadia Mono` (con
  fallback a Consolas / Courier New) reservada para el toque retro — título de
  la ventana, nombres de módulo en el sidebar, badges y datos numéricos/stats.
- **Detalles retro**: el módulo activo en el sidebar muestra una barrita verde
  tipo "cursor de terminal" a la izquierda; el punto junto a "LAUNCHER" en la
  barra de título tiene un glow verde sutil (`Effect.AccentGlow`, reutilizable).
- **Estilos ya listos para usar en módulos futuros**: `CardStyle` (tarjetas,
  para grids de Juegos/Carpetas), `BadgeStyle` (chips tipo "Steam" / "Favorito"),
  `MonoTextStyle` / `MonoHeadingStyle` (texto monoespaciado).

Para un tema claro más adelante: crear `Theme.Light.xaml` con las mismas claves
(`Brush.Background`, `Brush.Accent`, etc.) y cambiar el `MergedDictionary` en
`App.xaml` — ningún módulo tendría que tocarse porque todos usan `DynamicResource`.

## Qué hay montado

- **Ventana sin bordes** con esquinas redondeadas, sombra, barra de título propia
  (arrastrable, doble clic para maximizar) — `MainWindow.xaml`.
- **Sidebar** con los 9 módulos, generado dinámicamente a partir de una lista de
  `IModule` — no hay nada hardcodeado en XAML, añadir un módulo es añadir una línea.
- **Sistema de temas** en `UI/Themes/Theme.Dark.xaml`: todos los colores están
  centralizados como `DynamicResource`, así que añadir un tema claro más adelante
  es crear `Theme.Light.xaml` con las mismas claves y cambiar el `MergedDictionary`
  en tiempo de ejecución.
- **`IModule`** (`Core/Interfaces/IModule.cs`): contrato que deberá implementar cada
  módulo real. De momento todos usan `PlaceholderModule`, que solo pinta un texto.

## Estructura

```
Launcher/
├── Core/
│   ├── Interfaces/IModule.cs       # contrato de cada módulo
│   └── Models/PlaceholderModule.cs # implementación temporal
├── UI/
│   ├── Themes/Theme.Dark.xaml      # colores, estilos, tipografía
│   └── Controls/NavButton.cs       # botón del sidebar (icono + texto)
├── Modules/                        # carpetas vacías, un módulo por carpeta
│   ├── Games/  Folders/  Media/  Music/
│   ├── Search/ Commands/ Monitor/ Processes/ Settings/
├── MainWindow.xaml(.cs)
├── App.xaml(.cs)
└── Launcher.csproj
```

Las carpetas en `Modules/` están vacías a propósito: cuando implementes un módulo,
creas ahí su `UserControl` (vista), su `...Module.cs` (implementa `IModule`) y sus
propios modelos/servicios, y lo registras en `MainWindow.xaml.cs` sustituyendo su
`PlaceholderModule` correspondiente.

## Módulo Juegos

Primer módulo real (ya no es placeholder). Qué hace:

- **Grid de tarjetas** con portada (o inicial del nombre si no hay imagen), nombre y origen.
- **Buscar** por nombre en tiempo real.
- **Filtros** (chips): Todos / Favoritos / Steam / Epic / Manuales / Ocultos.
- **Ordenar**: Nombre, Último jugado, Más jugado, Favoritos primero.
- **Añadir manualmente**: nombre, ejecutable, argumentos, portada (diálogo propio).
- **Editar / Favorito / Ocultar / Eliminar**: clic derecho sobre la tarjeta.
  Eliminar solo quita la entrada del launcher — nunca borra el juego real.
- **Doble clic** en una tarjeta lanza el juego.
- Persistencia en `%AppData%\Launcher\games.json`.

### Arquitectura (patrón GameProvider)

`Game` no sabe de dónde viene. Cada fuente implementa `IGameProvider`
(`Core/Interfaces/IGameProvider.cs`) con un método `DetectGamesAsync()`:

```
Modules/Games/Providers/
├── SteamGameProvider.cs   # TODO: leer libraryfolders.vdf + appmanifest_*.acf
└── EpicGameProvider.cs    # TODO: leer manifiestos *.item de Epic
```

Ahora mismo ambos devuelven listas vacías a propósito — la detección real necesita
leer el registro/discos de Windows, así que hay que probarla en tu máquina. El botón
"Sincronizar" de la vista ya llama a `GameLibraryService.SyncAsync(...)`, que fusiona
lo detectado sin pisar ediciones manuales (hace match por `Source` + `ExternalId`).
Cuando implementes el provider de verdad, solo tienes que rellenar `DetectGamesAsync()`
— el resto (fusión, UI, persistencia) ya funciona.

### Sincronizacion con Steam / Epic (por carpetas, no por cuenta)

Login con OAuth de cada plataforma es mucho mas complejo (hay que registrar la app
en sus developer portals) y no hacia falta: los dos launchers dejan en disco todo
lo necesario para detectar los juegos instalados.

- Boton **engranaje** (junto a Sincronizar): abre un dialogo para indicar la carpeta
  de instalacion de Steam y/o la carpeta de manifiestos de Epic. Tiene un boton
  "Auto-detectar" que prueba el registro de Windows / rutas por defecto; si falla,
  se indican a mano con el selector de carpetas.
- Boton **Sincronizar**: si no hay ninguna carpeta configurada, abre ese mismo
  dialogo antes de sincronizar. Luego fusiona lo detectado con tu biblioteca sin
  tocar tus ediciones manuales (hace match por `Source` + `ExternalId`).
- **Steam**: lee `steamapps/libraryfolders.vdf` (para encontrar bibliotecas en
  varios discos) y los `appmanifest_<appid>.acf` de cada una (nombre + appid).
  Lanza con el protocolo `steam://rungameid/{appid}` en vez de adivinar el .exe.
- **Epic**: lee los manifiestos `.item` (JSON) en la carpeta de Epic. Lanza con
  `com.epicgames.launcher://apps/{AppName}?action=launch`.

Esta logica sigue el formato conocido/documentado de esos archivos, pero no ha
podido probarse contra una instalacion real de Steam/Epic desde aqui. Si algo no
detecta bien al probarlo en tu maquina, dime el error (o pega el contenido de un
`.acf` / `.item` de ejemplo) y lo ajustamos.

### Portada automatica

En el dialogo de anadir/editar hay un boton "Buscar portada automaticamente":
busca el nombre en la Steam Store (API publica, sin necesitar clave) y descarga
la carpeta vertical de la ficha a `%AppData%\Launcher\covers`. Funciona incluso
para juegos que no son de Steam, mientras el nombre coincida con algo publicado
ahi. Los juegos sincronizados desde Steam ya traen la portada automaticamente
(usa el appid directo, sin tener que buscar por nombre); los de Epic la buscan
por nombre igual que el boton manual.

Requiere conexion a internet cuando se ejecuta la app (el sandbox donde yo
trabajo no tiene red, asi que esto tampoco se ha podido probar en vivo).

## Próximos pasos sugeridos

1. Elegir el primer módulo real a implementar (Juegos es el más grande; Carpetas
   el más sencillo para probar el patrón de extremo a extremo).
2. Definir los modelos de datos (`Game`, `FolderShortcut`, etc.) y cómo se
   persisten (JSON local, SQLite...).
3. Decidir el patrón de `GameProvider` (Steam/Epic/Manual) antes de tocar la UI
   de Juegos, para no acoplar la vista a una fuente concreta.

## Notas / limitaciones conocidas de este esqueleto

- Con `AllowsTransparency="True"` (necesario para las esquinas redondeadas), el
  grip de resize nativo de WPF no se ve, aunque `ResizeMode` sigue permitiendo
  redimensionar arrastrando los bordes. Si molesta, se puede sustituir por
  `WindowChrome` + `DwmSetWindowAttribute` (Windows 11) para bordes redondeados
  nativos sin perder el resize grip — lo dejamos para cuando pulamos la UI.
- Los iconos usan **Segoe MDL2 Assets** (disponible en Windows 10 y 11). Si en
  algún momento migráis a Fluent Icons habría que cambiar los glifos.
