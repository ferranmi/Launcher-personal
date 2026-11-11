---
title: "Project Context"
type: project-reference
project: Launcher
source_of_truth: GitHub main
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
last_reviewed: 2026-11-11
status: reconciled-static
tags: [launcher, documentation]
---

# Project Context

## 1. Identificación

| Propiedad             | Valor                                                                                          |
| --------------------- | ---------------------------------------------------------------------------------------------- |
| Nombre                | Launcher                                                                                       |
| Repositorio           | [https://github.com/ferranmi/Launcher-personal](https://github.com/ferranmi/Launcher-personal) |
| Rama principal        | `main`                                                                                         |
| Proyecto              | Aplicación de escritorio                                                                       |
| Lenguaje principal    | C#                                                                                             |
| Framework de interfaz | WPF                                                                                            |
| Plataforma            | Windows                                                                                        |
| Target Framework      | `net10.0-windows10.0.19041.0`                                                                  |
| Versión del proyecto  | `0.1.52`                                                                                       |
| Archivo de proyecto   | `Launcher.csproj`                                                                              |
| Idioma de trabajo     | Español                                                                                        |

## 2. Objetivo del proyecto

Launcher es una aplicación de escritorio para Windows que pretende centralizar distintas funcionalidades y herramientas del ordenador en una única interfaz modular.

La aplicación utiliza una estética visual oscura, inspirada en terminales, con acentos verdes y navegación mediante una barra lateral.

El objetivo funcional es ofrecer un punto central desde el que acceder a herramientas, aplicaciones, juegos, información del sistema y otras funcionalidades del equipo.

Esta descripción representa el propósito general declarado del proyecto. No implica que todas las funcionalidades previstas estén implementadas.

## 3. Alcance funcional conocido

El README identifica las siguientes áreas funcionales:

|Área|Identificador conceptual|Descripción|
|---|---|---|
|Juegos|Games|Gestión y lanzamiento de juegos|
|Carpetas|Folders|Accesos a ubicaciones del sistema|
|Multimedia|Media|Control y acceso a aplicaciones multimedia|
|Comandos|Commands|Accesos y ejecución de comandos|
|Monitor|Monitor|Información y monitorización del sistema|
|Procesos|Processes|Consulta y seguimiento de procesos|
|Configuración|Settings|Preferencias de la aplicación|
|Música|Music|Área funcional pendiente de verificar|
|Buscar|Search|Área funcional pendiente de verificar|
|Asistente|Assistant|Integración de asistente pendiente de verificar|

El inventario definitivo de módulos se establecerá mediante la inspección del código y del registro de módulos de la aplicación.

## 4. Arquitectura tecnológica

### Tecnologías principales

- C#.
- .NET 10.
- WPF.
- Windows Forms para determinadas integraciones de escritorio.
- XAML para interfaces y recursos visuales.

### Dependencias declaradas en Launcher.csproj

- Velopack 1.2.161.
- NAudio 2.2.1.
- System.Management 10.0.*.
- Microsoft.Playwright 1.*.
- System.Data.OleDb 9.0.0.
- TagLibSharp 2.3.0.

La presencia de una dependencia en el proyecto no demuestra por sí sola que esté siendo utilizada funcionalmente. Su utilización efectiva y necesidad se estudiarán en la auditoría de dependencias.

## 5. Organización general

La raíz de GitHub contiene, entre otros elementos:

- `Assets/`: recursos gráficos.
- `Core/`: componentes centrales.
- `Modules/`: funcionalidades modulares.
- `UI/`: componentes y recursos de interfaz.
- `posos lora/`: directorio adicional pendiente de clasificación.

También existen archivos de aplicación, configuración, publicación y documentación:

- `App.xaml`
- `App.xaml.cs`
- `AssemblyInfo.cs`
- `Launcher.csproj`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Program.cs`
- `README.md`
- `app.manifest`
- `publish.ps1`

En el repositorio también aparecen archivos ZIP y PATCH relacionados con cambios del buscador y widgets. Su finalidad, vigencia y relación con el código principal deben determinarse antes de clasificarlos como parte del producto.

## 6. Ejecución y distribución

El proyecto está orientado a Windows y utiliza WPF.

El README documenta un proceso de publicación mediante `publish.ps1` y Velopack.

La configuración del proyecto establece:

- Aplicación Windows (`WinExe`).
- WPF habilitado.
- Windows Forms habilitado.
- Manifiesto de aplicación.
- Punto de entrada personalizado `Launcher.Program`.

El proceso de compilación, ejecución y publicación deberá verificarse en un entorno Windows con el SDK correspondiente.

## 7. Persistencia de datos

El README describe una persistencia basada principalmente en archivos JSON bajo el perfil del usuario.

Entre los datos documentados se encuentran:

- Configuración general.
- Rutas de bibliotecas.
- Biblioteca de juegos.
- Juegos ignorados.
- Accesos a carpetas.
- Comandos.
- Aplicaciones multimedia.
- Portadas.
- Registros de actividad.

Esta descripción se considera documentación declarativa hasta verificar los servicios de almacenamiento y las rutas reales en el código actual.

## 8. Criterios de desarrollo

El proyecto debe evolucionar respetando las siguientes directrices:

1. Mantener la arquitectura existente salvo que exista una justificación técnica.
2. Evitar duplicar servicios y responsabilidades.
3. Respetar los contratos entre módulos.
4. Mantener la compatibilidad de los datos persistidos.
5. No eliminar funcionalidades existentes sin autorización.
6. Priorizar cambios pequeños y verificables.
7. Mantener la coherencia visual de la aplicación.
8. Documentar las decisiones arquitectónicas relevantes.
9. Ejecutar las comprobaciones disponibles después de modificar código.
10. No declarar terminada una funcionalidad sin evidencia.

## 9. Fuente de verdad

Orden de prioridad para resolver discrepancias:

1. Código actual de la rama `main`.
2. Configuración efectiva del proyecto.
3. Pruebas automatizadas y resultados de ejecución.
4. Historial Git.
5. README y documentación técnica.
6. Inferencias y propuestas.

Las peticiones futuras y las ideas de diseño no deben confundirse con funcionalidades implementadas.

## 10. Limitaciones conocidas del análisis

En el momento de crear este documento:

- Se ha inspeccionado la estructura raíz.
- Se ha consultado el archivo `Launcher.csproj`.
- Se ha consultado el README.
- No se ha ejecutado la aplicación.
- No se ha realizado una compilación independiente.
- No se ha completado el análisis recursivo de todos los módulos.
- No se ha verificado la cobertura de pruebas.

Estas limitaciones deberán actualizarse conforme avance la auditoría.

## 11. Documentos relacionados

- [[Home|Vault principal]]
- [[CURRENT_STATE|Estado actual]]
- [[ROADMAP|Roadmap]]
- [[../01-Architecture/ARCHITECTURE|Arquitectura]]
- [[../01-Architecture/DIRECTORY_MAP|Mapa de directorios]]
- [[../02-Modules/MODULES|Catálogo de módulos]]
- [[../04-Decisions/Decisions|Decisiones técnicas]]

## 12. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Creación inicial del contexto del proyecto|
|Pendiente|Completar después del inventario y auditoría de módulos|

## Reconciliación con el código — 2026-11-11

La versión del snapshot revisado es 0.1.53. El código registra 13 módulos; Search permanece sin registrar y Assistant es una ventana independiente. La lista y los detalles se encuentran en [[Current State|Current State]] y [[../02-Modules/Modules|Modules]].

La auditoría es estática y no acredita funcionamiento en ejecución. Referencia: commit `5c621accde912fc8dda48873fb44d4d93857901a`.
