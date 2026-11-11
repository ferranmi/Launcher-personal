---
title: "Games"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Games

## Responsabilidad

Gestionar una biblioteca unificada de juegos y permitir su lanzamiento.

## Funcionalidades documentadas

- Biblioteca de juegos.
- Integración con Steam.
- Integración con Epic Games.
- Juegos manuales.
- Portadas.
- Favoritos.
- Ocultación.
- Búsqueda y filtros.
- Tiempo jugado.
- Sincronización.
- Gestión de juegos ignorados.

## Componentes identificados

- `GamesModule`.
- `GamesView`.
- `GameLibraryService`.
- `IGameProvider`.
- `SteamGameProvider`.
- `EpicGameProvider`.
- `Game`.
- `GameSource`.
- `CoverArtService`.

## Persistencia documentada

- `games.json`.
- `ignored-games.json`.
- Directorio de portadas.

## Auditoría

- [ ] Verificar proveedores.
- [ ] Revisar sincronización.
- [ ] Validar manifiestos de Steam.
- [ ] Validar manifiestos de Epic.
- [ ] Revisar ejecución de juegos.
- [ ] Revisar medición de tiempo.
- [ ] Revisar descarga de portadas.
- [ ] Revisar concurrencia.
- [ ] Revisar persistencia.
- [ ] Revisar seguridad de rutas y ejecutables.

## Riesgos

- Cambios en formatos de manifiestos.
- Portadas incorrectas.
- Descargas lentas.
- Juegos lanzados mediante protocolo sin proceso directamente controlable.
- Datos duplicados durante sincronización.

## Documentación relacionada

- [[MODULES]]
- [[DASHBOARD]]
- [[FOLDERS]]
- [[SETTINGS]]

## Estado contrastado con código — 2026-11-11

Registrado como GamesModule (Order 0). GameLibraryService mantiene la biblioteca y sincroniza proveedores Steam/Epic; GameInfoStore almacena información adicional por juego. SteamApiKey se guarda actualmente dentro de settings.json a través de LibraryPathsService, sin CredentialStore. Ver [[../03-Audit/Findings|Findings]].

GamesView inicia sincronización automática una sola vez al cargar si el ajuste está activado; el drag-and-drop admite ejecutables .exe. Probar proveedores, rutas desconectadas, metadatos y persistencia.
