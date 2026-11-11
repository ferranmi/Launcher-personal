---

title: Settings module_id: settings type: module status: documented-pending-verification tags:

- launcher/module
- launcher/configuration

---

# Settings

## Responsabilidad

Centralizar la configuración y las preferencias de Launcher.

## Funcionalidades documentadas

- Configuración general.
- Preferencias de ventana.
- Inicio con Windows.
- Actualizaciones.
- Configuración de bibliotecas.
- Módulo inicial.
- Gestión de juegos ignorados.

## Componentes identificados

- `SettingsModule`.
- `SettingsView`.
- `AppSettingsService`.
- `LibraryPathsService`.
- `StartupService`.
- `UpdateService`.
- `ModuleCatalog`.

## Persistencia documentada

- `config.json`.
- `settings.json`.

También se documenta el uso del registro de Windows para el inicio automático.

## Dependencias que investigar

- Core.
- ModuleCatalog.
- Todos los módulos que exponen preferencias.
- Servicios de actualización.
- Persistencia.

## Auditoría

- [ ] Verificar vista actual.
- [ ] Inventariar opciones.
- [ ] Revisar valores predeterminados.
- [ ] Revisar serialización.
- [ ] Revisar compatibilidad de versiones.
- [ ] Revisar migraciones.
- [ ] Revisar inicio con Windows.
- [ ] Revisar actualización.
- [ ] Comprobar persistencia.
- [ ] Revisar validación de entradas.

## Riesgos

- Configuraciones incompatibles.
- Valores predeterminados incorrectos.
- Pérdida de preferencias.
- Dependencias globales.
- Opciones que no afectan realmente al comportamiento.

## Documentación relacionada

- [[MODULES]]
- [[COMMANDS]]
- [[GAMES]]
- [[ASSISTANT]]