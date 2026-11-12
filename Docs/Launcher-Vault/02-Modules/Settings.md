---
title: Settings
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_ref: main @ tree 2c81caa430d49221a2e8cd7d5981e0aea688c0ef
last_reviewed: 2026-11-12
status: static-audit
tags: [launcher, module, settings]
---

# Settings

## Responsabilidad actual

Centralizar la configuracion y las preferencias de Launcher.

La implementacion actual ya agrupa buena parte de los ajustes en una vista vertical, pero no representa todavia el modelo de administracion centralizada acordado: no existe navegacion lateral por categorias ni una sesion de edicion con Guardar/Descartar.

## Implementacion observada

### Modulo y navegacion

- SettingsModule implementa IModule.
- ID: settings.
- Nombre visible: Configuracion.
- Orden: 100.
- Se registra explicitamente en MainWindow.xaml.cs.
- SettingsView es una vista WPF con un unico ScrollViewer vertical.
- No existe menu lateral interno con saltos a secciones.

### Secciones actuales de SettingsView

1. Arranque.
2. Ventana.
3. Monitor y Procesos.
4. Fondo.
5. Juegos.
6. Imagenes IA.
7. Actualizaciones.
8. Dashboard.
9. Datos.
10. Acerca de.

### Comportamiento actual de edicion

- Los controles modifican directamente AppSettingsService.Instance.
- Los eventos Toggle_Changed, Chip_Checked y BackgroundOpacity_ValueChanged llaman a Save().
- Algunos cambios tambien aplican efectos inmediatamente: tema, ventana, bandeja y atajos.
- No existe copia temporal, contador de cambios pendientes ni operaciones Guardar/Descartar.
- Por tanto, la implementacion actual contradice el requisito de no guardar automaticamente.

## Fuentes de configuracion identificadas

| Fuente | Archivo persistido | Ambito observado | Notas |
|---|---|---|---|
| AppSettingsService | config.json | Launcher y preferencias consumidas por modulos | Incluye ventana, inicio, tema, Dashboard, Monitor/Procesos, Juegos, musica y actualizaciones |
| LibraryPathsService | settings.json | Juegos / bibliotecas | Rutas Steam/Epic y SteamApiKey/SteamId; la clave queda serializada en JSON |
| ImageGenSettings | imagegen.json | ImageGen | URL, instalacion A1111, autoarranque, wildcards, carpetas y parametros anteriores |
| ModelsSettings | models.json | Models | Carpetas, concurrencia, limite de velocidad, reintentos, categorias y preferencias de biblioteca |
| Registro de Windows | Clave Run del usuario | Inicio con Windows | Se administra mediante StartupService, no se guarda en JSON |

### Observaciones sobre parametros compartidos

- RefreshSeconds esta presentado en Settings como intervalo compartido por Monitor y Processes.
- ThresholdProfile, StatusIntervalSeconds, DashboardEffects, BootSequence y WindowsEventLog pertenecen a preferencias de Dashboard.
- CardSize, SyncOnStartup y ConfirmDelete son preferencias de Games.
- MusicFolder existe en AppSettingsService; su interfaz consumidora debe comprobarse antes de clasificar su propiedad definitiva.
- Que dos propiedades tengan nombres o valores parecidos no demuestra que deban compartir una misma fuente.

## Acciones existentes que no son simples controles

SettingsView contiene botones para configurar bibliotecas de Steam/Epic, configurar cuenta de Steam, restaurar juegos ignorados, reparar portadas, configurar ImageGen, comprobar/aplicar actualizaciones, abrir carpetas de datos y logs y restablecer preferencias.

Estas operaciones tienen efectos propios. No deben tratarse como valores de formulario sin decidir previamente si son acciones inmediatas o ediciones temporales.

## Hallazgos de auditoria

| ID | Hallazgo | Prioridad | Evidencia |
|---|---|---:|---|
| SET-001 | No hay sesion temporal de edicion | P1 | Los handlers escriben en el singleton y llaman a Save |
| SET-002 | No existe navegacion lateral por categorias | P2 | SettingsView solo contiene ScrollViewer + StackPanel |
| SET-003 | La configuracion esta repartida en varios JSON | P1 | config.json, settings.json, imagegen.json y models.json |
| SET-004 | El inicio con Windows se modifica directamente en el registro | P1 | StartupService.SetEnabled se invoca desde Toggle_Changed |
| SET-005 | Cambios visuales y operativos se aplican antes de guardar | P1 | ThemeService, ApplyToWindow, ApplyTray y ApplyHotkey se invocan durante la edicion |
| SET-006 | AppSettingsService notifica Changed aunque falle la escritura | P1 | Save captura la excepcion y dispara Changed despues del catch |
| SET-007 | Guardado JSON no atomico | P1 | File.WriteAllText directo en los servicios revisados |
| SET-008 | Ajustes propios de ImageGen y Models no estan integrados en el formulario central | P1 | Modelos y servicios de configuracion independientes |
| SET-009 | La clave de Steam se almacena junto a rutas en settings.json | P2 | LibraryPathsService serializa SteamApiKey |

## Riesgos y decisiones de implementacion

1. Guardar/Descartar entre varios archivos: la sesion temporal debe incluir las fuentes de configuracion que se vayan migrando. No se debe simular una transaccion si parte de los ajustes ya se han escrito en disco.
2. Efectos secundarios: cambios de tema, ventana, bandeja, atajos y registro deben aplicarse despues de validar y persistir los valores.
3. Fallos parciales: guardar varios JSON no es atomico como conjunto. Hace falta una estrategia de validacion, escritura temporal y recuperacion ante fallo.
4. Credenciales: no ampliar la exposicion de SteamApiKey. Su migracion a Credential Manager es un trabajo de seguridad separado.
5. Acciones operativas: reparar portadas, restaurar ignorados y aplicar actualizaciones no son ajustes ordinarios y no deben quedar pendientes como si fueran simples valores.

## Objetivo funcional acordado

- Una unica pagina vertical con scroll continuo.
- Menu lateral de categorias y subcategorias con saltos a las secciones.
- La categoria activa se refleja al desplazarse.
- Settings administra preferencias globales y de los modulos.
- Los modulos consumen su configuracion y ejecutan su funcionalidad; no duplican la interfaz de administracion.
- Un parametro individual no necesita etiqueta.
- Los parametros globales, compartidos o que sobrescriben un valor global muestran una advertencia visible que explica el alcance y los modulos afectados.
- Un valor individual puede heredar el global o sobrescribirlo cuando proceda.
- Los cambios se mantienen en una sesion temporal.
- Botones Guardar y Descartar en la barra lateral.
- Navegar entre categorias no guarda ni descarta.
- Al salir con cambios pendientes se solicita una decision.
- Los efectos sobre modulos solo se aplican tras guardar correctamente.

## Plan de implementacion propuesto

1. Completar el inventario de todos los parametros y sus consumidores.
2. Definir los ambitos global, compartido, heredado y especifico.
3. Implementar una sesion de edicion temporal sin efectos secundarios.
4. Implementar persistencia segura y resultado de guardado verificable.
5. Construir navegacion lateral y secciones ancladas.
6. Integrar progresivamente configuraciones de Games, ImageGen y Models.
7. Aplicar los cambios confirmados a los servicios y modulos afectados.
8. Probar guardar, descartar, fallo de escritura, cierre con cambios pendientes y herencia.
9. Actualizar version y README cuando cambie el comportamiento visible.

## Estado de verificacion

Auditoria estatica sobre main. No se ha ejecutado dotnet build, WPF ni pruebas manuales en Windows. El entorno de esta sesion no dispone de acceso de red para clonar el repositorio ni de un entorno Windows para validar WPF.