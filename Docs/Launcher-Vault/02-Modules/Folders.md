---

title: Folders module_id: folders type: module status: documented-pending-verification tags:

- launcher/module
- launcher/filesystem

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