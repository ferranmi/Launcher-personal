---
title: Audit Findings
type: audit-findings
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/audit, launcher/findings]
---

# Hallazgos

## Alcance y certeza

Inspección estática de la rama main, commit `5c621accde912fc8dda48873fb44d4d93857901a`, versión `0.1.53`. No se ejecutó build, WPF, perfilado ni pentest. "Confirmado" significa que el patrón está en el código, no que se haya reproducido su impacto.

|ID|Hallazgo|Prioridad|Certeza|
|---|---|---:|---|
|SEC-001|SteamApiKey serializada en JSON|P2|Código confirmado|
|SEC-002|Autologin acepta HTTP y no fija origen final|P1|Código confirmado; impacto condicionado|
|SEC-003|LocalSend omite validación de certificado|P2|Código confirmado|
|SEC-004|Descargas externas sin hash/firma observados|P2|Hardening|
|DATA-001|Escritura JSON directa y tratamiento dispar|P1/P2|Código confirmado en servicios revisados|
|DATA-002|Comparación de rutas mediante StartsWith|P2|Algoritmo defectuoso|
|LIFECYCLE-001|Sesiones Playwright sin cierre explícito visible|P1/P2|Código confirmado en rutas revisadas|
|PERF-001|Muestreo de monitor duplicado y desde DispatcherTimer|P2|Arquitectura confirmada; impacto por medir|
|CORE-004|Recursos CoreAudio sin Dispose explícito|P2|Código confirmado|
|CORE-005|Recursión dentro de SampleTopProcesses|P2|Código confirmado; método no conectado|
|CFG-001|Reset no reasigna ConfirmKillProcess|P2|Omisión confirmada; intención pendiente|
|NET-001|Upload LocalSend sin timeout propio|P3|Código confirmado; cancelación manual|
|MOD-001|Search implementado pero no registrado; Assistant sin IModule|P2|Confirmado|
|MOD-002|Órdenes Order duplicadas|P3|Confirmado|
|MOD-003|Claves de abreviatura no coinciden con IDs|P3|Confirmado|

## SEC-001 — SteamApiKey en texto plano
**Archivos:** `Core/Services/LibraryPathsService.cs`, `Modules/Games/SteamAccountWindow.xaml.cs`. La clave se serializa junto a las rutas/SteamId en `%AppData%\Launcher\settings.json`. Existe `CredentialStore` con Windows Credential Manager, pero SteamApiKey no lo utiliza. Procesos con permisos del usuario pueden leer el archivo; no equivale a acceso universal a otros usuarios. Migrar con Credential Manager y conservar el valor antiguo hasta confirmar la migración.

## SEC-002 — Autologin web sin HTTPS/origen fijado
**Archivo:** `Core/Services/WebLauncher.cs`. `NormalizeUrl` admite http y https; tras navegar, `AutoLoginAsync` llama a `FillLoginAsync` sin comparar el origen final con el configurado. Una redirección o URL HTTP puede exponer credenciales. Exigir HTTPS en autologin, guardar origen permitido y validar antes de rellenar y tras cada navegación. No se ha demostrado explotación remota.

## SEC-003 — TLS LocalSend
**Archivo:** `Core/Services/LocalSendService.cs`. Usa `DangerousAcceptAnyServerCertificateValidator` y prueba HTTPS antes que HTTP. La dirección Tailscale restringe alcance, pero no autentica por sí sola la identidad del receptor. Investigar pairing/fingerprint del protocolo antes de cambiar comportamiento.

## SEC-004 — Integridad de binarios externos
**Archivo:** `Core/Services/ExternalToolsService.cs`. Descarga yt-dlp de GitHub Releases y FFmpeg de gyan.dev; no se observó verificación explícita de hash/firma en el flujo revisado. Añadir verificación publicada por la fuente, fijar releases y validar extracción. No hay evidencia de malware.

## DATA-001 — Escritura JSON
Servicios revisados: AppSettingsService, LibraryPathsService, FolderShortcutsService, MediaAppsService, GameLibraryService y CommandsService. Hay escrituras directas con File.WriteAllText; algunas excepciones se silencian y otras se propagan. AppSettingsService notifica Changed incluso si el guardado falla. Puede haber datos truncados ante interrupción y estado UI/disco divergente. Diseñar guardado temporal/reemplazo seguro y resultado de guardado coherente.

## DATA-002 — Rutas por prefijo
**Archivo:** `Core/Services/MusicLibraryService.cs`, `ImportAsync`. Compara rutas completas con `StartsWith`; una carpeta hermana como `C:\Musica-Externa` puede pasar por dentro de `C:\Musica`. Usar comparación con límite de directorio y probar raíces, separadores y mayúsculas.

## LIFECYCLE-001 — Playwright
**Archivos:** `Core/Services/WebLauncher.cs`, `MainWindow.xaml.cs`. Una lista estática conserva Playwright, contextos y browser; no se encontró método de limpieza en las rutas de cierre inspeccionadas. Puede dejar Chromium/perfiles vivos. Revisar todos los cierres y definir apagado asíncrono.

## PERF-001 — Monitor
**Archivos:** `SystemStatusHost.cs`, `SystemMonitorService.cs`, `MonitorView.xaml.cs`. Host y vista crean servicios/temporizadores distintos; Sample recoge CPU, RAM, GPU, disco y red. DispatcherTimer ejecuta su callback en Dispatcher. Medir bloqueos y consultas duplicadas antes de refactorizar.

## CORE-004 — Audio
**Archivo:** `AudioMixerService.cs`. Crea MMDeviceEnumerator y mantiene MMDevice; al cambiar dispositivo sustituye la referencia anterior. No implementa IDisposable ni libera explícitamente los recursos COM. Definir ownership y liberar en cambio/cierre.

## CORE-005 — TopProcesses
**Archivo:** `SystemMonitorService.cs`. `SampleTopProcesses()` se llama a sí mismo cuando IncludeTopProcesses es true. El Sample() inspeccionado no llama al método, por lo que no se afirma que el monitor normal produzca StackOverflow. Decidir si la métrica se necesita y corregir/retirar código desconectado.

## CFG-001 — Reset
**Archivo:** `AppSettingsService.cs`. Reset no reasigna ConfirmKillProcess, pese a que la propiedad existe y Settings la muestra. MusicFolder y LastModule tampoco se restablecen; pueden ser datos preservados deliberadamente. Definir contrato de Reset.

## NET-001 — LocalSend
**Archivo:** `LocalSendService.cs`. HttpClient tiene timeout infinito; prepare-upload tiene 3 minutos, pero cada upload usa solo el token de cancelación. La UI permite cancelar manualmente. Añadir timeout específico por transferencia.

## MOD-001 — Catálogo/registro
MainWindow registra 13 módulos: Dashboard, Games, Folders, Media, Music, Network, ImageGen, Models, Commands, Devices, Monitor, Processes y Settings. SearchModule implementa IModule pero no está registrado. Assistant tiene AssistantWindow y usa OllamaService, pero no existe AssistantModule registrado. El catálogo documental anterior declaraba 14 módulos incluyendo Assistant y excluyendo Search. Resolver intención, no cambiarlo automáticamente.

## MOD-002 y MOD-003
Network, Devices e ImageGen comparten Order=7; Processes y el antiguo Search usan Order=4. En MainWindow las claves "Network" e "image generation" no coinciden con los IDs "network" e "imagegen", por lo que el fallback de abreviatura se usa en esos casos.

## Pendiente de inspección
- Ciclo de vida de GlobalHotkeyService/WindowBehaviorService.
- Uso real y versionado de cada paquete NuGet.
- Todas las vistas XAML/code-behind y consumidores de servicios.
- Validación de rutas/argumentos en cada flujo de proceso.
- Compilación y pruebas Windows.
