---
title: "Processes"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Processes

## Responsabilidad

Consultar y mostrar los procesos activos del sistema.

## Funcionalidades documentadas

- Listado de procesos.
- Información de consumo.
- Monitorización.
- Agrupación de procesos.
- Información de carga.

## Componentes identificados

- `ProcessesModule`.
- `ProcessesView`.
- `ProcessMonitorService`.
- `ProcessGroup`.
- `ProcessRow`.
- `ProcessSample`.
- `ProcessLoadInfo`.

## Dependencias que investigar

- System.Management.
- System.Diagnostics.
- Monitor.
- Core.

## Auditoría

- [ ] Verificar enumeración.
- [ ] Revisar actualización de datos.
- [ ] Revisar cálculo de consumo.
- [ ] Analizar agrupación.
- [ ] Revisar permisos.
- [ ] Revisar procesos protegidos.
- [ ] Comprobar liberación de recursos.
- [ ] Revisar concurrencia.
- [ ] Comprobar impacto sobre el rendimiento.

## Riesgos

- Acceso denegado a determinados procesos.
- Lecturas inconsistentes.
- Actualizaciones demasiado frecuentes.
- Suscripciones o temporizadores persistentes.
- Operaciones que afecten al sistema.

## Documentación relacionada

- [[MODULES]]
- [[MONITOR]]
- [[DEVICES]]

## Estado contrastado con código — 2026-11-11

Registrado como ProcessesModule (Order 4). ProcessesView ejecuta muestreo en Task.Run, evita solapamientos y llama Dispose al descargar la vista; ProcessMonitorService sigue siendo reutilizable tras esa llamada y reinicia las referencias de CPU/GPU. Probar refrescos, permisos, finalización de procesos y vuelta a la vista.
