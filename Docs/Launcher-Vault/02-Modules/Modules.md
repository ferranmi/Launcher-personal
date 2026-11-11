---
title: Module Catalog
type: module-index
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: reconciled
tags: [launcher/modules, launcher/architecture]
---

# Catálogo de módulos

## Registro real en MainWindow
|Orden|Nombre|ID|Clase|
|---:|---|---|---|
|-1|Inicio|dashboard|DashboardModule|
|0|Juegos|games|GamesModule|
|1|Carpetas|folders|FoldersModule|
|2|Multimedia|media|MediaModule|
|3|Musica|music|MusicModule|
|4|Procesos|processes|ProcessesModule|
|6|Monitor|monitor|MonitorModule|
|7|Red|network|NetworkModule|
|7|Dispositivos|devices|DevicesModule|
|7|Imagenes IA|imagegen|ImageGenModule|
|8|Modelos|models|ModelsModule|
|10|Comandos|commands|CommandsModule|
|100|Configuracion|settings|SettingsModule|

## Elementos no registrados
- **Search:** `Modules/Search/SearchModule.cs` implementa IModule y tiene SearchView, pero MainWindow no lo registra.
- **Assistant:** existe `AssistantWindow`, usa OllamaService y AssistantMessage; no existe AssistantModule registrado.

No se debe mantener la cifra histórica de 14 módulos sin explicar estas diferencias. El código registra 13.

## Fichas
- [[Assistant|Assistant]]
- [[Commands|Commands]]
- [[Dashboard|Dashboard]]
- [[Devices|Devices]]
- [[Folders|Folders]]
- [[Games|Games]]
- [[Imagegen|ImageGen]]
- [[Media|Media]]
- [[Models|Models]]
- [[Monitor|Monitor]]
- [[Music|Music]]
- [[Network|Network]]
- [[Processes|Processes]]
- [[Settings|Settings]]

## Criterio
Una carpeta no demuestra un módulo registrado; una clase que implementa IModule tampoco lo convierte en módulo visible si MainWindow no la añade. Cada ficha separa implementación, registro, comportamiento comprobado y pruebas pendientes.
