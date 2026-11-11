---
title: Quality and Reliability
type: quality-audit
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/quality, launcher/audit]
---

# Calidad, fiabilidad y rendimiento

## Hallazgos

- **DATA-001:** varios servicios guardan JSON directamente con File.WriteAllText y tienen políticas de excepción diferentes. AppSettingsService dispara Changed incluso si falla la escritura.
- **DATA-002:** MusicLibraryService.ImportAsync compara rutas mediante StartsWith y confunde directorios hermanos con prefijo compartido.
- **LIFECYCLE-001:** WebLauncher conserva Playwright/browser/context en lista estática; no se encontró cierre centralizado en los cierres inspeccionados.
- **PERF-001:** SystemStatusHost y MonitorView usan instancias y DispatcherTimer distintos; el muestreo puede duplicarse y ejecutarse en hilo UI. Impacto por medir.
- **CORE-004:** AudioMixerService no implementa IDisposable para MMDeviceEnumerator/MMDevice ni libera el dispositivo anterior al cambiar.
- **CORE-005:** SampleTopProcesses contiene recursión directa cuando IncludeTopProcesses=true; Sample() no llama al método en el flujo inspeccionado.
- **CFG-001:** Reset no restablece ConfirmKillProcess; MusicFolder/LastModule podrían conservarse intencionadamente.
- **NET-001:** HttpClient LocalSend usa timeout infinito; prepare tiene 3 min y upload depende de cancelación manual.

## Prácticas positivas observadas
- GameInfoStore.Load recupera datos ausentes/nulos y devuelve defaults ante JSON no legible.
- MusicLibraryService tolera errores por archivo al leer la biblioteca.
- ThumbnailService limita concurrencia con SemaphoreSlim y soporta cancelación.
- ShellThumbnailService libera HBITMAP y objeto COM en finally.
- UplinkService implementa IDisposable y libera contadores/eventos.
- MusicDownloadService usa ProcessStartInfo.ArgumentList.

## Validación pendiente
- [ ] JSON ausente, corrupto, truncado, solo lectura y sin espacio.
- [ ] Cerrar Launcher con Playwright abierto.
- [ ] Cambiar/desconectar dispositivo de audio.
- [ ] Medir UI y CPU con Dashboard y Monitor activos.
- [ ] Importar desde C:\Musica-Externa a C:\Musica.
- [ ] Transferencia LocalSend interrumpida.
- [ ] Reset de todos los ajustes.
- [ ] Build Debug/Release en Windows.

No se ha ejecutado compilación, pruebas, perfilado ni validación dinámica.
