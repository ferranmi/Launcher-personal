---
title: Technical Debt
type: technical-debt
project: Launcher
audit_commit: 5c621accde912fc8dda48873fb44d4d93857901a
status: initial-register
tags: [launcher/technical-debt, launcher/audit]
---

# Deuda técnica

|ID|Área|Descripción|Prioridad|
|---|---|---|---:|
|TD-001|Persistencia|Escritura JSON heterogénea y no atómica|P1|
|TD-002|Ciclo de vida|IModule no define activación/desactivación/liberación|P2|
|TD-003|Servicios|Mezcla de singletons, estáticos e instancias por vista; ownership a documentar|P2|
|TD-004|Monitor|Muestreo separado en host y vista|P2|
|TD-005|Audio|AudioMixerService no libera explícitamente recursos COM|P2|
|TD-006|Web|Sesiones Playwright sin cleanup central visible|P1|
|TD-007|Módulos|Search implementado pero no registrado; Assistant es ventana|P2|
|TD-008|Orden|Valores Order duplicados|P3|
|TD-009|MVVM|Convivencia de code-behind y modelos observables|P3|
|TD-010|Modelos|Algunos modelos contienen referencias WPF/NAudio junto a datos|P3|
|TD-011|Dependencias|Versiones flotantes System.Management y Microsoft.Playwright|P2|
|TD-012|Pruebas|No se encontró proyecto de tests ni workflow CI en el árbol inspeccionado|P2|
|TD-013|Docs|Frontmatter y enlaces Obsidian inconsistentes|P2|
|TD-014|Docs|README describe snapshot y versión antiguos|P1|

## Criterios
1. Resolver primero exposición de credenciales, pérdida de datos y recursos que sobreviven al cierre.
2. Medir antes de optimizar.
3. No introducir framework MVVM externo.
4. No añadir abstracciones o dependencias sin necesidad demostrada.
5. Mantener compatibilidad con JSON existente.
6. No refactorizar módulos por uniformidad estética.

Los registros describen deuda detectada, no autorizan implementación.
