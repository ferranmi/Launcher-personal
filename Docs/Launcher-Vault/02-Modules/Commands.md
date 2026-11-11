---
title: "Commands"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Commands

## Responsabilidad

Gestionar accesos y comandos personalizados ejecutables desde Launcher.

## Funcionalidades documentadas

- Listado de comandos.
- Alta, edición y eliminación.
- Categorías.
- Sugerencias.
- Ejecución de comandos.
- Persistencia de accesos.

## Componentes identificados en documentación

- `CommandsModule`.
- `CommandsView`.
- `CommandsService`.
- `CommandShortcut`.
- `CommandCategory`.
- Ventana Add/Edit.

## Dependencias que investigar

- Core.
- AppSettingsService.
- Persistencia JSON.
- Procesos de Windows.
- Navegadores y terminales externos.

## Auditoría

- [ ] Verificar implementación actual.
- [ ] Revisar comandos permitidos.
- [ ] Analizar argumentos.
- [ ] Revisar ejecución de procesos.
- [ ] Revisar permisos.
- [ ] Revisar validación de rutas.
- [ ] Revisar persistencia.
- [ ] Revisar manejo de errores.
- [ ] Identificar posibles inyecciones de comandos.

## Riesgos

- Ejecución de comandos no deseados.
- Argumentos mal validados.
- Bloqueos por procesos síncronos.
- Exposición de rutas o parámetros sensibles.

## Documentación relacionada

- [[MODULES]]
- [[PROCESSES]]
- [[SETTINGS]]

## Estado contrastado con código — 2026-11-11

Registrado como CommandsModule (Order 10). Usa CommandsService y persiste en `commands.json`. Incluye accesos a aplicaciones/comandos y WebShortcutWindow para webs, perfiles, modo privado y autologin. El autologin usa CredentialStore y Playwright.

Riesgos: destinos URI aceptados de forma amplia por diseño; autologin admite HTTP y no vincula credenciales al origen final tras redirecciones. Ver [[../03-Audit/Findings|Findings]]. No modificar la ejecución de comandos sin revisar los casos de uso.
