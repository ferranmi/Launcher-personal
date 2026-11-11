---

title: Games module_id: games type: module status: documented-pending-verification tags:

- launcher/module
- launcher/games

---

# Games

## Responsabilidad

Gestionar una biblioteca unificada de juegos y permitir su lanzamiento.

## Funcionalidades documentadas

- Biblioteca de juegos.
- Integración con Steam.
- Integración con Epic Games.
- Juegos manuales.
- Portadas.
- Favoritos.
- Ocultación.
- Búsqueda y filtros.
- Tiempo jugado.
- Sincronización.
- Gestión de juegos ignorados.

## Componentes identificados

- `GamesModule`.
- `GamesView`.
- `GameLibraryService`.
- `IGameProvider`.
- `SteamGameProvider`.
- `EpicGameProvider`.
- `Game`.
- `GameSource`.
- `CoverArtService`.

## Persistencia documentada

- `games.json`.
- `ignored-games.json`.
- Directorio de portadas.

## Auditoría

- [ ] Verificar proveedores.
- [ ] Revisar sincronización.
- [ ] Validar manifiestos de Steam.
- [ ] Validar manifiestos de Epic.
- [ ] Revisar ejecución de juegos.
- [ ] Revisar medición de tiempo.
- [ ] Revisar descarga de portadas.
- [ ] Revisar concurrencia.
- [ ] Revisar persistencia.
- [ ] Revisar seguridad de rutas y ejecutables.

## Riesgos

- Cambios en formatos de manifiestos.
- Portadas incorrectas.
- Descargas lentas.
- Juegos lanzados mediante protocolo sin proceso directamente controlable.
- Datos duplicados durante sincronización.

## Documentación relacionada

- [[MODULES]]
- [[DASHBOARD]]
- [[FOLDERS]]
- [[SETTINGS]]