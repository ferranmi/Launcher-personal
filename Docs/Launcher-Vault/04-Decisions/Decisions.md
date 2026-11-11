---
title: Technical Decisions
type: decision-log
project: Launcher
status: pending-decisions
tags: [launcher/decisions, launcher/architecture]
---

# Decisiones técnicas

## Decisiones confirmadas
- Windows exclusivamente.
- C# / WPF / .NET 10.
- Sin framework MVVM externo.
- Datos locales JSON en %AppData%\Launcher, salvo datos asociados a archivos.
- Módulos implementan IModule y se registran en MainWindow.xaml.cs.
- Orden de construcción conceptual: modelo → servicio → módulo → vista.
- Mantener patrones existentes; no añadir dependencias ni refactorizar arquitectura sin justificación y aprobación.
- Datos ausentes/corruptos deben recurrir a defaults cuando sea razonable.
- Textos de interfaz sin tildes ni eñes.

## Decisiones pendientes

### DEC-001 — Search
SearchModule implementa IModule, pero MainWindow no lo registra. El catálogo documental anterior enumera 14 módulos incluyendo Assistant y omite Search. Decidir si Search es módulo oficial, auxiliar o legado.

### DEC-002 — Assistant
Existe AssistantWindow con OllamaService y AssistantMessage; no existe AssistantModule ni registro. Decidir si debe seguir como paleta/ventana, convertirse en módulo o disponer de ambos accesos.

### DEC-003 — Reset
Reset no reasigna ConfirmKillProcess. MusicFolder y LastModule tampoco se reasignan. Decidir qué ajustes se restauran y qué datos se preservan.

### DEC-004 — SteamApiKey
Se guarda en settings.json. Recomendación: migrar a Windows Credential Manager con lectura/migración del valor antiguo.

### DEC-005 — LocalSend
Decidir el modelo de confianza: compatibilidad LocalSend estándar frente a pairing/fingerprint y validación de certificado.

### DEC-006 — TopProcesses
Método recursivo desconectado del flujo actual. Decidir si la métrica se necesita o se retira.

Ninguna recomendación se considera decisión aprobada hasta que el propietario la confirme.
