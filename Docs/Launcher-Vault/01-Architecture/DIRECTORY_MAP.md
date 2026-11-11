---
title: Directory Map
type: technical-reference
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: inventory
tags: [launcher/architecture, launcher/files]
---

# Mapa de directorios

## Raíz
- `Program.cs`: punto de entrada propio y arranque Velopack.
- `App.xaml(.cs)`: Application WPF, instancia única y gestión global de errores.
- `MainWindow.xaml(.cs)`: shell, registro y navegación de módulos.
- `Launcher.csproj`: versión, target framework, opciones y NuGet.
- `publish.ps1`: publicación.
- `Assets/`: recursos.
- `Core/`: contratos, modelos, servicios y MVVM.
- `Modules/`: funcionalidades y vistas.
- `UI/`: temas y controles.
- `Docs/Launcher-Vault/`: bóveda Obsidian.

## Core
- Interfaces: IModule, IGameProvider.
- Models: 25 archivos de modelos.
- Mvvm: ViewModelBase y RelayCommand.
- Services: servicios de configuración, sistema, audio, red, credenciales, actualización y otros.
- Converters: conversores para XAML.

## Modules
- Dashboard, Devices, Commands, Folders, Games, ImageGen, Media, Models, Monitor, Music, Network, Processes, Settings.
- Assistant contiene AssistantWindow, no un módulo IModule.
- Search contiene SearchModule/SearchView, pero no se registra en MainWindow.

## UI
Controles y recursos XAML compartidos. Consultar árbol del commit para inventario exacto; este documento es mapa funcional, no listado de cada archivo.

## Exclusiones y cautelas
No inferir que ZIP, PATCH o directorios auxiliares son código activo sin comparar su contenido con main. No considerar una carpeta .gitkeep como implementación funcional.
