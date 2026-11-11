---
title: "Dependencies"
type: architecture-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, architecture, audit]
---

# Dependencies

## 1. Objetivo

Documentar las dependencias de Launcher:

- Paquetes externos.
- Componentes internos.
- Servicios compartidos.
- Relaciones entre módulos.
- Dependencias tecnológicas.
- Riesgos de acoplamiento.

El objetivo es comprender qué necesita cada componente para funcionar y qué impacto tendría modificarlo o eliminarlo.

## 2. Configuración de referencia

Archivo: `Launcher.csproj`

|Propiedad|Valor|
|---|---|
|Versión|0.1.53|
|Framework|net10.0-windows10.0.19041.0|
|WPF|Enabled|
|Windows Forms|Enabled|
|Nullable|Enabled|
|Implicit Usings|Enabled|

Fuente: archivo de proyecto en la rama `main`.

## 3. Dependencias externas

### 3.1 Velopack

|Propiedad|Valor|
|---|---|
|Paquete|Velopack|
|Versión|1.2.161|
|Estado|Declarado y referenciado en el arranque|
|Área|Instalación y actualizaciones|
|Consumidor identificado|Program.cs|

Responsabilidad:

Gestionar operaciones relacionadas con la distribución y actualización de la aplicación.

Relación arquitectónica:

`Program.cs` ejecuta Velopack antes de iniciar la aplicación WPF.

Puntos de auditoría:

- Configuración de canales.
- Descarga de actualizaciones.
- Instalación.
- Reinicio después de actualizar.
- Recuperación ante errores.
- Compatibilidad con instalaciones anteriores.

### 3.2 NAudio

|Propiedad|Valor|
|---|---|
|Paquete|NAudio|
|Versión|2.2.1|
|Estado|Declarado|
|Área probable|Audio|
|Consumidor concreto|Pendiente|

Responsabilidad probable:

Proporcionar acceso a funcionalidades de audio de Windows.

Áreas que deben investigarse:

- Enumeración de dispositivos.
- Control de volumen.
- Sesiones de audio.
- Integración con Media.
- Integración con Music.
- Liberación de recursos de audio.

No se considera verificado qué funcionalidades de NAudio utiliza actualmente Launcher.

### 3.3 System.Management

|Propiedad|Valor|
|---|---|
|Paquete|System.Management|
|Versión|10.0.*|
|Estado|Declarado|
|Área probable|Información del sistema|
|Consumidor concreto|Pendiente|

Responsabilidad probable:

Acceso a Windows Management Instrumentation (WMI).

Áreas que deben investigarse:

- Hardware.
- Dispositivos.
- Procesos.
- Información del sistema.
- Monitorización.

Puntos de auditoría:

- Coste de consultas WMI.
- Gestión de excepciones.
- Permisos.
- Recursos y suscripciones.
- Ejecución fuera del hilo de interfaz.

### 3.4 Microsoft.Playwright

|Propiedad|Valor|
|---|---|
|Paquete|Microsoft.Playwright|
|Versión|1.*|
|Estado|Declarado|
|Área probable|Automatización web|
|Consumidor concreto|Pendiente|

Responsabilidad probable:

Automatización de navegadores mediante Playwright.

Áreas que deben investigarse:

- Navegadores utilizados.
- Instalación de binarios.
- Gestión de sesiones.
- Ejecución de procesos externos.
- Integración con ImageGen o Assistant, si existe.

Puntos de auditoría:

- Gestión del ciclo de vida del navegador.
- Recursos y procesos huérfanos.
- Gestión de credenciales.
- Seguridad de navegación.
- Compatibilidad de versiones.

La dependencia no demuestra que exista actualmente una funcionalidad de navegador operativa.

### 3.5 System.Data.OleDb

|Propiedad|Valor|
|---|---|
|Paquete|System.Data.OleDb|
|Versión|9.0.0|
|Estado|Declarado|
|Área probable|Acceso a datos|
|Consumidor concreto|Pendiente|

Responsabilidad probable:

Acceso a fuentes de datos compatibles con OLE DB.

Puntos de auditoría:

- Uso efectivo.
- Proveedores instalados.
- Compatibilidad x64.
- Dependencias nativas.
- Gestión de conexiones.
- Necesidad real dentro del producto.

Se deberá determinar si este paquete es necesario para una funcionalidad activa o si constituye una dependencia residual.

### 3.6 TagLibSharp

|Propiedad|Valor|
|---|---|
|Paquete|TagLibSharp|
|Versión|2.3.0|
|Estado|Declarado|
|Área probable|Multimedia|
|Consumidor concreto|Pendiente|

Responsabilidad probable:

Lectura y manipulación de metadatos de archivos multimedia.

Áreas que deben investigarse:

- Metadatos musicales.
- Portadas.
- Artistas.
- Álbumes.
- Duración.
- Integración con Media y Music.

Puntos de auditoría:

- Formatos utilizados.
- Manejo de archivos corruptos.
- Acceso a archivos bloqueados.
- Rendimiento.
- Lectura de imágenes embebidas.

## 4. Resumen de dependencias externas

|Paquete|Versión|Uso confirmado|
|---|---|---|
|Velopack|1.2.161|Arranque|
|NAudio|2.2.1|Pendiente|
|System.Management|10.0.*|Pendiente|
|Microsoft.Playwright|1.*|Pendiente|
|System.Data.OleDb|9.0.0|Pendiente|
|TagLibSharp|2.3.0|Pendiente|

## 5. Dependencias internas

### 5.1 IModule

Ruta identificada:

`Core/Interfaces/IModule.cs`

Es el contrato común de los módulos.

Expone:

- Identificador.
- Nombre visible.
- Icono.
- Orden.
- Método de creación de vista.

Relación:

Los módulos implementan el contrato y MainWindow los utiliza para construir la navegación.

### 5.2 ModuleCatalog

Ruta:

`Core/Services/ModuleCatalog.cs`

Implementación comprobada:

- Clase estática.
- Almacena `IReadOnlyList<IModule>`.
- Inicializa la lista vacía.
- Recibe los módulos mediante `Register()`.
- Ordena los módulos por `Order`.

Consumidores documentados:

- MainWindow: registra el catálogo.
- Settings: necesita consultar los módulos para ofrecer opciones de navegación inicial.

El segundo consumidor debe verificarse directamente en el código actual.

### 5.3 Servicios compartidos

La documentación del repositorio identifica los siguientes servicios:

|Servicio|Responsabilidad declarada|
|---|---|
|AppSettingsService|Configuración general|
|LibraryPathsService|Rutas de bibliotecas|
|CoverArtService|Portadas|
|IconExtractionService|Extracción de iconos|
|FolderShortcutsService|Accesos a carpetas|
|MediaAppsService|Aplicaciones multimedia|
|NowPlayingService|Información de reproducción|
|AudioMixerService|Mezclador de audio|
|SystemMonitorService|Monitorización|
|WindowBehaviorService|Comportamiento de ventanas|
|GlobalHotkeyService|Atajos globales|
|StartupService|Inicio con Windows|
|UpdateService|Actualizaciones|
|LogService|Logging|
|OllamaService|Integración con Ollama|
|ModuleCatalog|Catálogo de módulos|

La lista constituye un inventario inicial. Hay que comprobar las clases, referencias y consumidores en la versión actual.

## 6. Dependencias entre módulos

Los siguientes vínculos son relaciones funcionales que debemos investigar, no dependencias de código ya demostradas.

|Módulo|Dependencias que investigar|
|---|---|
|Assistant|Models, OllamaService, Settings|
|Commands|Core, configuración|
|Dashboard|Servicios de monitorización y resumen|
|Devices|System.Management, Core|
|Folders|Persistencia y sistema de archivos|
|Games|Biblioteca, rutas, portadas|
|ImageGen|Models, servicios de IA y automatización web|
|Media|NAudio, TagLibSharp, servicios multimedia|
|Models|OllamaService, configuración|
|Monitor|System.Management, servicios de monitorización|
|Music|NAudio, TagLibSharp, NowPlayingService|
|Network|Servicios de Windows y red|
|Processes|System.Management, servicios del sistema|
|Settings|AppSettingsService, ModuleCatalog|

Se deberá identificar para cada relación:

1. Si existe una referencia directa.
2. Si utiliza un servicio compartido.
3. Si depende de otro módulo funcionalmente.
4. Si intercambia datos mediante modelos.
5. Si utiliza comunicación indirecta.
6. Si puede funcionar independientemente.

## 7. Grafo conceptual de dependencias

mermaid

```
flowchart TD
    APP["Launcher Application"]
    CORE["Core"]
    INTERFACES["Interfaces"]
    SERVICES["Shared Services"]
    MODELS["Shared Models"]
    CATALOG["ModuleCatalog"]
    MODULES["14 Functional Modules"]
    UI["Shared UI"]
    EXTERNAL["External Packages"]

    APP --> CORE
    APP --> MODULES
    APP --> UI
    APP --> EXTERNAL

    CORE --> INTERFACES
    CORE --> SERVICES
    CORE --> MODELS
    CORE --> CATALOG

    MODULES --> INTERFACES
    MODULES --> SERVICES
    MODULES --> MODELS
    MODULES --> UI
    SERVICES --> EXTERNAL
```

Este diagrama representa la organización conceptual. No es todavía un grafo extraído automáticamente de las referencias `using`, constructores o llamadas entre clases.

## 8. Riesgos de dependencias

### DEP-001 — Versiones flotantes

Paquetes afectados:

- System.Management: 10.0.*
- Microsoft.Playwright: 1.*

Riesgo:

La resolución de versiones puede cambiar entre restauraciones del proyecto.

Recomendación:

Evaluar el uso de versiones fijadas y un archivo de bloqueo de dependencias.

Prioridad: P2.

### DEP-002 — Compatibilidad de System.Data.OleDb

Riesgo:

La integración con proveedores OLE DB puede depender de componentes nativos y de la arquitectura del proceso.

Recomendación:

Comprobar la necesidad real y validar la compatibilidad x64.

Prioridad: P2.

### DEP-003 — Ciclo de vida de audio

Paquete relacionado: NAudio.

Riesgo:

Recursos de audio y sesiones que no se liberen correctamente.

Recomendación:

Revisar suscripciones, disposiciones y gestión de recursos.

Prioridad: P2.

### DEP-004 — Automatización de navegador

Paquete relacionado: Microsoft.Playwright.

Riesgo:

Procesos de navegador, dependencias de instalación y recursos externos.

Recomendación:

Identificar sus consumidores y documentar el ciclo de vida completo.

Prioridad: P2.

### DEP-005 — Acoplamiento entre módulos

Riesgo:

Que los módulos dependan directamente unos de otros en lugar de utilizar contratos y servicios compartidos.

Recomendación:

Construir un grafo de referencias y evaluar dependencias circulares.

Prioridad: P1.

### DEP-006 — Catálogo estático

Componente: ModuleCatalog.

Riesgo potencial:

Dependencia global del catálogo y dificultad para aislarlo en pruebas.

Recomendación:

Evaluar su ciclo de vida, responsabilidades y alternativas sin modificarlo innecesariamente.

Prioridad: P3.

## 9. Reglas de gestión de dependencias

1. No introducir paquetes sin justificar su necesidad.
2. Preferir funcionalidades existentes antes de crear nuevos servicios.
3. Evitar referencias directas entre módulos cuando puedan utilizarse contratos compartidos.
4. Documentar cambios de versiones.
5. Comprobar compatibilidad con .NET 10.
6. Revisar vulnerabilidades conocidas.
7. Evaluar dependencias transitivas.
8. No eliminar paquetes sin comprobar sus consumidores.
9. Actualizar este documento al modificar `Launcher.csproj`.
10. Mantener un registro de cambios arquitectónicos relevantes.

## 10. Pendientes de investigación

- [ ] Inventariar todos los archivos `.csproj`.
- [ ] Extraer referencias `PackageReference`.
- [ ] Identificar dependencias transitivas.
- [ ] Localizar los seis paquetes en el código.
- [ ] Identificar consumidores de cada servicio.
- [ ] Reconstruir las relaciones de los 14 módulos.
- [ ] Detectar dependencias circulares.
- [ ] Revisar versiones vulnerables.
- [ ] Revisar uso de dependencias obsoletas.
- [ ] Identificar librerías internas no utilizadas.
- [ ] Crear grafo de dependencias verificadas.

## 11. Documentos relacionados

- [[../../Home|Home]]
- [[ARCHITECTURE|Architecture]]
- [[DIRECTORY_MAP|Directory Map]]
- [[../00-Project/PROJECT_CONTEXT|Project Context]]
- [[../00-Project/CURRENT_STATE|Current State]]
- [[../02-Modules/MODULES|Module Catalog]]
- [[../03-Decisions/DECISIONS|Technical Decisions]]
- [[../04-Audit/FINDINGS|Audit Findings]]

## 12. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Inventario inicial de paquetes NuGet|
|2026-11-11|Documentación inicial de IModule y ModuleCatalog|
|Pendiente|Verificación de dependencias internas|
|Pendiente|Grafo real de dependencias|

## Estado de dependencias comprobado — 2026-11-11

Launcher.csproj declara Velopack 1.2.161, NAudio 2.2.1, System.Management 10.0.*, Microsoft.Playwright 1.*, System.Data.OleDb 9.0.0 y TagLibSharp 2.3.0. Target: `net10.0-windows10.0.19041.0`; WPF y Windows Forms habilitados.

System.Management y Microsoft.Playwright tienen versiones flotantes. No se ha realizado análisis de dependencias transitivas ni de vulnerabilidades. La declaración de un paquete no demuestra uso efectivo; revisar consumidores antes de retirarlo o actualizarlo.
