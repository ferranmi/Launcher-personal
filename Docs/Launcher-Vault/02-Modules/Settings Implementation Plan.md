---
title: Settings Implementation Plan
type: implementation-plan
project: Launcher
source_of_truth: feature/settings-centralized-management
last_reviewed: 2026-11-13
status: implemented-unverified
tags: [launcher, settings, implementation, audit]
---

# Settings — Plan de implementacion

## Objetivo

Settings es el centro unico de administracion de Launcher y de los parametros que afectan al funcionamiento general de sus modulos. Los modulos consumen la configuracion confirmada; el contenido y el estado operativo permanecen en su propietario.

Referencias:

- [[Settings]] — comportamiento y estructura de la vista.
- [[Settings Audit]] — inventario de los 13 modulos, persistencia y decisiones de clasificacion.

## Modelo acordado

Cada parametro se clasifica como:

- **Global:** una fuente de verdad para Launcher.
- **Compartido:** una fuente de verdad utilizada por varios modulos.
- **Heredado:** el modulo utiliza el valor compartido mientras no tenga una sobrescritura.
- **Especifico:** pertenece a un modulo y no necesita etiqueta visual.
- **Sobrescritura:** valor especifico que sustituye el compartido para ese modulo.

Los parametros compartidos y sus sobrescrituras explican el alcance mediante etiquetas visibles.

No se trasladan a Settings bibliotecas, accesos directos, colecciones, archivos de trabajo ni estado operativo solo por estar guardados en JSON.

## Estructura visual implementada

- Sidebar interno fijo con categorias.
- Panel de contenido con scroll vertical continuo.
- Saltos a secciones dentro de la misma pagina.
- Categoria activa segun la posicion del scroll.
- Estado de cambios pendientes.
- Botones Guardar y Descartar en la sidebar.
- Secciones para Arranque, Ventana, Monitor/Procesos, Fondo, Games, Devices, Media, Music, Network, ImageGen, Models, Actualizaciones, Dashboard, Datos y Acerca de.

## Sesion de edicion implementada

1. Al abrir Settings se crean copias de trabajo.
2. Los controles modifican los borradores.
3. Cambiar de categoria no persiste cambios.
4. Guardar valida y escribe primero los borradores en disco sin modificar los servicios activos; solo despues publica los valores en memoria y notifica a los modulos.
5. Si una escritura falla, se intenta restaurar todos los destinos anteriores; se informa si el rollback no se completa y se conservan los cambios pendientes.
6. Descartar recarga los valores activos.
7. Al abandonar Settings o cerrar Launcher se pregunta si guardar, descartar o seguir editando.
8. El registro Run y la credencial Civitai se aplican dentro del proceso de guardado.

La persistencia temporal/reemplazo es individual por archivo. El rollback es compensatorio: no ofrece atomicidad de conjunto frente a un corte de energia. Los borradores se escriben antes de publicar los nuevos valores en memoria, para que los modulos sigan leyendo la configuracion confirmada durante la fase de persistencia.

## Integraciones implementadas

### Fuentes principales

- AppSettingsService → config.json.
- LibraryPathsService → settings.json.
- ImageGenSettings → imagegen.json.
- ModelsSettings → models.json.
- StartupService → registro Run.
- CivitaiService → Credential Manager.
- DeviceService → ahora consume DeviceVendorApps desde AppSettingsService.

### Parametros compartidos

- RefreshSeconds es el intervalo compartido de Monitor y Processes.
- MonitorRefreshSecondsOverride y ProcessesRefreshSecondsOverride permiten heredar o sobrescribir.
- MediaRefreshSeconds controla Multimedia (1/2/5 s).
- NetworkRefreshSeconds controla Red/Tailscale (5/10/30 s).
- StatusIntervalSeconds controla la frecuencia de SystemStatusHost cuando Dashboard esta visible.

### Redireccion de ajustes desde modulos

- Games: las rutas Steam/Epic abren Settings > Juegos. La sincronizacion sin rutas configuradas redirige a Settings.
- Devices: las rutas de aplicaciones de fabricantes se administran en Settings > Dispositivos.
- ImageGen: Instalacion y carpetas abre Settings > Imagenes IA. Iniciar servidor redirige alli si falta el script.
- Music: el acceso de ajustes abre Settings > Musica.
- Models: la pestana Ajustes abre Settings > Modelos.

Los dialogos de Games e ImageGen abiertos desde Settings editan copias de trabajo.

## Migracion de rutas de fabricantes

Antes, DeviceService escribia las asignaciones manuales en device_apps.json directamente desde Devices.

Cambio aplicado:

1. DeviceVendorApps pasa a AppSettingsService/config.json.
2. AppSettingsService importa device_apps.json una sola vez.
3. LegacyDeviceAppsMigrated impide volver a importar el archivo antiguo.
4. device_apps.json no se borra y queda como respaldo.
5. El reset limpia las rutas y marca la migracion como completada; no recupera las rutas antiguas en el siguiente arranque.

No se realiza ninguna eliminacion automatica de datos.

## Restablecimiento

El reset desde Settings prepara los valores por defecto de config.json y desactiva el inicio con Windows, pero los cambios no se aplican hasta Guardar.

Se han incluido los campos que faltaban en el reset: ConfirmKillProcess, MusicFolder, DeviceVendorApps, MediaRefreshSeconds, NetworkRefreshSeconds y LastModule. El reset no modifica settings.json, imagegen.json, models.json ni la credencial Civitai.

## Estado por fases

### Fase 0 — Auditoria de los 13 modulos
**Estado:** completada estaticamente.

- Inventario de parametros y consumidores.
- Clasificacion de configuracion, preferencia, contenido, estado operativo y acciones.
- Identificacion de MediaAppsService/media_apps.json como persistencia existente sin consumidor en la vista activa.
- Identificacion de device_apps.json como configuracion de Devices que debia centralizarse.

Documento: [[Settings Audit]].

### Fase 1 — Sesion de edicion y persistencia
**Estado:** implementada en codigo; sin validar en Windows.

- Borradores para AppSettings, LibraryPaths, ImageGen y Models.
- Guardado coordinado de JSON, registro Run y credencial Civitai.
- Escritura inicial desde los objetos de borrador, sin mutar los servicios activos hasta completar los archivos y el registro.
- Comparacion y sincronizacion separada de estado runtime de ventana, navegacion y layouts para evitar sobrescribir cambios externos.
- Escritura temporal y reemplazo individual.
- Rollback compensatorio ante errores detectados.
- Notificacion de cambios despues del guardado.
- Guardar/Descartar y confirmaciones al navegar/cerrar.

### Fase 2 — Navegacion e interfaz
**Estado:** implementada en codigo; sin validacion visual.

- Sidebar de categorias.
- Scroll continuo y saltos a secciones.
- Categoria activa segun scroll.
- Indicador de cambios y botones Guardar/Descartar.
- Secciones nuevas para Devices, Media y Network.

### Fase 3 — Integracion por modulo
**Estado:** implementada parcialmente; auditoria estatica completada.

- Games, Devices, ImageGen, Music y Models redirigen su acceso de configuracion a Settings.
- Monitor/Processes consumen intervalo compartido y overrides.
- Media y Network consumen intervalos configurables.
- DeviceVendorApps se centraliza con importacion heredada.
- El panel XAML antiguo de ajustes de Models sigue oculto y conserva handlers de guardado antiguos; queda pendiente retirarlo tras verificar todas sus referencias.

### Fase 4 — Validacion
**Estado:** pendiente.

No se ha ejecutado dotnet build ni pruebas manuales WPF en Windows. No considerar terminada/validada la implementacion hasta:

1. Ejecutar dotnet build en Windows con .NET 10 SDK.
2. Corregir errores C#/XAML que aparezcan.
3. Probar config.json ausente, incompleto y corrupto.
4. Probar migracion de device_apps.json y reset sin reimportacion.
5. Probar Guardar/Descartar y cierre con cambios pendientes.
6. Probar fallo de escritura y rollback.
7. Probar intervalos de Media, Red, Monitor y Processes.
8. Probar rutas de Games y script de ImageGen desde Settings.
9. Probar en ejecucion que cada modulo aplica el cambio despues de guardar.

## Riesgos pendientes

| Prioridad | Riesgo | Accion |
|---|---|---|
| Alta | SteamApiKey sigue en texto JSON | Separar como tarea de seguridad; no migrar sin revisar compatibilidad |
| Alta | No hay atomicidad multiarchivo real | Mantener rollback y probar errores parciales |
| Media | media_apps.json sin vista consumidora | Conservar datos; decidir reactivar o retirar en otra tarea |
| Media | Panel de ajustes antiguo de Models oculto | Retirar despues de verificar referencias |
| Media | LastParams y rutas runtime de ImageGen comparten imagegen.json con preferencias | Preservar estado runtime al guardar configuracion |

## Version y rama

- Version: 0.1.56.
- Rama: feature/settings-centralized-management.
- main no se ha modificado.
- No se ha creado PR ni se ha fusionado.
- No se han anadido dependencias.
- El numero de commits ahead debe comprobarse antes de preparar squash/merge.
