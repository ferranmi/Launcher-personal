---
title: "Current State"
type: project-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, documentation]
---

# Current State

## 1. Propósito

Registrar el estado conocido del proyecto Launcher en la rama `main`.

Este documento diferencia:

- Hechos comprobados directamente en GitHub.
- Información declarada en el README.
- Información pendiente de contrastar.
- Problemas identificados que todavía necesitan validación.

No debe utilizarse como una lista de funcionalidades garantizadas.

## 2. Referencia del análisis

| Campo                                    | Valor                                                                                          |
| ---------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Repositorio                              | [https://github.com/ferranmi/Launcher-personal](https://github.com/ferranmi/Launcher-personal) |
| Rama                                     | `main`                                                                                         |
| Fecha de revisión                        | 2026-11-11                                                                                     |
| Versión de Launcher.csproj               | `0.1.52`                                                                                       |
| Target Framework                         | `net10.0-windows10.0.19041.0`                                                                  |
| Plataforma                               | Windows                                                                                        |
| Última referencia de historial observada | 34 commits                                                                                     |
| Estado de auditoría                      | Inicial                                                                                        |

La rama puede cambiar después de esta revisión. Para una auditoría reproducible se deberá registrar también el SHA completo del commit analizado.

## 3. Estado tecnológico

### Comprobado en Launcher.csproj

- Aplicación .NET.
- Lenguaje C#.
- WPF habilitado.
- Windows Forms habilitado.
- Target Framework `net10.0-windows10.0.19041.0`.
- Nullable habilitado.
- Implicit Usings habilitado.
- Manifiesto `app.manifest`.
- Punto de entrada `Launcher.Program`.
- Icono de aplicación `Assets/launcher.ico`.

### Dependencias declaradas

|Dependencia|Versión declarada|
|---|---|
|Velopack|1.2.161|
|NAudio|2.2.1|
|System.Management|10.0.*|
|Microsoft.Playwright|1.*|
|System.Data.OleDb|9.0.0|
|TagLibSharp|2.3.0|

La presencia de una dependencia no demuestra su utilización efectiva.

Pendiente: identificar referencias, servicios consumidores, funcionalidad asociada y necesidad de cada paquete.

## 4. Estado de los 14 módulos funcionales

La implementación de MainWindow.xaml.cs registra 13 módulos. Assistant no aparece en el registro actual inspeccionado, aunque permanece dentro del alcance funcional oficial de 14 módulos.

Los 14 módulos forman parte del alcance oficial del proyecto y deben permanecer en el inventario aunque no estén implementados completamente.

La siguiente tabla establece el estado de conocimiento, no una certificación de implementación.

|Módulo|ID|Estado de auditoría|
|---|---|---|
|Assistant|`assistant`|Por verificar|
|Commands|`commands`|Por verificar|
|Dashboard|`dashboard`|Por verificar|
|Devices|`devices`|Por verificar|
|Folders|`folders`|Por verificar|
|Games|`games`|Por verificar|
|ImageGen|`imagegen`|Por verificar|
|Media|`media`|Por verificar|
|Models|`models`|Por verificar|
|Monitor|`monitor`|Por verificar|
|Music|`music`|Por verificar|
|Network|`network`|Por verificar|
|Processes|`processes`|Por verificar|
|Settings|`settings`|Por verificar|

### Información declarada en el README

El README describe como funcionalidades implementadas en el snapshot que documenta:

- Games.
- Folders.
- Media.
- Commands.
- Monitor.
- Processes.
- Settings.

También describe áreas parciales, pendientes o no consolidadas, entre ellas Assistant, Dashboard, Music e ImageGen.

**Advertencia:** el propio README contiene una advertencia de divergencia entre el snapshot que documenta y la evolución del proyecto. Por tanto, esta clasificación es una referencia histórica que debe contrastarse con el código actual de `main`.

No utilizaremos dicha clasificación para asignar automáticamente el estado actual de los 14 módulos.

## 5. Estado de arquitectura

### Elementos identificados en la documentación del repositorio

- Aplicación WPF.
- Separación general entre Core, Modules y UI.
- Contrato modular denominado `IModule`.
- Catálogo de módulos denominado `ModuleCatalog`.
- Componentes MVVM propios.
- Recursos XAML y tema visual.
- Servicios centrales.
- Ventana principal como punto de integración.

Pendiente de verificar en el código actual:

- Registro real de los 14 módulos.
- Interfaces y contratos definitivos.
- Dependencias entre módulos.
- Servicios compartidos.
- Ciclo de vida de las vistas.
- Persistencia y configuración.
- Integración de Assistant, ImageGen y Models.
- Separación entre lógica de negocio y presentación.

## 6. Configuración y distribución

Elementos identificados en la raíz:

- `Launcher.csproj`
- `Program.cs`
- `App.xaml`
- `App.xaml.cs`
- `app.manifest`
- `publish.ps1`

El proyecto declara Velopack como dependencia y establece un punto de entrada personalizado.

Pendiente de verificar:

- Compilación Release.
- Publicación Windows x64.
- Instalación limpia.
- Actualización desde una versión anterior.
- Gestión de errores durante la actualización.
- Comportamiento de la instancia única.
- Inicio con Windows.
- Firma y distribución del ejecutable.

## 7. Persistencia y datos

El README describe una persistencia basada en JSON y almacenamiento en el perfil del usuario.

No se considera todavía verificada la totalidad de esa descripción.

Pendiente:

- Inventario de servicios de persistencia.
- Ubicación real de los datos.
- Modelos serializados.
- Compatibilidad entre versiones.
- Migraciones.
- Recuperación de datos corruptos.
- Copias de seguridad.
- Gestión de errores de lectura y escritura.

## 8. Pruebas y validación

Estado actual de la auditoría:

|Comprobación|Estado|
|---|---|
|Existencia de proyectos de test|Pendiente|
|Framework de testing|Pendiente|
|Tests unitarios|Pendiente|
|Tests de integración|Pendiente|
|Compilación|No ejecutada|
|Ejecución de WPF|No ejecutada|
|Pruebas de publicación|No ejecutadas|
|Pruebas de actualización|No ejecutadas|

La ausencia de pruebas en la documentación no demuestra que no existan. Hay que buscar proyectos, archivos de test y configuraciones de CI.

## 9. Hallazgos iniciales

### DOC-001 — Diferencia de versión documental

- Categoría: Documentación.
- Prioridad: P2.
- Evidencia: `Launcher.csproj` declara `0.1.53`; README declara `0.1.0`.
- Impacto: documentación potencialmente desactualizada.
- Estado: confirmado.
- Acción: actualizar el README cuando se complete la auditoría de estado.

### DOC-002 — Advertencia de divergencia en README

- Categoría: Documentación.
- Prioridad: P1.
- Evidencia: README indica que el snapshot que documenta no coincide con la memoria histórica del proyecto.
- Impacto: riesgo de utilizar descripciones obsoletas para modificar código.
- Estado: advertencia documental confirmada.
- Acción: reconstruir el estado funcional desde `main`.

### AUD-001 — Estado real de los 14 módulos

- Categoría: Auditoría funcional.
- Prioridad: P1.
- Estado: pendiente.
- Acción: inspeccionar registro, contratos, implementaciones, vistas y servicios.

### AUD-002 — Compilación y pruebas

- Categoría: Fiabilidad.
- Prioridad: P1.
- Estado: pendiente.
- Acción: validar en entorno Windows con .NET 10.

## 10. Próximas actividades

1. Completar el mapa recursivo de archivos.
2. Inspeccionar `MainWindow.xaml.cs` y `Program.cs`.
3. Identificar el registro efectivo de módulos.
4. Inspeccionar las carpetas de Core y Modules.
5. Reconstruir dependencias.
6. Identificar proyectos y pruebas.
7. Verificar persistencia.
8. Completar el estado individual de los 14 módulos.
9. Actualizar este documento con evidencias.

## 11. Documentos relacionados

- [[../../Home|Home]]
- [[PROJECT_CONTEXT|Project Context]]
- [[ROADMAP|Roadmap]]
- [[../01-Architecture/ARCHITECTURE|Architecture]]
- [[../01-Architecture/DIRECTORY_MAP|Directory Map]]
- [[../02-Modules/MODULES|Module Catalog]]
- [[../03-Audit/Findings|Audit Findings]]

## 12. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Creación inicial del estado del proyecto|
|Pendiente|Verificación recursiva del código|
|Pendiente|Clasificación definitiva de los 14 módulos|

## Actualización de auditoría estática — 2026-11-11

> Esta sección prevalece sobre cifras y estados históricos cuando exista discrepancia. Snapshot: `main` en `5c621accde912fc8dda48873fb44d4d93857901a`, versión `0.1.53`.

- MainWindow registra 13 módulos: Dashboard, Games, Folders, Media, Music, Network, ImageGen, Models, Commands, Devices, Monitor, Processes y Settings.
- SearchModule implementa IModule, pero no se registra. AssistantWindow existe como ventana independiente; no hay AssistantModule registrado.
- Launcher.csproj declara Velopack 1.2.161; cualquier afirmación contraria en material histórico está desactualizada.
- Auditoría estática: no se ejecutaron build, pruebas ni perfilado. Ver [[../03-Audit/Findings|Findings]].
