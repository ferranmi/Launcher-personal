---
title: "Dashboard"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Dashboard

## Responsabilidad

Proporcionar una pantalla principal con información resumida y elementos configurables del sistema.

## Funcionalidades que investigar

- Página de inicio.
- Widgets.
- Paneles configurables.
- Distribución de elementos.
- Información de sistema.
- Accesos rápidos.
- Persistencia de distribución.

## Componentes documentados históricamente

- `DashboardView`.
- `DashboardBoard`.
- `BoardBox`.
- `IWidget`.
- `WidgetCatalog`.

La documentación advierte que estos elementos no aparecían en el snapshot anterior. No se considera verificada su existencia actual.

## Dependencias que investigar

- Monitor.
- Games.
- Processes.
- Network.
- Devices.
- Sistema de widgets.
- Persistencia.

## Auditoría

- [ ] Localizar vista actual.
- [ ] Verificar registro.
- [ ] Localizar sistema de widgets.
- [ ] Revisar configuración del tablero.
- [ ] Analizar persistencia.
- [ ] Revisar actualización de datos.
- [ ] Analizar rendimiento.
- [ ] Identificar dependencias entre widgets.

## Riesgos

- Actualizaciones frecuentes de datos.
- Widgets que mantienen recursos activos.
- Dependencias circulares.
- Acoplamiento entre presentación y módulos.

## Documentación relacionada

- [[MODULES]]
- [[MONITOR]]
- [[GAMES]]
- [[PROCESSES]]

## Estado contrastado con código — 2026-11-11

Registrado como DashboardModule (Order -1). DashboardView usa DispatcherTimer de 1 s y realiza muestreo de sistema/procesos y refresco de Now Playing. El timer se detiene en Unloaded; la vista se mantiene cacheada por MainWindow. El impacto de muestreo síncrono y posible duplicación con SystemStatusHost se documenta en [[../03-Audit/Findings|Findings]].
