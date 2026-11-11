---
title: Core Audit
type: technical-audit
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: partial-static-review
tags: [launcher/core, launcher/audit]
---

# Auditoría de Core

## Estructura
Core contiene Interfaces, Models, Mvvm, Converters y Services.

## Interfaces
`IModule` define Id, DisplayName, Icon, Order y CreateView(): UserControl. Acopla el contrato modular a WPF y no expone métodos de activación/desactivación o liberación.

## ModuleCatalog
Clase estática que almacena IReadOnlyList<IModule>, recibe IEnumerable mediante Register y ordena por Order. Es un punto global de consulta. Revisar validación de IDs duplicados y consumidores antes de cambiarlo.

## Modelos
Se inventariaron 25 archivos en Core/Models:
AssistantMessage, AudioSessionInfo, CommandCategory, CommandShortcut, DeviceInfo, DriveStat, FileSystemEntry, FolderShortcut, Game, GameInfo, GameSource, GeneratedImage, ImageGenParams, MediaAppShortcut, MusicMetadata, MusicTrack, NetworkDevice, NowPlayingInfo, PlaceholderModule, ProcessLoadInfo, StagedFile, StatusModels, SystemSnapshot, TerminalHost y UplinkDevice.

### Observaciones de diseño
- Modelos simples con defaults y colecciones inicializadas.
- Algunos implementan INotifyPropertyChanged o heredan ViewModelBase.
- Algunos contienen tipos WPF (BitmapSource, ImageSource), NAudio (AudioSessionControl) o lógica de presentación calculada.
- Game y CommandShortcut se serializan; propiedades derivadas usan JsonIgnore cuando corresponde.
- MusicTrack indica expresamente que no se persiste: fuente de verdad son etiquetas de audio.
- GameInfo separa datos descargados y overrides editados por el usuario.
- SystemSnapshot incluye TopProcesses, pero el muestreo actual no asigna la colección en Sample().

## Servicios
Se inventarió el directorio Core/Services y se revisaron especialmente persistencia, credenciales, web, LocalSend, audio, monitorización, herramientas externas, actualizaciones y Uplink. No se considera una auditoría línea a línea de todos los servicios.

Hallazgos: ver [[../03-Audit/Findings|Findings]].

## Prácticas positivas
- ThumbnailService limita concurrencia y admite cancelación.
- ShellThumbnailService libera HBITMAP y objeto COM en finally.
- UplinkService implementa IDisposable y libera recursos observados.
- GameInfoStore recupera valores por defecto cuando el JSON no puede leerse.
- MusicLibraryService trata fallos de lectura por archivo.

## Pendiente
- Revisar consumidores de cada servicio.
- Revisar todos los constructores y singletons.
- Comprobar serialización/compatibilidad de todos los modelos.
- Ejecutar build y pruebas en Windows.
- Completar análisis de converters y Mvvm.
