---
title: "Home"
type: dashboard
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, documentation]
---

# Launcher — Project Intelligence

> Centro de conocimiento técnico, arquitectura y auditoría del proyecto Launcher.

## Proyecto

- **Repositorio:** [ferranmi/Launcher-personal](https://github.com/ferranmi/Launcher-personal)
- **Rama de referencia:** `main`
- **Tecnología:** C# / WPF / .NET 10
- **Plataforma:** Windows
- **Versión declarada en código:** 0.1.53
- **Estado del Vault:** En construcción

## Contexto del proyecto

- [[00-Project/PROJECT_CONTEXT|Project Context]]
- [[00-Project/CURRENT_STATE|Current State]]
- [[00-Project/ROADMAP|Roadmap]]

## Arquitectura

- [[01-Architecture/ARCHITECTURE|Architecture]]
- [[01-Architecture/DIRECTORY_MAP|Directory Map]]
- [[01-Architecture/DEPENDENCIES|Dependencies]]
- [[02-Modules/MODULES|Module Catalog]]

## Auditoría

- [[03-Audit/Findings|Findings]]
- [[03-Audit/Security|Security Audit]]
- [[03-Audit/Quality|Code Quality]]
- [[03-Audit/Findings|Testing Audit]]

## Decisiones técnicas

- [[04-Decisions/Decisions|Technical Decisions]]

## Trabajo con IA

- [[05-AI/AGENTS|Agent Instructions]]
- [[05-AI/TASK_CONTEXT_TEMPLATE|Task Context Template]]

## Historial

- [[06-History/AUDIT_HISTORY|Audit History]]

---

## Estado del análisis

|Área|Estado|
|---|---|
|Identificación del proyecto|Iniciado|
|Inventario de archivos|Parcial|
|Arquitectura|Pendiente|
|Catálogo de módulos|Pendiente|
|Dependencias|Parcial|
|Auditoría de código|Pendiente|
|Auditoría de seguridad|Pendiente|
|Pruebas|Pendiente|
|Instrucciones para IA|Pendiente|

## Reglas de conocimiento

1. GitHub `main` es la fuente de verdad del código.
2. El código tiene prioridad sobre las descripciones antiguas.
3. Las decisiones del usuario deben quedar registradas.
4. Los hallazgos deben incluir evidencias.
5. Las hipótesis no se documentarán como hechos.
6. Las funcionalidades incompletas deben identificarse explícitamente.
7. Cada cambio relevante debe actualizar las notas afectadas.

## Etiquetas

- `#launcher/project`
- `#launcher/architecture`
- `#launcher/module`
- `#launcher/audit`
- `#launcher/decision`
- `#launcher/ai`
- `#launcher/pending`
- `#launcher/verified`

## Actualización de auditoría — 2026-11-11

La bóveda se ha reconciliado con el código del commit `5c621accde912fc8dda48873fb44d4d93857901a`, versión 0.1.53. El registro real contiene 13 módulos; Search está implementado pero no registrado, y Assistant es una ventana independiente.

### Índices actuales
- [[00-Project/Current State|Estado actual]]
- [[01-Architecture/Architecture|Arquitectura]]
- [[01-Architecture/CORE_AUDIT|Auditoría de Core]]
- [[01-Architecture/Dependencies|Dependencias]]
- [[02-Modules/Modules|Catálogo de módulos]]
- [[02-Modules/Search|Módulo Search no registrado]]
- [[03-Audit/Findings|Hallazgos]]
- [[03-Audit/Security|Seguridad]]
- [[03-Audit/Quality|Calidad]]
- [[03-Audit/Technical Debt|Deuda técnica]]
- [[04-Decisions/Decisions|Decisiones]]
- [[05-AI/AGENTS|Instrucciones para agentes]]
- [[06-History/Audit History|Historial]]

La auditoría es estática. No se han ejecutado compilación, pruebas, perfilado ni pentest.
