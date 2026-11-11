---
title: "Search"
type: module-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: implemented-not-registered
tags: [launcher, module, audit]
---

# Search

## Estado de integración
Existe `Modules/Search/SearchModule.cs` y `SearchView.xaml`; SearchModule implementa IModule, pero no aparece en la lista registrada en MainWindow. Por tanto, no forma parte de la navegación visible en el snapshot auditado.

## Componentes observados
- SearchModule y SearchView.
- SearchService ejecuta proveedores en paralelo y combina resultados por puntuación.
- Proveedores: rutas, juegos, comandos, carpetas, aplicaciones, archivos y webs.
- SearchView aplica debounce de 150 ms, cancelación de búsquedas anteriores, navegación por teclado y ejecución/mostrar en carpeta.

## Decisión pendiente
Determinar si Search debe incorporarse al sidebar o mantenerse como funcionalidad auxiliar. No registrar automáticamente sin decisión del propietario.

## Auditoría pendiente
- [ ] Revisar proveedores individualmente, especialmente rutas y resultados web.
- [ ] Probar cancelación y búsquedas simultáneas.
- [ ] Verificar que las acciones Execute/Reveal validan rutas y capturan errores.
- [ ] Añadir pruebas de orden, puntuación y límite de 30 resultados.
