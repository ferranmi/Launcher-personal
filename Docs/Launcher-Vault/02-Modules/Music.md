---

title: Music module_id: music type: module status: pending-verification tags:

- launcher/module
- launcher/media

---

# Music

## Responsabilidad

Proporcionar funcionalidades específicas para la gestión y reproducción musical.

## Funcionalidades que investigar

- Biblioteca musical.
- Exploración de archivos.
- Metadatos.
- Álbumes y artistas.
- Portadas.
- Reproducción.
- Listas de reproducción.
- Integración con Media.

## Dependencias que investigar

- NAudio.
- TagLibSharp.
- NowPlayingService.
- AudioMixerService.
- Sistema de archivos.
- Persistencia.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar reproductor.
- [ ] Revisar formatos soportados.
- [ ] Revisar lectura de metadatos.
- [ ] Revisar portadas.
- [ ] Revisar listas.
- [ ] Revisar persistencia.
- [ ] Revisar ciclo de vida del audio.
- [ ] Comprobar integración con Media.

## Riesgos

- Conflictos entre sesiones.
- Lectura de metadatos defectuosos.
- Archivos grandes.
- Recursos de reproducción sin liberar.
- Duplicación de responsabilidades con Media.

## Documentación relacionada

- [[MODULES]]
- [[MEDIA]]
- [[FOLDERS]]