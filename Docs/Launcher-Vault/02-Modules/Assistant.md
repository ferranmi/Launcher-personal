---

title: Assistant module_id: assistant type: module status: pending-verification tags:

- launcher/module
- launcher/ai

---

# Assistant

## Responsabilidad

Módulo destinado a proporcionar un asistente de inteligencia artificial integrado en Launcher.

## Funcionalidades que investigar

- Interfaz conversacional.
- Comunicación con modelos locales.
- Historial de conversaciones.
- Gestión del contexto.
- Ejecución de acciones desde el asistente.
- Integración con el resto de módulos.
- Atajos globales y activación rápida.

## Componentes identificados en documentación

- `AssistantWindow`.
- `AssistantMessage`.
- `OllamaService`.
- `GlobalHotkeyService`.

La documentación anterior indica que AssistantWindow existe en el snapshot, pero no está registrado como módulo. Debe comprobarse en `main`.

## Dependencias que investigar

- Models.
- OllamaService.
- AppSettingsService.
- Servicios de navegación.
- Core.

## Auditoría

- [ ] Localizar implementación actual.
- [ ] Verificar registro como módulo.
- [ ] Inspeccionar AssistantWindow.
- [ ] Inspeccionar OllamaService.
- [ ] Verificar conexión con Ollama.
- [ ] Revisar gestión de conversaciones.
- [ ] Revisar seguridad de acciones.
- [ ] Revisar tratamiento de errores.
- [ ] Identificar funcionalidades incompletas.

## Riesgos

- Exposición de información sensible en el contexto.
- Ejecución de acciones sin autorización suficiente.
- Bloqueos de interfaz durante inferencias.
- Dependencia de servicios externos o locales.

## Documentación relacionada

- [[MODULES]]
- [[MODELS]]
- [[../01-Architecture/DEPENDENCIES]]