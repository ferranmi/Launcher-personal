---
title: Module Instructions
type: ai-guidelines
project: Launcher
status: active
tags: [launcher/ai, launcher/modules]
---

# Instrucciones para trabajar en módulos

## Antes de modificar
1. Leer ficha del módulo y decisiones abiertas.
2. Inspeccionar IModule y contratos relacionados.
3. Identificar modelo, servicio, módulo, vista y code-behind.
4. Buscar consumidores, eventos y servicios compartidos.
5. Revisar persistencia y compatibilidad JSON.
6. Identificar ownership y ciclo de vida de timers, procesos, COM y ventanas.
7. Definir criterios de aceptación y pruebas.
8. Obtener aprobación antes de implementar.

## Orden de construcción
Modelo → Servicio → Módulo → Vista. No crear ViewModel adicional por defecto; mantener MVVM ligero y code-behind donde ya existe.

## Registro
Cada módulo oficial necesita ID estable/único, nombre, icono, orden y CreateView(). Registrar en MainWindow.xaml.cs. Comprobar ModuleCatalog y Settings. Resolver primero si el elemento es módulo oficial o auxiliar.

## Persistencia
- Identificar modelo y archivo serializado.
- Mantener compatibilidad con JSON existente.
- Recuperarse de JSON ausente/corrupto.
- Evitar escrituras parciales.
- No guardar secretos en JSON.
- No silenciar fallos de guardado sin dejar evidencia.

## Interfaz
- Tema oscuro/acento verde y DynamicResource.
- Textos visibles sin tildes ni eñes.
- No bloquear hilo UI con disco, WMI, red o procesos.
- Mostrar carga, error, cancelación y resultado.

## Ciclo de vida
Documentar quién crea/libera cada recurso, quién cancela tareas, detiene timers, desuscribe eventos, cierra procesos y dispone objetos IDisposable/COM.

## Pruebas mínimas
Primera ejecución; JSON corrupto; valores nulos; rutas no disponibles; permisos denegados; cancelación; dispositivo desconectado; navegación repetida; cierre durante operaciones; build Debug/Release en Windows.
