---
title: Roadmap
type: project-roadmap
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: proposed
tags: [launcher/project, launcher/roadmap, launcher/pending]
---

# Roadmap

Fuente auditada: `main` en `5c621accde912fc8dda48873fb44d4d93857901a`, versión `0.1.53`. Auditoría estática; no se ha compilado ni ejecutado la aplicación.

## Prioridades
- P1: seguridad, integridad de datos o estabilidad importante.
- P2: defecto relevante o hardening con impacto condicionado.
- P3: mejora menor, consistencia o deuda.

## Fase 1 — Seguridad
|ID|Tarea|Prioridad|Estado|
|---|---|---:|---|
|SEC-001|Migrar SteamApiKey de settings.json a Windows Credential Manager conservando migración|P2|Pendiente|
|SEC-002|Exigir HTTPS y validar el origen final del autologin web antes de introducir credenciales|P1|Pendiente|
|SEC-003|Definir autenticación/confianza TLS de LocalSend compatible con su protocolo|P2|Pendiente|
|SEC-004|Verificar hash o firma de yt-dlp y FFmpeg antes de ejecutar|P2|Pendiente|
|SEC-005|Validar GameId antes de formar rutas de GameInfoStore|P2|Por verificar|

## Fase 2 — Datos y fiabilidad
|ID|Tarea|Prioridad|Estado|
|---|---|---:|---|
|DATA-001|Estandarizar escritura JSON segura y tratamiento visible/registrado de errores|P1|Pendiente|
|DATA-002|Corregir comparación de rutas por prefijo en MusicLibraryService.ImportAsync|P2|Pendiente|
|DATA-003|Revisar creación de AppData y guardados de inicialización para tolerar permisos/disco|P1|Pendiente|
|CFG-001|Definir semántica de Reset para ConfirmKillProcess, MusicFolder y LastModule|P2|Decisión pendiente|

## Fase 3 — Ciclo de vida y rendimiento
|ID|Tarea|Prioridad|Estado|
|---|---|---:|---|
|LIFECYCLE-001|Cerrar sesiones Playwright, contextos y navegadores al cerrar Launcher|P1|Pendiente|
|CORE-004|Liberar MMDeviceEnumerator y MMDevice, incluido cambio de dispositivo|P2|Pendiente|
|PERF-001|Medir muestreo duplicado/síncrono entre SystemStatusHost y MonitorView|P2|Pendiente|
|CORE-005|Decidir si TopProcesses se implementa; corregir o retirar método recursivo desconectado|P2|Decisión pendiente|
|NET-001|Añadir timeout propio a cada upload LocalSend manteniendo cancelación|P3|Pendiente|

## Fase 4 — Producto y arquitectura
|ID|Tarea|Prioridad|Estado|
|---|---|---:|---|
|MOD-001|Decidir si Search es módulo oficial o herramienta auxiliar; existe SearchModule pero no se registra|P2|Pendiente|
|MOD-002|Decidir si Assistant es ventana auxiliar o módulo navegable|P2|Pendiente|
|MOD-003|Resolver empates de Order entre módulos|P3|Pendiente|
|MOD-004|Corregir claves de abreviatura que no coinciden con IDs|P3|Pendiente|
|DEP-001|Auditar uso de NuGet y fijar versiones flotantes si procede|P2|Pendiente|
|TEST-001|Definir pruebas y CI Windows cuando exista estrategia de tests|P2|Pendiente|

## Fase 5 — Validación
- [ ] Build Debug y Release en Windows con .NET 10.
- [ ] Probar JSON ausente, corrupto, truncado, bloqueado y sin permisos.
- [ ] Probar autologin con HTTPS, HTTP y redirección a otro host.
- [ ] Probar cierre de Launcher con navegador Playwright abierto.
- [ ] Probar cambio/desconexión de dispositivo de audio.
- [ ] Perfilar Dashboard/Monitor con ambos activos.
- [ ] Probar importación desde carpetas hermanas con prefijo común.
- [ ] Probar actualización instalada con Velopack.

Las tareas no pasan a implementacion sin aprobación explícita. Los hallazgos estáticos requieren validación dinámica cuando aplique.
