---
title: "DIRECTORY_MAP"
type: architecture-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, architecture, audit]
---

# Directory Map

## 1. Objetivo

Describir la organización física del repositorio Launcher.

Este documento permite localizar:

- Código de aplicación.
- Módulos funcionales.
- Servicios compartidos.
- Modelos e interfaces.
- Recursos visuales.
- Configuración.
- Documentación.
- Archivos auxiliares.

No pretende describir individualmente todos los archivos. Esa información se incorporará a las fichas de módulos cuando aporte valor.

## 2. Repositorio

**URL:** [https://github.com/ferranmi/Launcher-personal](https://github.com/ferranmi/Launcher-personal)

**Rama:** `main`

**Estructura raíz observada:**

text

```
Launcher-personal/
│
├── Assets/
├── Core/
├── Modules/
├── UI/
├── posos lora/
│
├── .gitignore
├── App.xaml
├── App.xaml.cs
├── AssemblyInfo.cs
├── Launcher.csproj
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── Program.cs
├── README.md
├── app.manifest
├── cambios-widgets-buscador.zip
├── publish.ps1
└── widgets-anadir-quitar-buscador.patch
```

Esta es la estructura de nivel raíz visible en GitHub. El inventario de subdirectorios todavía está pendiente de inspección recursiva.

## 3. Directorios principales

### Assets/

**Responsabilidad:** recursos gráficos y elementos visuales de la aplicación.

Uso identificado:

- Icono de aplicación.
- Recursos gráficos.

El archivo `Launcher.csproj` referencia `Assets/launcher.ico` como icono y recurso de aplicación.

Pendiente:

- Inventario de imágenes.
- Iconos por módulo.
- Fuentes.
- Recursos compartidos.
- Recursos sin referencias.
- Duplicados.

### Core/

**Responsabilidad esperada:** componentes compartidos por la aplicación y sus módulos.

Áreas que deberán localizarse:

- Interfaces.
- Modelos comunes.
- Servicios.
- MVVM.
- Conversiones de datos.
- Gestión de configuración.
- Logging.
- Navegación y registro de módulos.

El inventario interno se completará mediante inspección del código actual.

### Modules/

**Responsabilidad:** agrupar las funcionalidades de negocio y las interfaces específicas de cada módulo.

El proyecto contempla estos 14 módulos funcionales:

text

```
Modules/
│
├── Assistant/
├── Commands/
├── Dashboard/
├── Devices/
├── Folders/
├── Games/
├── ImageGen/
├── Media/
├── Models/
├── Monitor/
├── Music/
├── Network/
├── Processes/
└── Settings/
```

Esta representación es el mapa funcional deseado para el Vault, no una afirmación de que las 14 carpetas existan físicamente con estos nombres exactos en `main`.

La correspondencia física y los nombres reales deben verificarse antes de completar el inventario.

Cada módulo tendrá su propia ficha en `02-Modules/`.

### UI/

**Responsabilidad esperada:** recursos visuales y componentes de interfaz reutilizables.

Áreas que deben identificarse:

- Temas.
- Estilos.
- Controles personalizados.
- Recursos XAML.
- Elementos de navegación.
- Componentes visuales compartidos.

Pendiente: inventario recursivo y análisis de referencias desde las vistas.

### posos lora/

**Responsabilidad:** pendiente de identificación.

Se trata de un directorio visible en la raíz del repositorio.

No se debe asumir que forma parte del producto Launcher ni que es un componente de ejecución.

Hay que investigar:

- Tipo de archivos.
- Relación con Models e ImageGen.
- Documentación contenida.
- Posible material de otro proyecto.
- Dependencias con el código principal.

Hasta completar esta inspección se mantendrá clasificado como directorio auxiliar sin responsabilidad confirmada.

## 4. Archivos de aplicación en la raíz

|Archivo|Responsabilidad|
|---|---|
|`Program.cs`|Punto de entrada personalizado declarado por el proyecto|
|`App.xaml`|Definición de aplicación WPF|
|`App.xaml.cs`|Código asociado al ciclo de vida de aplicación|
|`MainWindow.xaml`|Definición visual de ventana principal|
|`MainWindow.xaml.cs`|Código asociado a ventana principal|
|`AssemblyInfo.cs`|Metadatos del ensamblado|
|`Launcher.csproj`|Configuración del proyecto .NET|
|`app.manifest`|Configuración del manifiesto de Windows|
|`.gitignore`|Exclusiones de Git|
|`README.md`|Documentación del repositorio|
|`publish.ps1`|Script de publicación|

### Archivos de especial importancia

#### Program.cs

Es el punto de entrada personalizado indicado mediante `StartupObject` en `Launcher.csproj`.

Su responsabilidad concreta debe analizarse para determinar el orden de inicialización y las integraciones previas al arranque de WPF.

#### App.xaml / App.xaml.cs

Constituyen la definición y el comportamiento de la aplicación WPF.

Hay que estudiar su relación con:

- Inicialización.
- Recursos globales.
- Gestión de excepciones.
- Instancia única.
- Servicios de aplicación.

#### MainWindow.xaml / MainWindow.xaml.cs

Representan la ventana principal.

Su análisis permitirá determinar:

- Navegación.
- Registro de módulos.
- Sidebar.
- Gestión de vistas.
- Servicios compartidos.
- Integración visual.

#### Launcher.csproj

Es la fuente de configuración técnica del proyecto.

Datos comprobados:

- Versión: `0.1.53`.
- Framework: `net10.0-windows10.0.19041.0`.
- WPF: habilitado.
- Windows Forms: habilitado.
- Punto de entrada: `Launcher.Program`.

## 5. Archivos auxiliares y paquetes

### cambios-widgets-buscador.zip

Archivo ZIP visible en la raíz.

Pendiente de determinar:

- Contenido.
- Fecha y origen.
- Correspondencia con archivos actuales.
- Si representa cambios ya integrados.
- Si debe conservarse como material histórico o eliminarse.

No debe tratarse como código activo sin verificar su contenido.

### widgets-anadir-quitar-buscador.patch

Archivo de parche Git relacionado aparentemente con widgets y buscador.

Pendiente:

- Revisar los archivos afectados.
- Comprobar si sus cambios ya están en `main`.
- Identificar posibles cambios obsoletos.
- Evitar aplicar el parche sin comparar previamente su contenido.

### publish.ps1

Script relacionado con la publicación de la aplicación.

Debe estudiarse junto con:

- `Launcher.csproj`.
- Configuración de Velopack.
- Parámetros de publicación.
- Arquitectura de destino.
- Generación del instalador.
- Gestión de versiones.

## 6. Mapa lógico de responsabilidades

La siguiente representación es conceptual. No sustituye al diagrama de dependencias real.

text

```
Launcher
│
├── Application
│   ├── Program
│   ├── App
│   └── MainWindow
│
├── Core
│   ├── Contracts
│   ├── Models
│   ├── Services
│   └── Shared Components
│
├── Modules
│   ├── Assistant
│   ├── Commands
│   ├── Dashboard
│   ├── Devices
│   ├── Folders
│   ├── Games
│   ├── ImageGen
│   ├── Media
│   ├── Models
│   ├── Monitor
│   ├── Music
│   ├── Network
│   ├── Processes
│   └── Settings
│
├── UI
│   ├── Themes
│   └── Controls
│
└── Assets
    └── Graphics and Resources
```

## 7. Reglas para clasificar nuevos archivos

Al encontrar un archivo nuevo o desconocido:

1. Identificar su extensión y tecnología.
2. Determinar su función.
3. Identificar el módulo propietario.
4. Comprobar si es compartido.
5. Identificar quién lo utiliza.
6. Comprobar si tiene referencias activas.
7. Clasificarlo como código, recurso, configuración, documentación o material auxiliar.

Un archivo no debe asignarse a un módulo únicamente por su nombre o ubicación.

## 8. Criterios de mantenimiento

Este mapa debe actualizarse cuando:

- Se crea un módulo.
- Se elimina o renombra un directorio.
- Se mueve código entre Core y Modules.
- Se introduce una nueva tecnología.
- Se añaden recursos compartidos.
- Se cambia el punto de entrada.
- Se modifica la estructura de publicación.

## 9. Pendientes del inventario

- [ ] Recorrer `Assets/`.
- [ ] Recorrer `Core/`.
- [ ] Recorrer `Modules/`.
- [ ] Identificar físicamente los 14 módulos.
- [ ] Recorrer `UI/`.
- [ ] Inspeccionar `posos lora/`.
- [ ] Inspeccionar el ZIP de widgets.
- [ ] Inspeccionar el PATCH.
- [ ] Identificar archivos de pruebas.
- [ ] Identificar workflows de GitHub Actions.
- [ ] Registrar el SHA del commit auditado.
- [ ] Generar inventario recursivo definitivo.

## 10. Documentos relacionados

- [[../../Home|Home]]
- [[../00-Project/PROJECT_CONTEXT|Project Context]]
- [[../00-Project/CURRENT_STATE|Current State]]
- [[ARCHITECTURE|Architecture]]
- [[DEPENDENCIES|Dependencies]]
- [[../02-Modules/MODULES|Module Catalog]]
- [[../03-Decisions/DECISIONS|Technical Decisions]]

## 11. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Creación del mapa inicial de directorios|
|Pendiente|Inventario recursivo completo|
|Pendiente|Verificación de correspondencia física de módulos|

## Reconciliación del árbol actual — 2026-11-11

El árbol contiene Core, Modules y UI. Hay 15 carpetas bajo Modules, pero solo 13 módulos se registran en MainWindow. Assistant contiene una ventana independiente; Search contiene un módulo no registrado. Lista exacta: [[../02-Modules/Modules|Modules]].

Este mapa conserva el inventario histórico; los nombres auxiliares y archivos empaquetados deben contrastarse con el árbol de cada commit antes de tratarlos como código activo.
