---
title: Current State
type: project-status
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: static-audit
tags: [launcher/project, launcher/status, launcher/audit]
---

# Estado actual

## Referencia
|Campo|Valor|
|---|---|
|Repositorio|ferranmi/Launcher-personal|
|Rama auditada|main|
|Commit|5c621accde912fc8dda48873fb44d4d93857901a|
|Versión|0.1.53|
|Framework|net10.0-windows10.0.19041.0|
|Plataforma|Windows|
|Validación|Inspección estática; build y ejecución no realizados|

## Tecnología y paquetes
WPF y Windows Forms habilitados; Nullable e ImplicitUsings habilitados; punto de entrada Launcher.Program. Paquetes declarados: Velopack 1.2.161, NAudio 2.2.1, System.Management 10.0.*, Microsoft.Playwright 1.*, System.Data.OleDb 9.0.0 y TagLibSharp 2.3.0. Declaración no implica uso efectivo.

## Registro real de módulos
MainWindow registra 13 módulos:
Dashboard, Games, Folders, Media, Music, Network, ImageGen, Models, Commands, Devices, Monitor, Processes y Settings.

Diferencias:
- `Modules/Search/SearchModule.cs` implementa IModule, pero no se registra.
- `Modules/Assistant/AssistantWindow.xaml.cs` existe; no hay AssistantModule ni registro IModule.
- Por tanto, la lista documental histórica de 14 módulos no coincide con la composición actual del sidebar.

## Orden de navegación
Dashboard -1; Games 0; Folders 1; Media 2; Music 3; Processes 4; Monitor 6; Network 7; Devices 7; ImageGen 7; Models 8; Commands 10; Settings 100. Los empates conservan dependencia del orden de inserción.

## Arquitectura observada
- Inicio personalizado en Program.cs ejecuta Velopack antes de WPF.
- App.xaml.cs controla instancia única, logging y excepciones globales.
- MainWindow registra módulos y mantiene caché de UserControl por ID.
- Contrato IModule expone Id, DisplayName, Icon, Order y CreateView().
- ModuleCatalog mantiene lista global ordenada.
- Core contiene interfaces, modelos, servicios, MVVM y converters.
- Módulos combinan servicios propios, vistas XAML y code-behind.

## Persistencia
El diseño usa archivos JSON bajo AppData. Se detectaron servicios con estrategias de escritura distintas; ver [[../03-Audit/Findings|Hallazgos]]. Algunos modelos no se persisten: MusicTrack representa metadatos leídos desde los propios archivos de audio.

## Pruebas y límites
No se ha ejecutado compilación, pruebas unitarias, aplicación WPF, instalador ni actualización. No se identificó proyecto de tests ni workflow CI en el árbol revisado; verificar si existe infraestructura externa antes de concluir que no hay pruebas.
