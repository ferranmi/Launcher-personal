---
title: "Monitor"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Monitor

## Responsabilidad

Mostrar información de rendimiento y estado del sistema.

## Funcionalidades documentadas

- Monitorización del sistema.
- Información de procesador.
- Memoria.
- Discos.
- GPU.
- Información de rendimiento.

## Componentes identificados

- `MonitorModule`.
- `MonitorView`.
- `SystemMonitorService`.
- `SystemSnapshot`.
- `DriveStat`.

## Dependencias que investigar

- System.Management.
- System.Diagnostics.PerformanceCounter, si sigue presente.
- Windows APIs.
- Core.

## Auditoría

- [ ] Verificar métricas disponibles.
- [ ] Identificar frecuencia de actualización.
- [ ] Revisar consultas WMI.
- [ ] Revisar contadores.
- [ ] Comprobar compatibilidad GPU.
- [ ] Revisar concurrencia.
- [ ] Revisar consumo de CPU.
- [ ] Revisar tratamiento de valores no disponibles.
- [ ] Revisar liberación de recursos.

## Riesgos

- Consultas frecuentes y costosas.
- Contadores no disponibles.
- Diferencias de hardware.
- Bloqueos de interfaz.
- Lecturas incorrectas de memoria o GPU.

## Documentación relacionada

- [[MODULES]]
- [[DEVICES]]
- [[PROCESSES]]
- [[MODELS]]

## Estado contrastado con código — 2026-11-11

Registrado como MonitorModule (Order 6). MonitorView crea su propia instancia de SystemMonitorService y usa DispatcherTimer. SystemStatusHost mantiene otra instancia y otro ciclo de muestreo. Sample incluye consultas de CPU, RAM, GPU, VRAM, red y discos; parte del trabajo puede bloquear Dispatcher. Ver [[../03-Audit/Findings|Findings]].
