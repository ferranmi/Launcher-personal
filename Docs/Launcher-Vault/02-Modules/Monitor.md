---

title: Monitor module_id: monitor type: module status: documented-pending-verification tags:

- launcher/module
- launcher/system

---

# Monitor

## Responsabilidad

Mostrar información de rendimiento y estado del sistema.

## Funcionalidades documentadas

- Monitorización del sistema.
- Información de procesador.
- Memoria.
- Discos.
- GPU.
- Información de rendimiento.

## Componentes identificados

- `MonitorModule`.
- `MonitorView`.
- `SystemMonitorService`.
- `SystemSnapshot`.
- `DriveStat`.

## Dependencias que investigar

- System.Management.
- System.Diagnostics.PerformanceCounter, si sigue presente.
- Windows APIs.
- Core.

## Auditoría

- [ ] Verificar métricas disponibles.
- [ ] Identificar frecuencia de actualización.
- [ ] Revisar consultas WMI.
- [ ] Revisar contadores.
- [ ] Comprobar compatibilidad GPU.
- [ ] Revisar concurrencia.
- [ ] Revisar consumo de CPU.
- [ ] Revisar tratamiento de valores no disponibles.
- [ ] Revisar liberación de recursos.

## Riesgos

- Consultas frecuentes y costosas.
- Contadores no disponibles.
- Diferencias de hardware.
- Bloqueos de interfaz.
- Lecturas incorrectas de memoria o GPU.

## Documentación relacionada

- [[MODULES]]
- [[DEVICES]]
- [[PROCESSES]]
- [[MODELS]]