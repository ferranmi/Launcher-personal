---

title: Media module_id: media type: module status: documented-pending-verification tags:

- launcher/module
- launcher/media

---

# Media

## Responsabilidad

Gestionar aplicaciones multimedia y proporcionar información y controles relacionados con la reproducción.

## Funcionalidades documentadas

- Listado de aplicaciones multimedia.
- Alta y edición de aplicaciones.
- Integración con reproducción actual.
- Información de sesiones de audio.

## Componentes identificados

- `MediaModule`.
- `MediaView`.
- `MediaAppShortcut`.
- `MediaAppsService`.
- `NowPlayingService`.
- `AudioMixerService`.
- `NowPlayingInfo`.
- `AudioSessionInfo`.

## Dependencias que investigar

- NAudio.
- TagLibSharp.
- Windows APIs.
- Core.
- Persistencia JSON.

## Auditoría

- [ ] Verificar vista actual.
- [ ] Identificar aplicaciones compatibles.
- [ ] Revisar sesiones de audio.
- [ ] Revisar controles de reproducción.
- [ ] Analizar integración con Music.
- [ ] Revisar suscripciones a eventos.
- [ ] Revisar liberación de recursos.
- [ ] Revisar persistencia.

## Riesgos

- Sesiones de audio que desaparecen.
- Recursos no liberados.
- Diferencias entre reproductores.
- Dependencias entre Media y Music.

## Documentación relacionada

- [[MODULES]]
- [[MUSIC]]
- [[SETTINGS]]