---
title: "Music"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Music

## Responsabilidad

Proporcionar funcionalidades específicas para la gestión y reproducción musical.

## Funcionalidades que investigar

- Biblioteca musical.
- Exploración de archivos.
- Metadatos.
- Álbumes y artistas.
- Portadas.
- Reproducción.
- Listas de reproducción.
- Integración con Media.

## Dependencias que investigar

- NAudio.
- TagLibSharp.
- NowPlayingService.
- AudioMixerService.
- Sistema de archivos.
- Persistencia.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar reproductor.
- [ ] Revisar formatos soportados.
- [ ] Revisar lectura de metadatos.
- [ ] Revisar portadas.
- [ ] Revisar listas.
- [ ] Revisar persistencia.
- [ ] Revisar ciclo de vida del audio.
- [ ] Comprobar integración con Media.

## Riesgos

- Conflictos entre sesiones.
- Lectura de metadatos defectuosos.
- Archivos grandes.
- Recursos de reproducción sin liberar.
- Duplicación de responsabilidades con Media.

## Documentación relacionada

- [[MODULES]]
- [[MEDIA]]
- [[FOLDERS]]

## Estado contrastado con código — 2026-11-11

Registrado como MusicModule (Order 3). MusicLibraryService obtiene metadatos desde archivos; MusicTrack no se persiste como JSON. ImportAsync compara rutas mediante StartsWith sobre rutas completas, lo que puede confundir carpetas hermanas con subdirectorios. Ver DATA-002 en [[../03-Audit/Findings|Findings]].

ExternalToolsService gestiona yt-dlp y FFmpeg; no se observó verificación explícita de hash/firma en el flujo revisado.
