---
title: Settings Implementation Plan
type: implementation-plan
project: Launcher
source_of_truth: GitHub main
audit_ref: main @ tree 2c81caa430d49221a2e8cd7d5981e0aea688c0ef
last_reviewed: 2026-11-12
status: proposed
tags: [launcher, settings, implementation]
---

# Settings — Plan de implementacion

## Objetivo

Convertir Settings en el centro unico de administracion de Launcher y sus modulos, conservando la arquitectura existente y evitando cambios automaticos en disco mientras el usuario edita.

## Estructura visual

- Sidebar interno fijo con categorias y subcategorias.
- Panel de contenido con scroll vertical continuo.
- Cada categoria/subcategoria apunta a una seccion del mismo scroll.
- La categoria activa se actualiza al desplazarse.
- Guardar y Descartar permanecen en la parte inferior de la barra lateral.
- Indicador de cambios pendientes y numero de modificaciones.

## Modelo de configuracion

Cada parametro debe clasificarse con uno de estos ambitos:

- Global: una fuente de verdad para Launcher.
- Compartido: una fuente de verdad utilizada por varios modulos.
- Heredado: el modulo utiliza el valor global mientras no tenga una sobrescritura.
- Especifico: pertenece a un unico modulo y no necesita etiqueta visual.
- Sobrescritura: valor especifico del modulo que sustituye el global para ese modulo.

Las etiquetas GLOBAL, COMPARTIDO y SOBRESCRIBE GLOBAL deben explicar el alcance y enumerar los modulos afectados.

## Sesion de edicion

1. Al abrir Settings se crea una copia de trabajo de los valores persistidos.
2. Los controles solo modifican esa copia.
3. Cambiar de categoria no persiste nada.
4. Guardar valida todos los valores, persiste y solo entonces aplica efectos.
5. Si el guardado falla, la sesion de trabajo se conserva y los modulos siguen con el ultimo estado confirmado.
6. Descartar recarga los valores persistidos.
7. Al cerrar Settings o Launcher con cambios pendientes se pregunta si guardar, descartar o seguir editando.

## Persistencia y efectos

El estado actual esta repartido entre varios JSON y el registro de Windows. No debe implementarse una falsa transaccion que actualice algunos destinos y deje otros a medias.

Antes de conectar cada fuente: definir como se prepara una copia de trabajo; validar sin mutar el estado activo; guardar con escritura temporal y reemplazo seguro; comprobar el resultado; aplicar efectos secundarios solo tras guardar; conservar la copia de trabajo si falla una escritura.

El registro de Windows y los cambios de atajo/ventana/tema requieren aplicar y revertir efectos de forma controlada. No se deben ejecutar al mover un control.

## Acciones, no preferencias

Acciones como reparar portadas, restaurar elementos ignorados, comprobar/aplicar actualizaciones o abrir carpetas son operaciones explicitas. No forman parte del ciclo Guardar/Descartar salvo que se transformen expresamente en un flujo de configuracion.

Los dialogos de configuracion de Steam, ImageGen y otras areas deben migrarse a secciones centrales cuando sus parametros y validaciones esten inventariados.

## Secuencia incremental

### Fase 0 — Auditoria (actual)
- Identificar fuentes de configuracion.
- Enumerar parametros y consumidores.
- Distinguir preferencias de acciones.
- Documentar riesgos de persistencia y efectos.

### Fase 1 — Modelo de edicion
- Introducir copia de trabajo y comparacion de cambios.
- No modificar todavia los archivos de configuracion de modulos no auditados.
- Validar Guardar/Descartar con preferencias de AppSettingsService.

### Fase 2 — Interfaz y navegacion
- Sidebar interno, scroll continuo, anclas y categoria activa.
- Guardar/Descartar persistentes en sidebar.
- Advertencias de alcance.

### Fase 3 — Integracion por modulo
- Games y LibraryPathsService.
- ImageGen.
- Models.
- Monitor/Processes y sus parametros compartidos.
- Resto de modulos tras inspeccion.

### Fase 4 — Validacion
- Build Windows .NET 10.
- Guardar y reiniciar.
- Descartar y reiniciar.
- Navegar con cambios pendientes.
- Cierre con cambios pendientes.
- Simular fallo de escritura.
- Verificar que ningun modulo observa valores no confirmados.
- Verificar herencia y sobrescritura.

## Riesgos abiertos

- Varios archivos JSON no se pueden guardar atomicamente como una unica operacion.
- El registro de Windows no participa en una transaccion de archivos.
- Algunas vistas consumen propiedades singleton directamente.
- Algunas opciones aplican efectos en tiempo real.
- SteamApiKey se encuentra actualmente en settings.json; no debe exponerse en una nueva vista sin tratamiento de credenciales.
- No se ha ejecutado build ni pruebas en esta auditoria.

## Criterio de salida

No considerar terminada la implementacion hasta que todas las configuraciones existentes tengan propietario, ambito, fuente de datos, validacion, politica de guardado y consumidor documentados.