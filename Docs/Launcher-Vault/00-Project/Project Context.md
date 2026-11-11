---
title: Project Context
type: project-context
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: verified-static
tags: [launcher/project, launcher/context]
---

# Contexto del proyecto

## Identificación
Launcher es una aplicación de escritorio Windows desarrollada en C# con WPF sobre .NET 10. Versión del snapshot: 0.1.53. Repositorio: [ferranmi/Launcher-personal](https://github.com/ferranmi/Launcher-personal).

## Propósito
Centralizar en una interfaz modular herramientas del equipo: juegos, carpetas, multimedia, comandos, monitorización, procesos, dispositivos, red, generación de imágenes y gestión de modelos.

El propósito no implica que cada idea futura esté terminada o integrada.

## Restricciones de producto y desarrollo
- Windows exclusivamente.
- C# / WPF / .NET 10.
- Sin frameworks MVVM externos.
- Datos JSON locales en %AppData%\Launcher, según servicios que lo implementan.
- Los módulos oficiales implementan IModule y se registran en MainWindow.xaml.cs.
- Construcción conceptual: modelo → servicio → módulo → vista.
- Mantener patrones existentes; no refactorizar ni añadir dependencias sin justificación y aprobación.
- Los archivos de datos ausentes/corruptos deben gestionarse con defaults cuando sea razonable.
- UI en español; textos de interfaz sin tildes ni eñes.
- No afirmar compilación/funcionamiento sin pruebas.

## Estructura principal
- `Core/`: Interfaces, Models, Mvvm, Converters y Services.
- `Modules/`: funcionalidades y vistas.
- `UI/`: controles y temas.
- Raíz: Program.cs, App.xaml, MainWindow.xaml, Launcher.csproj, manifiesto y script de publicación.
- `Docs/Launcher-Vault/`: documentación Obsidian.

## Módulos y elementos auxiliares
Los 13 módulos registrados aparecen en [[Current State|Estado actual]]. SearchModule existe sin registro. Assistant existe como ventana independiente y no implementa IModule. Mantener esta distinción hasta decisión del propietario.

## Fuente de verdad
1. Código del commit auditado para describir implementación.
2. Configuración efectiva del proyecto.
3. Pruebas ejecutadas para demostrar comportamiento.
4. Historial Git.
5. Obsidian para decisiones, contexto, auditoría y roadmap.
6. README como guía de entrada, subordinada al código actual.

## Límites de la auditoría actual
Se inspeccionó estáticamente el árbol y componentes seleccionados. No se ejecutó build, WPF, pruebas, perfilado ni pentest. Las conclusiones dinámicas quedan pendientes.
