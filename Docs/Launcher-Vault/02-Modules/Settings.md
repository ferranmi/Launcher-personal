---
title: Settings
type: module-reference
project: Launcher
source_of_truth: feature/settings-centralized-management
last_reviewed: 2026-11-13
status: implemented-unverified
tags: [launcher, module, settings, configuration]
---

# Settings

## Responsabilidad

Settings es el centro de administracion de la configuracion de Launcher y de los parametros que afectan al funcionamiento general de sus modulos.

Los modulos consumen los valores confirmados. Las rutas, bibliotecas, accesos directos, elementos ignorados y otros datos de trabajo siguen perteneciendo al modulo que los utiliza.

## Navegacion

La vista usa una unica pagina vertical con scroll continuo y una barra lateral de categorias. La categoria activa se actualiza al desplazarse.

Secciones actuales:

1. Arranque
2. Ventana
3. Monitor y procesos
4. Fondo
5. Juegos
6. Dispositivos
7. Multimedia
8. Musica
9. Red
10. Imagenes IA
11. Modelos
12. Actualizaciones
13. Dashboard
14. Datos
15. Acerca de

Los modulos pueden abrir una categoria concreta mediante SettingsView.OpenSection(sectionId).

## Sesion de edicion

- Al abrir Settings se crean copias de trabajo de los valores actuales.
- Los controles modifican las copias, no los servicios activos.
- Guardar valida los campos y escribe primero los borradores en sus destinos, sin modificar los servicios activos durante la persistencia.
- Solo cuando terminan los archivos y el registro, publica los valores en memoria y notifica a los modulos.
- Si falla un destino, intenta restaurar todos los destinos anteriores y conserva los cambios pendientes; informa si la restauracion tampoco se completa.
- Descartar vuelve a copiar los valores activos.
- Navegar entre categorias no guarda.
- Al salir de Settings o cerrar Launcher con cambios pendientes se pregunta si guardar, descartar o seguir editando.
- Los dialogos de Games e ImageGen reciben copias de trabajo cuando se abren desde Settings.
- Los cambios del registro Run y la credencial Civitai se aplican dentro del proceso de guardado.

## Fuentes de configuracion y persistencia

| Fuente | Destino | Responsabilidad |
|---|---|---|
| AppSettingsService | %AppData%/Launcher/config.json | Configuracion general, preferencias de modulos e intervalos centralizados |
| LibraryPathsService | %AppData%/Launcher/settings.json | Rutas de bibliotecas Steam/Epic y datos de cuenta Steam |
| ImageGenSettings | %AppData%/Launcher/imagegen.json | Configuracion de AUTOMATIC1111 y estado de generacion |
| ModelsSettings | %AppData%/Launcher/models.json | Rutas, carpetas, descargas y estado operativo de Models |
| StartupService | Registro HKCU Run | Inicio automatico con Windows |
| CivitaiService | Administrador de credenciales de Windows | API Key de Civitai |
| device_apps.json | Archivo legado | Se importa una sola vez a config.json; se conserva como respaldo |

Los archivos JSON se escriben a un temporal y se reemplazan individualmente. Los objetos de borrador se persisten antes de publicar los valores en memoria. El guardado coordinado incluye rollback compensatorio si se detecta un error, pero no es una transaccion atomica entre varios archivos y el registro ante un corte de energia.

## Parametros por modulo

### Globales y compartidos

- Arranque: iniciar minimizado y modulo inicial.
- Ventana: siempre encima, cerrar a la bandeja, recordar tamano/posicion y atajo global.
- El ancho del sidebar y los layouts se conservan como preferencias de interfaz/runtime; no se editan desde Settings.
- Tema: color de acento.
- Fondo: modo, imagen e intensidad.
- Monitor y Procesos: RefreshSeconds compartido, con overrides opcionales por modulo.
- Actualizaciones: origen, comprobacion al iniciar y aplicacion automatica.

Los parametros compartidos y sus sobrescrituras muestran etiquetas que explican la herencia o el alcance.

### Dashboard

- ThresholdProfile.
- StatusIntervalSeconds.
- DashboardEffects.
- BootSequence.
- WindowsEventLog.

El muestreo del sistema lo coordina SystemStatusHost. El Dashboard consume su estado y muestra accesos de Commands y Folders.

### Games

- CardSize.
- SyncOnStartup.
- ConfirmDelete.
- Rutas Steam/Epic, SteamID y SteamApiKey mediante dialogos que editan la copia de trabajo.

La biblioteca de juegos no es configuracion: permanece en games.json. ignored-games.json conserva las claves de juegos Steam/Epic eliminados para que no reaparezcan al sincronizar.

Los botones de rutas en Games abren Settings > Juegos. Si se intenta sincronizar sin rutas configuradas, tambien se redirige a esa seccion.

### Devices

DeviceVendorApps contiene una ruta personalizada por fabricante (Logitech, Razer, Corsair, SteelSeries, NVIDIA e HyperX).

Si una ruta esta vacia o no existe, DeviceService intenta las ubicaciones habituales conocidas. El boton de una tarjeta abre la aplicacion si se encuentra; si falta, lleva a Settings > Dispositivos.

Migracion: al iniciar, AppSettingsService importa las asignaciones de device_apps.json una sola vez. La importacion se marca en config.json. El archivo antiguo no se borra. Restablecer preferencias limpia las asignaciones y marca la migracion como completada para impedir que reaparezcan.

### Multimedia

MediaRefreshSeconds controla el ciclo de actualizacion del estado de reproduccion y del mezclador de volumen. Valores permitidos: 1, 2 o 5 segundos. Valor por defecto: 1 segundo.

MediaView usa NowPlayingService y AudioMixerService. No utiliza MediaAppsService.

### Music

MusicFolder define la carpeta de la biblioteca. Los metadatos musicales se leen y escriben en las etiquetas de los propios archivos de audio; no hay un catalogo JSON de canciones.

### Red

NetworkRefreshSeconds controla la consulta del estado de Tailscale. Valores permitidos: 5, 10 o 30 segundos. Valor por defecto: 10 segundos.

La lista de dispositivos y la preparacion temporal de ficheros no se persisten como configuracion.

### ImageGen

Settings administra URL, instalacion A1111, ruta del script, autoarranque, wildcards y carpetas personalizadas.

Los directorios que informa A1111 y LastParams son estado/runtime que el modulo conserva para poder recuperar la configuracion de trabajo. El modulo ya no ofrece un editor paralelo: Instalacion y carpetas abre Settings > Imagenes IA. Si no hay un script configurado, Iniciar servidor tambien redirige a Settings.

La modificacion de webui-user.bat para anadir --api sigue siendo una accion inmediata, con confirmacion y copia .bak.

### Models

Settings administra instalacion, carpetas LoRA/Checkpoint/LyCORIS/Wildcards, concurrencia, limite de velocidad, reintentos, categorias y credencial Civitai.

IgnoredReferences y PendingCorrections son estado operativo. LibraryView y LibrarySort son preferencias de presentacion de la biblioteca y permanecen en el modulo.

La pestana Ajustes de Models redirige a Settings > Modelos.

### Monitor y Procesos

Ambos consumen RefreshSeconds. MonitorRefreshSecondsOverride y ProcessesRefreshSecondsOverride permiten heredar el valor compartido o sustituirlo localmente. ConfirmKillProcess controla la confirmacion antes de finalizar procesos.

## Datos que permanecen en los modulos

| Archivo | Contenido | Clasificacion |
|---|---|---|
| folders.json | Accesos directos a carpetas, incluidos los integrados y ocultos | Contenido del usuario |
| commands.json | Accesos directos, argumentos, terminal, iconos y opciones por comando | Contenido del usuario |
| games.json | Biblioteca, favoritos, ocultos, tiempo jugado y rutas | Contenido del usuario |
| ignored-games.json | Juegos de tiendas eliminados para evitar su reaparicion | Estado operativo |
| media_apps.json | Accesos a aplicaciones multimedia | Persistencia dormida: servicio/modelo existentes sin conexion con MediaView |

media_apps.json no se migra ni se elimina. Si se reactiva la funcion de accesos multimedia, debe seguir tratandose como contenido del modulo, no como preferencia global.

## Restablecer preferencias

El boton Restablecer prepara valores por defecto de config.json y desactiva el inicio con Windows. Los cambios no se aplican hasta Guardar.

No modifica settings.json, imagegen.json, models.json ni la credencial de Civitai. La asignacion antigua device_apps.json no se elimina; la marca de migracion evita que las rutas antiguas se vuelvan a importar.

## Riesgos y limitaciones conocidas

1. SteamApiKey sigue almacenada en texto JSON en settings.json. Migrarla a Credential Manager requiere una tarea de seguridad separada.
2. El guardado entre varios destinos no es atomico como conjunto.
3. MediaAppsService y MediaAppShortcut mantienen media_apps.json, pero no existe una interfaz conectada a ellos en el modulo Multimedia activo.
4. El panel de ajustes antiguo de Models permanece en XAML como contenido oculto; la pestana Ajustes redirige a Settings. Conviene retirar ese codigo muerto en una limpieza posterior, tras verificar referencias.
5. La configuracion de ImageGen conserva LastParams y directorios detectados por el servidor en imagegen.json; no son preferencias puras, sino estado de trabajo.
6. No se ha ejecutado dotnet build ni pruebas WPF en Windows. Esta rama no se considera validada hasta completar las comprobaciones.

## Archivos principales

- Modules/Settings/SettingsView.xaml
- Modules/Settings/SettingsView.xaml.cs
- Core/Services/AppSettingsService.cs
- Core/Services/LibraryPathsService.cs
- Core/Services/DeviceService.cs
- Modules/Devices/DevicesView.xaml.cs
- Modules/Media/MediaView.xaml.cs
- Modules/Network/NetworkView.xaml.cs
- Modules/Games/GamesView.xaml.cs
- Modules/ImageGen/ImageGenView.xaml.cs
- Modules/Models/ModelsView.xaml.cs
- MainWindow.xaml.cs
- Launcher.csproj
- README.md
