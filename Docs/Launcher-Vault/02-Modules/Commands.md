---

title: Commands module_id: commands type: module status: documented-pending-verification tags:

- launcher/module

---

# Commands

## Responsabilidad

Gestionar accesos y comandos personalizados ejecutables desde Launcher.

## Funcionalidades documentadas

- Listado de comandos.
- Alta, edición y eliminación.
- Categorías.
- Sugerencias.
- Ejecución de comandos.
- Persistencia de accesos.

## Componentes identificados en documentación

- `CommandsModule`.
- `CommandsView`.
- `CommandsService`.
- `CommandShortcut`.
- `CommandCategory`.
- Ventana Add/Edit.

## Dependencias que investigar

- Core.
- AppSettingsService.
- Persistencia JSON.
- Procesos de Windows.
- Navegadores y terminales externos.

## Auditoría

- [ ] Verificar implementación actual.
- [ ] Revisar comandos permitidos.
- [ ] Analizar argumentos.
- [ ] Revisar ejecución de procesos.
- [ ] Revisar permisos.
- [ ] Revisar validación de rutas.
- [ ] Revisar persistencia.
- [ ] Revisar manejo de errores.
- [ ] Identificar posibles inyecciones de comandos.

## Riesgos

- Ejecución de comandos no deseados.
- Argumentos mal validados.
- Bloqueos por procesos síncronos.
- Exposición de rutas o parámetros sensibles.

## Documentación relacionada

- [[MODULES]]
- [[PROCESSES]]
- [[SETTINGS]]