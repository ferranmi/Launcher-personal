---
title: "Folders"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Folders

## Responsabilidad

Gestionar accesos y navegación por carpetas y ubicaciones del sistema.

## Funcionalidades documentadas

- Tarjetas de accesos directos.
- Alta y edición.
- Eliminación.
- Iconos personalizados.
- Detección de rutas inexistentes.
- Accesos predeterminados.

## Componentes identificados

- `FoldersModule`.
- `FoldersView`.
- `FolderShortcut`.
- `FolderShortcutsService`.
- Ventana Add/Edit.

## Funcionalidad pendiente documentada

Se ha planteado transformar el módulo en un explorador integrado con navegación, miniaturas y previsualización.

No se considera implementada hasta verificarlo.

## Dependencias que investigar

- Core.
- FolderShortcutsService.
- Sistema de archivos.
- Persistencia JSON.
- UI.

## Auditoría

- [ ] Revisar rutas.
- [ ] Revisar permisos.
- [ ] Revisar operaciones de archivos.
- [ ] Analizar navegación.
- [ ] Revisar persistencia.
- [ ] Analizar accesos predeterminados.
- [ ] Revisar enlaces simbólicos.
- [ ] Identificar riesgos de acceso a ubicaciones externas.

## Riesgos

- Rutas inexistentes.
- Acceso a ubicaciones no disponibles.
- Operaciones sobre archivos sin confirmación.
- Problemas con rutas largas.

## Documentación relacionada

- [[MODULES]]
- [[SETTINGS]]
- [[COMMANDS]]

## Estado contrastado con código — 2026-11-11

Registrado como FoldersModule (Order 1). Usa FolderShortcutsService, FolderShortcut y FileSystemEntry; el árbol incluye FolderExplorerPanel, ThumbnailService y ShellThumbnailService. La integración exacta de todas las operaciones del explorador debe comprobarse en FoldersView antes de asumirlas.

Pendiente: probar navegación, rutas largas, permisos, miniaturas y operaciones de archivos. Los errores de persistencia JSON se describen en [[../03-Audit/Findings|Findings]].
