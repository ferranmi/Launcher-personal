---
title: "Media"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Media

## Responsabilidad

Gestionar aplicaciones multimedia y proporcionar información y controles relacionados con la reproducción.

## Funcionalidades documentadas

- Listado de aplicaciones multimedia.
- Alta y edición de aplicaciones.
- Integración con reproducción actual.
- Información de sesiones de audio.

## Componentes identificados

- `MediaModule`.
- `MediaView`.
- `MediaAppShortcut`.
- `MediaAppsService`.
- `NowPlayingService`.
- `AudioMixerService`.
- `NowPlayingInfo`.
- `AudioSessionInfo`.

## Dependencias que investigar

- NAudio.
- TagLibSharp.
- Windows APIs.
- Core.
- Persistencia JSON.

## Auditoría

- [ ] Verificar vista actual.
- [ ] Identificar aplicaciones compatibles.
- [ ] Revisar sesiones de audio.
- [ ] Revisar controles de reproducción.
- [ ] Analizar integración con Music.
- [ ] Revisar suscripciones a eventos.
- [ ] Revisar liberación de recursos.
- [ ] Revisar persistencia.

## Riesgos

- Sesiones de audio que desaparecen.
- Recursos no liberados.
- Diferencias entre reproductores.
- Dependencias entre Media y Music.

## Documentación relacionada

- [[MODULES]]
- [[MUSIC]]
- [[SETTINGS]]

## Estado contrastado con código — 2026-11-11

Registrado como MediaModule (Order 2). MediaView usa NowPlayingService y AudioMixerService. El timer se detiene en Unloaded, pero la vista se cachea. AudioMixerService mantiene MMDeviceEnumerator y MMDevice sin implementar Dispose explícito; al cambiar dispositivo sustituye la referencia anterior. Ver [[../03-Audit/Findings|Findings]].
