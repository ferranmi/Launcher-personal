---
title: "Modules"
type: module-index
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, documentation]
---

# Module Catalog

## 1. Objetivo

Índice central de los 14 módulos funcionales oficiales de Launcher.

Todos los módulos forman parte del alcance de análisis, independientemente de su implementación actual.

## 2. Inventario oficial

|#|Módulo|ID|Estado de conocimiento|
|---|---|---|---|
|1|[[ASSISTANT]]|assistant|Documentado / pendiente|
|2|[[COMMANDS]]|commands|Documentado|
|3|[[Dashboard]]|dashboard|Documentado / pendiente|
|4|[[DEVICES]]|devices|Pendiente|
|5|[[FOLDERS]]|folders|Documentado|
|6|[[GAMES]]|games|Documentado|
|7|[[IMAGEGEN]]|imagegen|Documentado / pendiente|
|8|[[MEDIA]]|media|Documentado|
|9|[[MODELS]]|models|Documentado / pendiente|
|10|[[MONITOR]]|monitor|Documentado|
|11|[[MUSIC]]|music|Documentado|
|12|[[NETWORK]]|network|Pendiente|
|13|[[PROCESSES]]|processes|Documentado|
|14|[[SETTINGS]]|settings|Documentado|

Los estados anteriores reflejan el nivel de información disponible, no una certificación del funcionamiento actual.

## 3. Clasificación de módulos

### Organización y navegación

- [[Dashboard]]
- [[SETTINGS]]

### Sistema y hardware

- [[DEVICES]]
- [[MONITOR]]
- [[NETWORK]]
- [[PROCESSES]]

### Productividad

- [[COMMANDS]]
- [[FOLDERS]]

### Biblioteca y multimedia

- [[GAMES]]
- [[MEDIA]]
- [[MUSIC]]

### Inteligencia artificial

- [[ASSISTANT]]
- [[IMAGEGEN]]
- [[MODELS]]

## 4. Contrato arquitectónico

Los módulos deben respetar el contrato común:

`Core/Interfaces/IModule.cs`

Elementos documentados:

- `Id`
- `DisplayName`
- `Icon`
- `Order`
- `CreateView()`

El registro y la navegación se gestionan desde la aplicación principal.

No se presupone que todos los módulos implementen actualmente el contrato.

## 5. Fichas individuales

|Ficha|Ruta|
|---|---|
|Assistant|`ASSISTANT.md`|
|Commands|`COMMANDS.md`|
|Dashboard|`DASHBOARD.md`|
|Devices|`DEVICES.md`|
|Folders|`FOLDERS.md`|
|Games|`GAMES.md`|
|ImageGen|`IMAGEGEN.md`|
|Media|`MEDIA.md`|
|Models|`MODELS.md`|
|Monitor|`MONITOR.md`|
|Music|`MUSIC.md`|
|Network|`NETWORK.md`|
|Processes|`PROCESSES.md`|
|Settings|`SETTINGS.md`|

## 6. Criterios de auditoría

Cada módulo deberá documentar:

1. Propósito.
2. Estado real.
3. Clase que implementa el módulo.
4. Vista principal.
5. ViewModels.
6. Servicios propios.
7. Servicios compartidos utilizados.
8. Modelos.
9. Dependencias externas.
10. Persistencia.
11. Eventos y comunicación.
12. Integración con otros módulos.
13. Problemas detectados.
14. Deuda técnica.
15. Funcionalidades pendientes.
16. Pruebas existentes.

## 7. Reglas

- No eliminar módulos del inventario.
- No asumir que una carpeta equivale a un módulo funcional.
- No confundir una clase placeholder con una implementación.
- Identificar funcionalidades huérfanas.
- Detectar servicios compartidos duplicados.
- Identificar dependencias circulares.
- Documentar los archivos relevantes, no cada archivo indiscriminadamente.
- Registrar evidencias para los hallazgos.
- Mantener separadas las funcionalidades implementadas y las propuestas.

## 8. Documentos relacionados

- [[../00-Project/PROJECT_CONTEXT|Project Context]]
- [[../00-Project/CURRENT_STATE|Current State]]
- [[../01-Architecture/ARCHITECTURE|Architecture]]
- [[../01-Architecture/DEPENDENCIES|Dependencies]]
- [[../04-Audit/FINDINGS|Audit Findings]]
- [[../../Home|Home]]

## 9. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Creación del catálogo oficial de 14 módulos|

## Registro contrastado con código — 2026-11-11

MainWindow registra 13 módulos: Dashboard, Games, Folders, Media, Music, Network, ImageGen, Models, Commands, Devices, Monitor, Processes y Settings. SearchModule implementa IModule pero no se registra. AssistantWindow es una ventana independiente y no hay AssistantModule.

Se ha creado [[Search|Search]] para documentar la implementación no registrada. No convertir el inventario histórico de 14 módulos en especificación actual.
