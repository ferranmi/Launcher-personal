---
title: "Models"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# Models

## Responsabilidad

Gestionar los modelos de inteligencia artificial disponibles para Launcher.

## Funcionalidades que investigar

- Detección de modelos locales.
- Listado de modelos.
- Información de tamaño y formato.
- Descarga.
- Instalación.
- Eliminación.
- Selección de modelo activo.
- Integración con Ollama.
- Integración con otros motores.

## Dependencias que investigar

- OllamaService.
- Assistant.
- ImageGen.
- Servicios HTTP.
- Sistema de archivos.
- AppSettingsService.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar motores.
- [ ] Revisar detección.
- [ ] Revisar descargas.
- [ ] Revisar almacenamiento.
- [ ] Revisar selección de modelos.
- [ ] Revisar gestión de errores.
- [ ] Revisar seguridad de archivos descargados.
- [ ] Analizar uso de GPU y memoria.

## Riesgos

- Modelos incompatibles.
- Descargas incompletas.
- Uso excesivo de almacenamiento.
- Consumo de VRAM.
- Confusión entre modelos de texto e imagen.

## Documentación relacionada

- [[MODULES]]
- [[ASSISTANT]]
- [[IMAGEGEN]]
- [[MONITOR]]

## Estado contrastado con código — 2026-11-11

Registrado como ModelsModule (Order 8). El árbol incluye catálogo Civitai, biblioteca, descargas, cola, referencias, renombrado y wildcards. La clave API de Civitai se guarda en Windows Credential Manager según ModelsView.

Pendiente: validar integridad de descargas, límites de concurrencia, cancelación, operaciones de renombrado/movimiento y compatibilidad con cambios de la API.
