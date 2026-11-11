---
title: Models
type: module
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/module]
---

# Models

## Estado de integración
Registrado como ModelsModule (Order 8), vista ModelsView. El directorio incluye catálogo, servicio Civitai, biblioteca, descargas, cola, referencias, renombrado y wildcards. Revisar integridad de descargas, operaciones sobre archivos, rutas, concurrencia y compatibilidad con Civitai.

## Archivos y componentes
Consultar el directorio `Modules/Models/` en el commit auditado. Los nombres citados arriba se han identificado en el árbol o en la inspección estática.

## Dependencias y datos
Los servicios/modelos compartidos relacionados se enumeran en el texto anterior. No se debe inferir una dependencia directa solo por coexistir en el proyecto. Ver [[../01-Architecture/Dependencies|Dependencias]].

## Auditoría pendiente
- [ ] Leer todas las vistas XAML y code-behind.
- [ ] Identificar cada consumidor y evento.
- [ ] Revisar validación de entradas y permisos.
- [ ] Revisar concurrencia, cancelación y errores.
- [ ] Revisar persistencia y compatibilidad.
- [ ] Revisar ciclo de vida de timers, procesos y recursos.
- [ ] Definir y ejecutar pruebas en Windows.

## Hallazgos relacionados
Ver [[../03-Audit/Findings|Hallazgos]] y [[../00-Project/Roadmap|Roadmap]].

## Regla
La existencia de archivos y el registro del módulo no demuestran que todas sus funciones estén verificadas en ejecución.