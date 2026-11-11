---
title: "Network"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Network

## Responsabilidad

Proporcionar información y funcionalidades relacionadas con la conectividad de red.

## Funcionalidades que investigar

- Adaptadores de red.
- Estado de conexión.
- Direcciones IP.
- Información de interfaces.
- Transferencias.
- Conectividad local.
- Integraciones con servicios de transferencia de archivos.

## Contexto funcional

El proyecto ha contemplado funcionalidades relacionadas con LocalSend y Tailscale. Debe verificarse si forman parte del módulo actual o son propuestas.

## Dependencias que investigar

- Windows Networking APIs.
- System.Management.
- Servicios HTTP.
- Devices.
- Core.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar adaptadores.
- [ ] Revisar actualización de estado.
- [ ] Revisar tratamiento de direcciones.
- [ ] Identificar integraciones externas.
- [ ] Revisar transferencias.
- [ ] Revisar seguridad de comunicaciones.
- [ ] Revisar permisos.

## Riesgos

- Exposición de información de red.
- Direcciones cambiantes.
- Servicios externos no disponibles.
- Transferencias sin validación suficiente.

## Documentación relacionada

- [[MODULES]]
- [[DEVICES]]
- [[SETTINGS]]

## Estado contrastado con código — 2026-11-11

Registrado como NetworkModule (Order 7). NetworkView muestra dispositivos Tailscale y prepara archivos antes de transferir. FileTransferService intenta LocalSend y contempla fallback mediante Tailscale.

LocalSendService acepta certificados HTTPS sin validarlos y HttpClient usa timeout infinito; prepare-upload tiene límite de 3 minutos, pero upload depende de cancelación manual. Ver [[../03-Audit/Findings|Findings]]. La aceptación de certificado no equivale a tráfico en claro: es una carencia de autenticación TLS del peer.
