---
title: "Imagegen"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, module]
---

# ImageGen

## Responsabilidad

Integrar funcionalidades de generación de imágenes en Launcher.

## Funcionalidades que investigar

- Conexión con motores de generación.
- Configuración de modelos.
- Parámetros de generación.
- Gestión de prompts.
- Cola de trabajos.
- Visualización de resultados.
- Galería.
- Guardado de imágenes.

## Contexto documentado

El proyecto ha contemplado una integración con AUTOMATIC1111 / Stable Diffusion.

La integración actual debe verificarse.

## Dependencias que investigar

- Models.
- Assistant.
- Microsoft.Playwright, si existe utilización efectiva.
- Servicios HTTP.
- Gestión de imágenes.
- Persistencia.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar motores compatibles.
- [ ] Revisar endpoints.
- [ ] Revisar gestión de prompts.
- [ ] Revisar trabajos asíncronos.
- [ ] Revisar cancelación.
- [ ] Revisar almacenamiento.
- [ ] Revisar tratamiento de errores.
- [ ] Revisar seguridad de comunicaciones.

## Riesgos

- Dependencia de servicios externos.
- Consumo elevado de recursos.
- Operaciones largas.
- Imágenes temporales no eliminadas.
- Credenciales o endpoints expuestos.

## Documentación relacionada

- [[MODULES]]
- [[MODELS]]
- [[ASSISTANT]]

## Estado contrastado con código — 2026-11-11

Registrado como ImageGenModule (Order 7). ImageGenView se conecta con AUTOMATIC1111 mediante StableDiffusionService; integra galería, parámetros, autocompletado y extensiones. Puede intentar iniciar el servidor si AutoStartServer está habilitado. Generación admite cancelación mediante CancellationTokenSource e interrupción de A1111.

Pendiente: probar fallos de conexión, cancelación real del backend, rutas de instalación, extensiones y persistencia de imágenes.
