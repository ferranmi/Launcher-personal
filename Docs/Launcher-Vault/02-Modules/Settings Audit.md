---
title: Settings Audit
type: architecture-audit
project: Launcher
source_of_truth: feature/settings-centralized-management
last_reviewed: 2026-11-12
status: static-audit-complete-implementation-unverified
tags: [launcher, audit, settings, modules, persistence]
---

# Auditoria de configuracion por modulo

## Alcance y criterio

Auditoria estatica de la rama feature/settings-centralized-management. El registro de modulos se ha contrastado con MainWindow.xaml.cs y las fuentes de persistencia con sus servicios consumidores.

Criterio acordado con el usuario:

> Si lo que se gestiona es global o afecta a todo el modulo sin ser preferencia del usuario, se administra desde Settings.

Aplicacion del criterio:

- **Configuracion:** parametros que definen el funcionamiento general de Launcher o de un modulo.
- **Preferencia de usuario:** opciones de presentacion/uso que el usuario elige para su propia vista. Pueden permanecer en el modulo si no controlan su funcionamiento global.
- **Contenido:** accesos, bibliotecas, rutas y elementos que el usuario crea, importa, edita u organiza como material de trabajo.
- **Estado operativo:** datos que el modulo mantiene para continuar una operacion, evitar duplicados o recordar elementos procesados.
- **Accion:** operacion puntual como sincronizar, reparar, probar o actualizar. No es un parametro y no debe convertirse en un borrador de configuracion.

No se ha inferido una funcion solo por el nombre de un archivo. Se ha comprobado quien lo lee/escribe y si la vista activa lo consume.

## Inventario de los 13 modulos registrados

| Modulo | Configuracion identificada | Datos / estado que permanece en el modulo | Resultado |
|---|---|---|---|
| Dashboard | Umbrales, intervalo de estado, efectos, secuencia de arranque, eventos de Windows y layouts | Accesos mostrados desde Commands y Folders; estado del dashboard en ejecucion | Configuracion en Settings |
| Games | CardSize, SyncOnStartup, ConfirmDelete; rutas Steam/Epic y cuenta Steam | games.json e ignored-games.json | Configuracion en Settings; biblioteca y exclusiones permanecen en Games |
| Folders | No se identifica un parametro general editable del modulo | folders.json: accesos, nombres, rutas, iconos, integrados y ocultos | Contenido del usuario |
| Media | Intervalo de lectura del reproductor y mezclador | Sesiones de Windows y estado de audio en tiempo real | Intervalo en Settings; sesiones y volumen son estado del sistema |
| Music | MusicFolder | Metadatos dentro de los propios archivos de audio | Carpeta en Settings; biblioteca basada en etiquetas de audio |
| Network | Intervalo de consulta de Tailscale | Dispositivos detectados y ficheros preparados para envio, en memoria | Intervalo en Settings; estado de red no se persiste |
| ImageGen | URL, instalacion A1111, script, autoarranque, wildcards y carpetas | LastParams y rutas que informa A1111; biblioteca de imagenes | Configuracion en Settings; estado de trabajo sigue en ImageGen |
| Models | Instalacion, carpetas, concurrencia, limite, reintentos y categorias | IgnoredReferences, PendingCorrections; preferencias LibraryView/LibrarySort | Configuracion en Settings; estado operativo y preferencias locales siguen en Models |
| Commands | No se identifica politica general del modulo editable por el usuario | commands.json: accesos, argumentos, terminal, elevacion, perfil web, favoritos, iconos y credenciales referenciadas | Contenido del usuario |
| Devices | Rutas personalizadas de aplicaciones por fabricante | Datos WMI/WinRT detectados en tiempo real | Rutas trasladadas a Settings |
| Monitor | Intervalo compartido y override de Monitor | Historial de muestras y datos actuales | Configuracion en Settings |
| Processes | Intervalo compartido y override de Processes; ConfirmKillProcess | Procesos, grupos, busqueda y orden de la vista | Configuracion en Settings; datos de proceso son runtime |
| Settings | Preferencias globales y configuracion de modulos | No mantiene biblioteca de contenido propia | Centro de configuracion |

## Detalle por modulo

### 1. Dashboard

**Hechos del codigo**

- MainWindow registra DashboardModule.
- DashboardView consume servicios de sistema, procesos, Uplink y NowPlaying.
- DashboardView carga accesos desde CommandsService y FolderShortcutsService.
- SystemStatusHost consume ThresholdProfile, StatusIntervalSeconds y WindowsEventLog.
- El layout de la interfaz se persiste mediante AppSettingsService.Layouts; MainWindow usa la clave sidebar para el ancho del menu.

**Clasificacion**

- Umbrales, frecuencia de muestreo, efectos, secuencia de arranque y eventos: configuracion.
- Accesos que Dashboard muestra: contenido de Commands/Folders, no propiedad de Dashboard.
- Layouts, ultima vista, bounds de ventana y sidebar: estado/preferencias de interfaz.

**Decision**

La configuracion de Dashboard permanece en Settings. Los accesos no se duplican ni se administran desde Dashboard.

### 2. Games

**Hechos del codigo**

- AppSettingsService contiene CardSize, SyncOnStartup y ConfirmDelete.
- LibraryPathsService persiste SteamPath, EpicManifestsPath, SteamApiKey y SteamId en settings.json.
- GameLibraryService persiste la biblioteca en games.json.
- GameLibraryService persiste en ignored-games.json las claves de juegos de Steam/Epic eliminados para que no reaparezcan al sincronizar.
- La sincronizacion manual es una accion, no una preferencia.

**Clasificacion**

- Tamano de tarjeta, sincronizacion al abrir y confirmacion de borrado: configuracion.
- Rutas Steam/Epic y cuenta Steam: configuracion de Games.
- games.json: contenido/biblioteca del usuario.
- ignored-games.json: estado operativo de sincronizacion.
- Reparar portadas y restaurar ignorados: acciones.

**Decision aplicada**

Los botones de rutas en Games abren Settings > Juegos. Si el usuario intenta sincronizar sin rutas configuradas, se redirige a Settings en vez de abrir un editor paralelo. Settings conserva los dialogos como editores de borrador.

**Riesgo**

SteamApiKey sigue almacenada en texto JSON en settings.json. No se migra en esta tarea.

### 3. Folders

**Hechos del codigo**

- FolderShortcutsService persiste folders.json.
- Si el archivo no existe, siembra accesos habituales de Windows.
- Cada acceso contiene Name, Path, Icon, IsBuiltIn y Hidden.
- El modulo permite anadir, editar, ocultar y eliminar accesos no integrados.
- No se identifica un parametro global de funcionamiento configurable.

**Clasificacion**

folders.json es contenido del usuario. Hidden es parte del estado del acceso, no una preferencia global. Las dimensiones del panel del explorador son valores de vista/runtime.

**Decision**

No trasladar folders.json a Settings. No centralizar la gestion de accesos que el usuario organiza dentro de Folders.

### 4. Media / Multimedia

**Hechos del codigo**

- MediaModule crea MediaView.
- MediaView usa NowPlayingService para sesiones multimedia de Windows y AudioMixerService para el mezclador CoreAudio.
- El ciclo de actualizacion era una constante de 1 segundo.
- MediaAppsService y MediaAppShortcut existen y persisten media_apps.json, pero MediaView no los consume y no hay una interfaz conectada en el modulo activo.

**Clasificacion**

- Frecuencia de actualizacion: parametro de funcionamiento del modulo.
- Sesiones, volumen y estado de reproduccion: estado del sistema en tiempo real.
- media_apps.json: seria contenido del usuario si se reactivase la funcion de accesos multimedia; hoy es persistencia dormida.

**Decision aplicada**

MediaRefreshSeconds se incorpora a config.json y Settings > Multimedia. Opciones: 1, 2 y 5 segundos. MediaView lee el valor y actualiza su DispatcherTimer al guardar.

No migrar ni borrar media_apps.json. Antes de reactivar esa funcionalidad, decidir si se conecta la vista o se retira el servicio/modelo.

### 5. Music

**Hechos del codigo**

- MusicFolder se guarda en AppSettingsService.
- MusicLibraryService enumera los archivos de la carpeta configurada.
- Los metadatos se leen/escriben en etiquetas del propio archivo mediante TagLib; no existe un catalogo JSON.
- La vista ofrece filtros y orden local.

**Clasificacion**

- Carpeta raiz de biblioteca: configuracion del modulo.
- Metadatos de canciones: contenido dentro de archivos del usuario.
- Filtros/orden de la vista: preferencia local.

**Decision**

MusicFolder se mantiene en Settings. La biblioteca no se duplica en un archivo de Launcher.

### 6. Network / Red

**Hechos del codigo**

- NetworkView usa TailscaleService y FileTransferService.
- TailscaleService localiza tailscale.exe en Program Files o PATH; no guarda credenciales ni perfiles.
- NetworkView consultaba cada 10 segundos.
- La lista de ficheros preparada para enviar vive en memoria durante la sesion.

**Clasificacion**

- Intervalo de consulta: parametro general de funcionamiento del modulo.
- Estado de Tailscale, dispositivos y preparacion de envio: runtime.
- Enviar ficheros: accion.

**Decision aplicada**

NetworkRefreshSeconds se incorpora a config.json y Settings > Red. Opciones: 5, 10 y 30 segundos. NetworkView actualiza su DispatcherTimer al guardar.

### 7. ImageGen

**Hechos del codigo**

ImageGenSettings persiste imagegen.json con:

- Url.
- InstallPath y ServerBatPath.
- AutoStartServer.
- WildcardsFolder y CustomFolders.
- Rutas que A1111 informa: CkptDir, LoraDir, VaeDir, EmbeddingsDir y ControlNetDir.
- LastParams.

La vista guarda LastParams al generar o salir. ApplyServerFlags conserva las rutas detectadas por A1111. La modificacion de webui-user.bat para anadir --api es una accion con confirmacion y copia de respaldo.

**Clasificacion**

- URL, instalacion, script, autoarranque, wildcards y carpetas: configuracion.
- LastParams y rutas detectadas: estado de trabajo/runtime, aunque se persistan en el mismo archivo.
- Modificar webui-user.bat: accion explicita, no parametro JSON.

**Decision aplicada**

El boton Instalacion y carpetas redirige a Settings > Imagenes IA. Si no hay script valido, Iniciar servidor tambien redirige a Settings. Se elimina la seleccion/guardado directo del script desde la vista del modulo.

### 8. Models

**Hechos del codigo**

ModelsSettings persiste models.json con:

- InstallPath.
- LoraFolders, CheckpointFolders, LyCorisFolders y WildcardFolders.
- MaxConcurrentDownloads, AutoConcurrency, SpeedLimitKBps y DownloadRetries.
- Categories.
- IgnoredReferences y PendingCorrections.
- LibraryView y LibrarySort.

CivitaiService guarda la API Key en Credential Manager.

**Clasificacion**

- Rutas, carpetas, parametros de descarga y categorias: configuracion.
- IgnoredReferences y PendingCorrections: estado operativo del trabajo.
- LibraryView y LibrarySort: preferencias locales de presentacion.
- La instalacion, escaneo y descarga son acciones.

**Decision**

Settings administra la configuracion. La pestana Ajustes de Models redirige a Settings > Modelos. El panel XAML antiguo permanece oculto y contiene handlers de guardado antiguos; se documenta como deuda tecnica para retirar tras comprobar referencias. LibraryView/LibrarySort permanecen locales.

### 9. Commands

**Hechos del codigo**

CommandsService persiste commands.json. Cada CommandShortcut puede contener destino, argumentos, directorio de trabajo, terminal, elevacion, perfil web, incognito, favorito, iconos y referencias a credenciales.

**Clasificacion**

Son propiedades de cada acceso directo que el usuario crea y mantiene. No se identifica una politica general de ejecucion editable desde el modulo.

**Decision**

commands.json permanece como contenido del usuario. Una futura politica global de ejecucion/seguridad, si se introduce, debe vivir en Settings y no mezclarse con accesos individuales.

### 10. Devices

**Hechos del codigo**

- DeviceService detecta hardware mediante WMI y Bluetooth/WinRT.
- Conocia rutas habituales de aplicaciones de fabricantes.
- Las rutas elegidas manualmente se persistian en device_apps.json.
- Las rutas afectan a la resolucion de la aplicacion para todos los dispositivos de un fabricante.

**Clasificacion**

La asignacion de ejecutable es configuracion funcional del modulo, no una biblioteca de accesos que el usuario organiza.

**Decision aplicada**

- DeviceVendorApps se incorpora a AppSettingsService/config.json.
- AppSettingsService importa device_apps.json una sola vez mediante LegacyDeviceAppsMigrated.
- El archivo legado se conserva; no se borra.
- DeviceService consume la configuracion central.
- Si falta la ruta personalizada, la tarjeta de Devices abre Settings > Dispositivos.
- Si existe un ejecutable valido, la tarjeta lo abre.

### 11. Monitor

**Hechos del codigo**

MonitorView consume GetMonitorRefreshSeconds. Null hereda RefreshSeconds; 0 significa Auto; un valor positivo fija el intervalo del modulo.

**Clasificacion**

Configuracion compartida/especifica.

**Decision**

Se mantiene el modelo COMPARTIDO + override, con etiquetas de herencia/sobrescritura en Settings.

### 12. Processes

**Hechos del codigo**

ProcessesView consume GetProcessesRefreshSeconds. ConfirmKillProcess se consulta antes de finalizar procesos/grupos.

**Clasificacion**

Intervalo y confirmacion: configuracion. Lista, grupos, busqueda, orden y expansion: estado de la vista/procesos.

**Decision**

Se mantiene el modelo COMPARTIDO + override y ConfirmKillProcess en Settings.

### 13. Settings

Settings administra los parametros globales y de funcionamiento de los modulos, pero no absorbe contenido, bibliotecas ni acciones.

Fuentes actuales: config.json, settings.json, imagegen.json, models.json, registro Run y Credential Manager. device_apps.json es una fuente de migracion heredada.

## Inventario de persistencia

| Archivo / destino | Propietario | Clasificacion |
|---|---|---|
| config.json | AppSettingsService | Configuracion general y de modulos |
| settings.json | LibraryPathsService | Configuracion de bibliotecas/cuenta Steam |
| imagegen.json | ImageGenSettings | Configuracion + estado runtime de ImageGen |
| models.json | ModelsSettings | Configuracion + estado operativo + preferencias locales |
| device_apps.json | DeviceService anterior | Legado; importacion unica y respaldo |
| games.json | GameLibraryService | Biblioteca/contenido |
| ignored-games.json | GameLibraryService | Estado operativo |
| folders.json | FolderShortcutsService | Accesos/contenido |
| commands.json | CommandsService | Accesos/contenido |
| media_apps.json | MediaAppsService | Persistencia dormida, sin vista consumidora |
| etiquetas de audio | MusicLibraryService / TagLib | Metadatos dentro del contenido del usuario |
| Registro HKCU Run | StartupService | Configuracion de arranque |
| Credential Manager | CivitaiService | Credencial Civitai |

## Cambios implementados en esta revision

1. DeviceVendorApps se centraliza en AppSettingsService.
2. Importacion de device_apps.json una sola vez; no se elimina el archivo original.
3. Settings > Dispositivos permite editar rutas de fabricantes.
4. Devices redirige a Settings cuando falta una aplicacion; no escribe JSON desde el modulo.
5. Games redirige a Settings para las rutas Steam/Epic; sincronizar sin rutas no abre un editor paralelo.
6. ImageGen redirige a Settings para instalacion/script y ya no guarda ServerBatPath desde el boton de arranque.
7. MediaRefreshSeconds configurable en Settings: 1/2/5 s.
8. NetworkRefreshSeconds configurable en Settings: 5/10/30 s.
9. MediaView y NetworkView consumen sus intervalos desde AppSettingsService.
10. Reset incluye ConfirmKillProcess, MusicFolder, DeviceVendorApps, intervalos Media/Red y LastModule. El reset desde la UI marca la migracion heredada como completada para que las rutas antiguas no reaparezcan.
11. Version incrementada a 0.1.55 y README/Obsidian actualizados.

## Riesgos y trabajo pendiente

| Prioridad | Hallazgo | Tratamiento |
|---|---|---|
| Alta | SteamApiKey en texto JSON | Migracion a Credential Manager requiere decision/permiso separado |
| Alta | Guardado multiarchivo no atomico como conjunto | Mantener rollback compensatorio; probar fallos parciales |
| Media | MediaAppsService/media_apps.json sin consumidor activo | No borrar; decidir reactivar o retirar en otra tarea |
| Media | Panel de ajustes antiguo de Models oculto con handlers direct-save | Retirar tras verificar referencias y probar la vista |
| Media | Estado runtime de ImageGen comparte imagegen.json con preferencias | Mantener separacion conceptual y evitar sobrescribir valores activos |

## Verificacion

- Revision estatica de codigo y referencias.
- No se ha ejecutado dotnet build, pruebas automatizadas ni WPF en Windows.
- No afirmar que compila o funciona hasta ejecutar en un entorno Windows con .NET 10 SDK.

### Pruebas manuales necesarias

1. Arrancar con config.json ausente, antiguo, incompleto y corrupto.
2. Migrar device_apps.json con rutas validas, vacias, duplicadas y corruptas.
3. Comprobar que Guardar persiste DeviceVendorApps y que Descartar no modifica config.json.
4. Comprobar que Restablecer + Guardar limpia DeviceVendorApps y no vuelve a importar el archivo legado en el siguiente arranque.
5. Cambiar intervalos Media y Red; comprobar que sus timers usan los valores nuevos.
6. Salir de Settings con cambios pendientes: probar Guardar, Descartar y Cancelar.
7. Probar guardado con permisos insuficientes o archivo bloqueado; comprobar que los cambios siguen pendientes.
8. Verificar rutas Steam/Epic y script A1111 solo desde Settings.
9. Ejecutar dotnet build en Windows y corregir cualquier error XAML/C# antes de dar por cerrada la implementacion.
