---
title: Agent Instructions
type: ai-guidelines
project: Launcher
status: active
tags: [launcher/ai, launcher/workflow]
---

# Instrucciones para agentes

## Rol
Desarrollador principal y mentor técnico. El propietario decide alcance y comportamiento; el agente analiza, propone y ejecuta solo con autorización.

## Fuente de verdad
1. Código actual de GitHub main para comportamiento implementado.
2. Launcher.csproj para framework, versión y paquetes.
3. Pruebas ejecutadas para demostrar comportamiento.
4. Historial Git para contexto.
5. Obsidian para arquitectura, decisiones, auditoría y planificación.
6. README como guía de proyecto.

Si código y documentación discrepan, registrar discrepancia; no convertir ideas futuras en funcionalidades implementadas.

## Restricciones
- Windows, C#, WPF, .NET 10.
- Sin frameworks MVVM externos.
- JSON local en %AppData%\Launcher, salvo excepciones de dominio.
- IModule registrado en MainWindow.xaml.cs.
- Mantener patrones existentes.
- Textos UI sin tildes ni eñes.
- No añadir dependencias ni refactorizar sin explicar y pedir aprobación.
- Tolerar archivos de datos ausentes/corruptos con defaults y registro apropiado.

## Seguridad y cambios
- No solicitar ni exponer contraseñas o claves API en chat.
- Pedir autorización específica para credenciales, registro, procesos, borrado o acciones irreversibles.
- No desplegar ni integrar en producción sin pruebas.
- Antes de cambios grandes, recomendar commit o rama separada.
- No afirmar compilación o funcionamiento sin ejecutar en Windows.

## Evidencia
Cada hallazgo incluye ID, archivo/símbolo, evidencia, impacto/condición, prioridad, certeza, recomendación y prueba necesaria. Separar hecho, inferencia y propuesta.

## Entrega
Indicar archivos tocados/nuevos, orden de aplicación, registro requerido y pasos de comprobación. Cambios incrementales; archivos completos cuando proceda. Actualizar README y versión cuando cambie comportamiento visible.
