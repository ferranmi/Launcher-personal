---
title: "Assistant"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Assistant

## Responsabilidad

Módulo destinado a proporcionar un asistente de inteligencia artificial integrado en Launcher.

## Funcionalidades que investigar

- Interfaz conversacional.
- Comunicación con modelos locales.
- Historial de conversaciones.
- Gestión del contexto.
- Ejecución de acciones desde el asistente.
- Integración con el resto de módulos.
- Atajos globales y activación rápida.

## Componentes identificados en documentación

- `AssistantWindow`.
- `AssistantMessage`.
- `OllamaService`.
- `GlobalHotkeyService`.

La documentación anterior indica que AssistantWindow existe en el snapshot, pero no está registrado como módulo. Debe comprobarse en `main`.

## Dependencias que investigar

- Models.
- OllamaService.
- AppSettingsService.
- Servicios de navegación.
- Core.

## Auditoría

- [ ] Localizar implementación actual.
- [ ] Verificar registro como módulo.
- [ ] Inspeccionar AssistantWindow.
- [ ] Inspeccionar OllamaService.
- [ ] Verificar conexión con Ollama.
- [ ] Revisar gestión de conversaciones.
- [ ] Revisar seguridad de acciones.
- [ ] Revisar tratamiento de errores.
- [ ] Identificar funcionalidades incompletas.

## Riesgos

- Exposición de información sensible en el contexto.
- Ejecución de acciones sin autorización suficiente.
- Bloqueos de interfaz durante inferencias.
- Dependencia de servicios externos o locales.

## Documentación relacionada

- [[MODULES]]
- [[MODELS]]
- [[../01-Architecture/DEPENDENCIES]]

## Estado contrastado con código — 2026-11-11

Existe `AssistantWindow.xaml.cs` y usa `OllamaService`; no existe `AssistantModule` ni registro en MainWindow. Es una ventana independiente. El historial se mantiene en memoria y la ventana se cierra al perder foco. No asumir persistencia ni integración como módulo. Pendiente: decidir si se conserva como paleta auxiliar o se integra en navegación.
