---

title: Models module_id: models type: module status: pending-verification tags:

- launcher/module
- launcher/ai

---

# Models

## Responsabilidad

Gestionar los modelos de inteligencia artificial disponibles para Launcher.

## Funcionalidades que investigar

- Detección de modelos locales.
- Listado de modelos.
- Información de tamaño y formato.
- Descarga.
- Instalación.
- Eliminación.
- Selección de modelo activo.
- Integración con Ollama.
- Integración con otros motores.

## Dependencias que investigar

- OllamaService.
- Assistant.
- ImageGen.
- Servicios HTTP.
- Sistema de archivos.
- AppSettingsService.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar motores.
- [ ] Revisar detección.
- [ ] Revisar descargas.
- [ ] Revisar almacenamiento.
- [ ] Revisar selección de modelos.
- [ ] Revisar gestión de errores.
- [ ] Revisar seguridad de archivos descargados.
- [ ] Analizar uso de GPU y memoria.

## Riesgos

- Modelos incompatibles.
- Descargas incompletas.
- Uso excesivo de almacenamiento.
- Consumo de VRAM.
- Confusión entre modelos de texto e imagen.

## Documentación relacionada

- [[MODULES]]
- [[ASSISTANT]]
- [[IMAGEGEN]]
- [[MONITOR]]