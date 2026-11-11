---
title: Security Audit
type: security-audit
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-review
tags: [launcher/security, launcher/audit]
---

# Auditoría de seguridad

## Alcance
Revisión estática parcial de credenciales, automatización web, transferencias y descargas. No se ha ejecutado pentest ni inspeccionado el equipo Windows.

## Riesgos priorizados

### SEC-001 — Steam API key en JSON (P2)
`LibraryPathsService` serializa SteamApiKey en `%AppData%\Launcher\settings.json`. A diferencia de credenciales web gestionadas por `CredentialStore`, la clave queda en texto plano. Un proceso con permisos del usuario podría leerla. Recomendación: Windows Credential Manager, migración compatible y borrado del dato antiguo solo tras verificar el guardado nuevo.

### SEC-002 — Autologin web acepta HTTP y no fija origen (P1)
`WebLauncher.NormalizeUrl` acepta HTTP/HTTPS. `AutoLoginAsync` navega y después rellena campos visibles sin comprobar el origen final. URL HTTP o redirección a host inesperado puede exponer credenciales. Exigir HTTPS, asociar credencial a origen permitido y comprobar URL final antes de rellenar. No se ha demostrado explotación.

### SEC-003 — Certificado LocalSend no validado (P2)
`LocalSendService` usa `DangerousAcceptAnyServerCertificateValidator`, intenta HTTPS y luego HTTP. La IP Tailscale reduce exposición pero no autentica el dispositivo. Investigar pairing/fingerprint antes de modificar compatibilidad.

### SEC-004 — Integridad de herramientas externas (P2)
`ExternalToolsService` descarga yt-dlp de GitHub Releases y FFmpeg de gyan.dev. No se observó comprobación explícita de hash/firma. Fijar release y validar integridad antes de ejecutar.

### SEC-005 — GameId como segmento de ruta (P2, por verificar)
`GameInfoStore.PathFor` usa GameId en un nombre de archivo sin validación local visible. Los IDs suelen generarse internamente, pero proceden también de JSON local. Restringir formato y verificar que la ruta normalizada permanece bajo game-info. No se ha demostrado entrada remota.

## Controles positivos observados
- CredentialStore usa Windows Credential Manager para credenciales web.
- CommandsService elimina credencial asociada al borrar el comando.
- MusicDownloadService construye argumentos con ArgumentList.
- WindowsEventLogService lee System y Application; no Security.
- LocalSend apunta a peers Tailscale visibles en UI.

## Pruebas propuestas
- [ ] Comprobar ACL de AppData.
- [ ] Comprobar que claves no aparecen en logs.
- [ ] Probar autologin HTTP y redirección entre hosts con servidor de pruebas.
- [ ] Comprobar rechazo de origen distinto.
- [ ] Probar certificado no confiable en LocalSend.
- [ ] Verificar hash/firma de binarios.
- [ ] Probar GameId con separadores y segmentos de ruta.
- [ ] Revisar todos los argumentos de procesos externos.
