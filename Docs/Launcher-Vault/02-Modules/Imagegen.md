---

title: ImageGen module_id: imagegen type: module status: pending-verification tags:

- launcher/module
- launcher/ai

---

# ImageGen

## Responsabilidad

Integrar funcionalidades de generación de imágenes en Launcher.

## Funcionalidades que investigar

- Conexión con motores de generación.
- Configuración de modelos.
- Parámetros de generación.
- Gestión de prompts.
- Cola de trabajos.
- Visualización de resultados.
- Galería.
- Guardado de imágenes.

## Contexto documentado

El proyecto ha contemplado una integración con AUTOMATIC1111 / Stable Diffusion.

La integración actual debe verificarse.

## Dependencias que investigar

- Models.
- Assistant.
- Microsoft.Playwright, si existe utilización efectiva.
- Servicios HTTP.
- Gestión de imágenes.
- Persistencia.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar motores compatibles.
- [ ] Revisar endpoints.
- [ ] Revisar gestión de prompts.
- [ ] Revisar trabajos asíncronos.
- [ ] Revisar cancelación.
- [ ] Revisar almacenamiento.
- [ ] Revisar tratamiento de errores.
- [ ] Revisar seguridad de comunicaciones.

## Riesgos

- Dependencia de servicios externos.
- Consumo elevado de recursos.
- Operaciones largas.
- Imágenes temporales no eliminadas.
- Credenciales o endpoints expuestos.

## Documentación relacionada

- [[MODULES]]
- [[MODELS]]
- [[ASSISTANT]]