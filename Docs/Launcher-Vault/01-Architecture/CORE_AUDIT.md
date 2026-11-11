---

title: Core Audit type: technical-audit project: Launcher-personal source_of_truth: GitHub main last_reviewed: 2026-11-11 status: partial tags:

- launcher/core
- launcher/audit
- launcher/architecture

---

# Core Audit

## 1. Objetivo

Analizar la infraestructura compartida de Launcher y determinar cómo se comunican los módulos con la aplicación principal.

Core debe proporcionar contratos y servicios reutilizables sin convertirse en un contenedor de funcionalidades específicas de cada módulo.

## 2. Estructura objeto de auditoría

text

```
Core/
├── Interfaces/
├── Models/
├── Services/
├── Converters/
└── Mvvm/
```

Esta es la estructura de áreas que debemos investigar. La presencia y contenido de cada directorio deben comprobarse recursivamente.

## 3. Interfaces

### IModule

Archivo:

`Core/Interfaces/IModule.cs`

Estado: comprobado directamente en GitHub.

Namespace:

`Launcher.Core.Interfaces`

Contrato:

|Miembro|Tipo|Responsabilidad|
|---|---|---|
|Id|string|Identificador estable|
|DisplayName|string|Nombre visible|
|Icon|string|Glifo de Segoe Fluent Icons|
|Order|int|Orden de navegación|
|CreateView()|UserControl|Crear vista del módulo|

### Observaciones arquitectónicas

1. La aplicación conoce los módulos mediante un contrato común.
2. El contrato no expone los servicios internos del módulo.
3. La creación de vistas está delegada a cada módulo.
4. La interfaz devuelve directamente un `UserControl`.
5. El contrato no define explícitamente inicialización asíncrona.
6. No define un método de liberación de recursos.
7. No define un ciclo de vida de activación y desactivación.

### Riesgos potenciales

- Acoplamiento entre módulos y WPF.
- Dificultad para liberar recursos al abandonar una vista.
- Ausencia de un contrato explícito de inicialización.
- Dificultad para realizar pruebas sin crear componentes visuales.

Estos puntos son oportunidades de investigación, no defectos confirmados.

## 4. ModuleCatalog

Archivo:

`Core/Services/ModuleCatalog.cs`

Estado: comprobado directamente en GitHub.

Namespace:

`Launcher.Core.Services`

Implementación:

- Clase estática.
- Propiedad `Modules`.
- Tipo `IReadOnlyList<IModule>`.
- Inicialización mediante lista vacía.
- Método `Register(IEnumerable<IModule>)`.
- Ordenación mediante `OrderBy(m => m.Order)`.

### Funcionamiento

text

```
MainWindow
    |
    | Register(modules)
    v
ModuleCatalog
    |
    | OrderBy(Order)
    v
Modules
    |
    +--> Settings
    +--> Other Consumers
```

### Observaciones

- La lista se comparte globalmente.
- El registro sustituye la lista anterior.
- La ordenación se realiza durante el registro.
- No existe una protección explícita contra identificadores duplicados.
- No existe una validación explícita de módulos nulos.
- No existe un mecanismo explícito de registro incremental.

### Riesgos potenciales

- Dependencia global.
- Dificultad para aislar pruebas.
- Registro accidental de módulos duplicados.
- Sustitución inesperada del catálogo.

## 5. Models

Estado: pendiente de inventario completo.

Objetivos:

- Identificar modelos compartidos.
- Identificar modelos específicos de módulos.
- Detectar duplicaciones.
- Revisar serialización.
- Identificar relaciones entre modelos.
- Detectar modelos que mezclan datos y lógica de presentación.

Elementos documentados históricamente:

- Game.
- FolderShortcut.
- CommandShortcut.
- MediaAppShortcut.
- NowPlayingInfo.
- AudioSessionInfo.
- SystemSnapshot.
- DriveStat.
- ProcessLoadInfo.
- AssistantMessage.

No se considera confirmada su ubicación ni vigencia sin revisar los archivos actuales.

## 6. Services

Estado: pendiente de inventario completo.

Servicios que debemos localizar:

|Servicio|Área|
|---|---|
|AppSettingsService|Configuración|
|LibraryPathsService|Bibliotecas|
|CoverArtService|Portadas|
|IconExtractionService|Iconos|
|FolderShortcutsService|Carpetas|
|MediaAppsService|Multimedia|
|NowPlayingService|Reproducción|
|AudioMixerService|Audio|
|SystemMonitorService|Monitorización|
|WindowBehaviorService|Ventanas|
|GlobalHotkeyService|Atajos|
|StartupService|Inicio de Windows|
|UpdateService|Actualizaciones|
|LogService|Logging|
|OllamaService|Inteligencia artificial|

La lista procede de la documentación del proyecto y debe contrastarse con las implementaciones de `main`.

### Análisis requerido para cada servicio

- Responsabilidad.
- Constructor.
- Dependencias.
- Consumidores.
- Estado compartido.
- Persistencia.
- Operaciones asíncronas.
- Gestión de errores.
- Liberación de recursos.
- Pruebas.

## 7. MVVM

Estado: pendiente de inspección directa.

Elementos que deben localizarse:

- ViewModelBase.
- RelayCommand.
- Implementación de INotifyPropertyChanged.
- Gestión de comandos.
- Actualizaciones de propiedades.
- Operaciones asíncronas.
- Cancelación de operaciones.

Objetivo: comprobar que la lógica de negocio no se encuentre innecesariamente en el code-behind de las vistas.

## 8. Converters

Estado: pendiente.

Analizar:

- Conversores disponibles.
- Dependencias con modelos.
- Uso desde XAML.
- Conversores duplicados.
- Conversores sin referencias.
- Tratamiento de valores nulos.

## 9. Relaciones comprobadas

|Origen|Destino|Relación|
|---|---|---|
|MainWindow|IModule|Consume contrato|
|MainWindow|ModuleCatalog|Registra módulos|
|ModuleCatalog|IModule|Almacena módulos|
|Módulos|IModule|Implementación del contrato|

## 10. Hallazgos iniciales

### CORE-001 — Dependencia de WPF en el contrato modular

- Prioridad: P3.
- Evidencia: IModule utiliza `UserControl`.
- Impacto: el contrato está ligado a la tecnología de presentación.
- Estado: confirmado.
- Acción: evaluar si esta dependencia es aceptable para la arquitectura actual.

### CORE-002 — Catálogo global estático

- Prioridad: P3.
- Evidencia: ModuleCatalog utiliza una propiedad estática.
- Impacto: dependencia global y aislamiento de pruebas.
- Estado: confirmado.
- Acción: revisar consumidores y ciclo de vida.

### CORE-003 — Ausencia de validación explícita del registro

- Prioridad: P2.
- Evidencia: Register ordena y almacena la colección sin validaciones visibles.
- Impacto: posibilidad de registrar identificadores duplicados.
- Estado: confirmado en la implementación examinada.
- Acción: estudiar si la validación se realiza en otro punto antes de proponer cambios.

## 11. Pendientes

- [ ] Inventario recursivo de Interfaces.
- [ ] Inventario recursivo de Models.
- [ ] Inventario recursivo de Services.
- [ ] Inventario recursivo de Converters.
- [ ] Inventario recursivo de Mvvm.
- [ ] Revisar todos los constructores de servicios.
- [ ] Identificar instancias singleton.
- [ ] Identificar consumidores de cada servicio.
- [ ] Construir grafo real de dependencias.
- [ ] Revisar persistencia.
- [ ] Revisar pruebas.
- [ ] Revisar posibles dependencias circulares.

## 12. Documentos relacionados

- [[ARCHITECTURE]]
- [[DEPENDENCIES]]
- [[DIRECTORY_MAP]]
- [[../02-Modules/MODULES]]
- [[../00-Project/CURRENT_STATE]]
- [[../04-Audit/FINDINGS]]

## 13. Historial

|Fecha|Cambio|
|---|---|
|2026-11-11|Primera auditoría de IModule y ModuleCatalog|
|Pendiente|Inspección completa del resto de Core|