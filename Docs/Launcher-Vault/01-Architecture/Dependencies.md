---
title: Dependencies
type: technical-reference
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: static-inventory
tags: [launcher/architecture, launcher/dependencies]
---

# Dependencias

## Configuración
Fuente: Launcher.csproj. Framework: net10.0-windows10.0.19041.0. WPF y Windows Forms habilitados. Nullable e ImplicitUsings habilitados.

## Paquetes NuGet declarados
|Paquete|Versión|Área observada/probable|
|---|---|---|
|Velopack|1.2.161|Arranque y actualización|
|NAudio|2.2.1|Audio/CoreAudio|
|System.Management|10.0.*|WMI/dispositivos|
|Microsoft.Playwright|1.*|Autologin web|
|System.Data.OleDb|9.0.0|Uso efectivo por identificar|
|TagLibSharp|2.3.0|Metadatos de audio|

System.Management y Playwright usan versiones flotantes. La presencia de un paquete no prueba uso completo ni necesidad. No se han auditado vulnerabilidades transitivas.

## Dependencias internas
- IModule: contrato de navegación.
- ModuleCatalog: catálogo global estático.
- AppSettingsService: preferencias.
- LibraryPathsService: rutas Steam y clave API.
- CredentialStore: Windows Credential Manager para credenciales web.
- UpdateService: Velopack.
- OllamaService: API local Ollama.
- LocalSendService: cliente protocolo LocalSend.
- SystemMonitorService/SystemStatusHost: métricas y estado.
- AudioMixerService/NowPlayingService: audio y reproducción.

## Módulos
La lista registrada está en [[../00-Project/Current State|Current State]]. Los módulos utilizan servicios compartidos y servicios propios. No se ha construido todavía un grafo de dependencias de compilación exhaustivo.

## Riesgos
- Versiones flotantes: restauraciones pueden resolver versiones diferentes.
- Microsoft.Playwright implica navegador/procesos externos.
- System.Data.OleDb puede depender de proveedores nativos; uso por localizar.
- NAudio/COM requieren ciclo de vida correcto.
- Servicios globales y estáticos pueden dificultar aislamiento de pruebas.

## Acciones
- [ ] Buscar consumidores exactos de cada paquete.
- [ ] Revisar dependencias transitivas y vulnerabilidades.
- [ ] Decidir si fijar versiones flotantes.
- [ ] Verificar compatibilidad x64 de OLE DB si se usa.
- [ ] Generar grafo real de referencias entre proyectos/clases.
