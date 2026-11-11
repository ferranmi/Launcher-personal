---
title: "Devices"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Devices

## Responsabilidad

Gestionar y mostrar información sobre dispositivos conectados al equipo.

## Funcionalidades que investigar

- Enumeración de dispositivos.
- Información de hardware.
- Identificación de dispositivos.
- Estado de conexión.
- Información de dispositivos multimedia.
- Posibles acciones de gestión.

## Dependencias que investigar

- System.Management.
- Windows APIs.
- Core.
- Models.
- Servicios de monitorización.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar fuentes de información.
- [ ] Revisar consultas WMI.
- [ ] Revisar actualización de estados.
- [ ] Analizar permisos.
- [ ] Revisar gestión de errores.
- [ ] Comprobar liberación de recursos.

## Riesgos

- Consultas WMI costosas.
- Diferencias entre versiones de Windows.
- Dispositivos que desaparecen durante una consulta.
- Confusión entre dispositivos físicos y lógicos.

## Documentación relacionada

- [[MODULES]]
- [[MONITOR]]
- [[NETWORK]]

## Estado contrastado con código — 2026-11-11

Registrado como DevicesModule (Order 7). La vista consulta dispositivos mediante servicios Windows/WMI y permite abrir ajustes o aplicaciones asociadas. Revisar permisos, disponibilidad de dispositivos y latencia WMI en pruebas Windows.
