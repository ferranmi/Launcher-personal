---
title: Assistant
type: module
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/module]
---

# Assistant

## Estado de integración
No es módulo registrado. Existe AssistantWindow; instancia OllamaService, mantiene historial en memoria y transmite respuestas. Se cierra al perder foco. No hay AssistantModule ni registro en MainWindow. Pendiente: decidir si ventana auxiliar o módulo; revisar permisos/contexto y persistencia.

## Archivos y componentes
Consultar el directorio `Modules/Assistant/` en el commit auditado. Los nombres citados arriba se han identificado en el árbol o en la inspección estática.

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