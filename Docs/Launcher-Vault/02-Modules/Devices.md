---

title: Devices module_id: devices type: module status: pending-verification tags:

- launcher/module
- launcher/system

---

# Devices

## Responsabilidad

Gestionar y mostrar información sobre dispositivos conectados al equipo.

## Funcionalidades que investigar

- Enumeración de dispositivos.
- Información de hardware.
- Identificación de dispositivos.
- Estado de conexión.
- Información de dispositivos multimedia.
- Posibles acciones de gestión.

## Dependencias que investigar

- System.Management.
- Windows APIs.
- Core.
- Models.
- Servicios de monitorización.

## Auditoría

- [ ] Localizar implementación.
- [ ] Verificar registro.
- [ ] Identificar fuentes de información.
- [ ] Revisar consultas WMI.
- [ ] Revisar actualización de estados.
- [ ] Analizar permisos.
- [ ] Revisar gestión de errores.
- [ ] Comprobar liberación de recursos.

## Riesgos

- Consultas WMI costosas.
- Diferencias entre versiones de Windows.
- Dispositivos que desaparecen durante una consulta.
- Confusión entre dispositivos físicos y lógicos.

## Documentación relacionada

- [[MODULES]]
- [[MONITOR]]
- [[NETWORK]]