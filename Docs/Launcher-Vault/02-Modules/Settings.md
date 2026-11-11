---
title: "Settings"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Settings

## Responsabilidad

Centralizar la configuración y las preferencias de Launcher.

## Funcionalidades documentadas

- Configuración general.
- Preferencias de ventana.
- Inicio con Windows.
- Actualizaciones.
- Configuración de bibliotecas.
- Módulo inicial.
- Gestión de juegos ignorados.

## Componentes identificados

- `SettingsModule`.
- `SettingsView`.
- `AppSettingsService`.
- `LibraryPathsService`.
- `StartupService`.
- `UpdateService`.
- `ModuleCatalog`.

## Persistencia documentada

- `config.json`.
- `settings.json`.

También se documenta el uso del registro de Windows para el inicio automático.

## Dependencias que investigar

- Core.
- ModuleCatalog.
- Todos los módulos que exponen preferencias.
- Servicios de actualización.
- Persistencia.

## Auditoría

- [ ] Verificar vista actual.
- [ ] Inventariar opciones.
- [ ] Revisar valores predeterminados.
- [ ] Revisar serialización.
- [ ] Revisar compatibilidad de versiones.
- [ ] Revisar migraciones.
- [ ] Revisar inicio con Windows.
- [ ] Revisar actualización.
- [ ] Comprobar persistencia.
- [ ] Revisar validación de entradas.

## Riesgos

- Configuraciones incompatibles.
- Valores predeterminados incorrectos.
- Pérdida de preferencias.
- Dependencias globales.
- Opciones que no afectan realmente al comportamiento.

## Documentación relacionada

- [[MODULES]]
- [[COMMANDS]]
- [[GAMES]]
- [[ASSISTANT]]

## Estado contrastado con código — 2026-11-11

Registrado como SettingsModule (Order 100). SettingsView construye dinámicamente opciones de módulo desde ModuleCatalog y gestiona preferencias, actualizaciones, inicio con Windows y personalización. AppSettingsService.Reset no reasigna ConfirmKillProcess aunque el ajuste existe; intención pendiente. Save notifica Changed incluso si la escritura falla. Ver [[../03-Audit/Findings|Findings]].
