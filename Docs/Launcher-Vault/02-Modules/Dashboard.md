---
title: "Dashboard"
type: module-reference
project: Launcher
source_of_truth: feature/dashboard-cpu-gpu-thermal
last_reviewed: 2026-11-13
status: implemented-unverified
tags: [launcher, module, dashboard, monitoring]
---

# Dashboard

## Responsabilidad

Mostrar de un vistazo el estado del sistema, métricas, procesos, juegos, accesos rápidos, eventos, UPLINK y multimedia. El usuario puede mover y redimensionar las tarjetas existentes; el diseño se guarda con la configuración del tablero.

## Monitorización y estados

- `SystemStatusHost` es el único propietario del muestreo de sistema, procesos, UPLINK, evaluación de estados y eventos de Windows.
- Dashboard consume la última muestra. Cuando está visible, el intervalo usa `StatusIntervalSeconds`; al mostrar otro módulo baja a 3 s y con Launcher oculto o minimizado a 10 s.
- `StatusEvaluator` mantiene estados por métrica y un estado global: `ONLINE`, `WARNING` o `DEFCON`. Los perfiles `relaxed`, `normal` y `strict` cambian sus umbrales.
- Un dispositivo UPLINK con estado de problema eleva el global a `WARNING`. Una desconexión normal queda en el registro como evento informativo.
- El indicador global usa color y texto; pulsa en `WARNING` y con mayor rapidez en `DEFCON`. No depende solo del color.

## Métricas visibles

- CPU y GPU muestran porcentaje y temperatura en la primera línea; la segunda muestra el modelo. CPU añade núcleos e hilos en formato compacto.
- Las temperaturas se leen con LibreHardwareMonitor. Si el sensor no está disponible, se muestra `--°C` y el Dashboard sigue funcionando.
- RAM y VRAM conservan su presentación actual.
- Las tarjetas, sus claves y sus posiciones por defecto se mantienen.

## Efectos visuales

La opción `DashboardEffects` de Settings controla la rejilla, el resplandor superior, la viñeta y las scanlines del Dashboard. Si se desactiva, oculta esas capas; no cambia los datos ni la distribución.

La opción D de fondos sigue pendiente: fondos incluidos, imágenes propias, asociación automática con el tema y selección manual. Las capas vectoriales actuales no constituyen esa galería de fondos.

## Validación pendiente

La rama está revisada estáticamente, pero sigue pendiente de compilar y probar en Windows:

- Verificar lecturas térmicas con sensores disponibles y no disponibles.
- Comprobar el refresco y las alertas de métricas, UPLINK y eventos de Windows.
- Probar los tamaños pequeños de CPU/GPU y confirmar que el porcentaje sigue legible.
- Cambiar `DashboardEffects` desde Settings y confirmar que las cuatro capas se muestran u ocultan tras Guardar.

## Documentación relacionada

- [[MONITOR]]
- [[GAMES]]
- [[PROCESSES]]
- [[Settings]]
