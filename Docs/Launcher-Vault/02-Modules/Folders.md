---
title: Folders
type: module
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/module]
---

# Folders

## Estado de integración
Registrado como FoldersModule (Order 1), vista FoldersView. Usa FolderShortcut y FolderShortcutsService. El árbol incluye FolderExplorerPanel y FileSystemEntry, que modela entradas de archivos/carpetas y clasifica imágenes, vídeo, audio, texto y otros tipos.

La presencia de FolderExplorerPanel no demuestra por sí sola que todas las operaciones del explorador estén integradas o completas. Verificar el flujo real desde FoldersView antes de afirmar el alcance funcional.

## Componentes identificados
- FoldersModule
- FoldersView
- FolderExplorerPanel
- AddEditFolderWindow
- FolderShortcut
- FileSystemEntry
- FolderShortcutsService
- ThumbnailService y ShellThumbnailService (servicios compartidos de miniaturas)

## Auditoría pendiente
- [ ] Comprobar integración de FolderExplorerPanel en FoldersView.
- [ ] Revisar navegación, historial y selección.
- [ ] Revisar miniaturas, caché y cancelación.
- [ ] Revisar operaciones de copiar/mover/borrar y confirmaciones.
- [ ] Revisar rutas largas, enlaces simbólicos y permisos.
- [ ] Revisar accesos predeterminados y persistencia JSON.
- [ ] Ejecutar pruebas con ubicaciones inexistentes y unidades desconectadas.

## Dependencias y datos
Ver [[../01-Architecture/Dependencies|Dependencias]] y [[../03-Audit/Findings|Hallazgos]]. No inferir dependencias directas solo por coexistencia en el árbol.

## Regla
La existencia de archivos y el registro del módulo no demuestran que todas sus funciones estén verificadas en ejecución.