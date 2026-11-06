# Política de seguridad de Warehouse API

Gracias por dedicar tiempo a mejorar la seguridad de este proyecto.

## Versiones soportadas

Solo se mantiene la rama principal (`main`). No se publican versiones
etiquetadas; los parches se aplican directamente en `main`.

| Versión | Soporte |
|---|---|
| `main` | ✅ |
| Cualquier otra rama o fork | ❌ |

## Reportar una vulnerabilidad

**No abras un issue público** para reportar una vulnerabilidad.

Elige uno de estos canales privados:

1. **GitHub Security Advisories** (preferido):
   [Reportar una vulnerabilidad](https://github.com/ThejavaZ/Warehouse-API/security/advisories/new)
2. **Correo electrónico**: [jsdash10000@gmail.com](mailto:jsdash10000@gmail.com)
   con asunto `[SECURITY] Warehouse API`.

Incluye en el reporte:

- Descripción de la vulnerabilidad y su impacto.
- Pasos mínimos para reproducirla (endpoint, payload, configuración afectada).
- Versión o commit (`git rev-parse HEAD`) donde se reproduce, si aplica.
- CWE/CVE asociados, si los conoces.
- Cualquier arreglo o mitigación que hayas identificado (opcional).

### Qué esperar

- Confirmación de recepción en un plazo razonable tras enviar el reporto.
- Evaluación del impacto y, si se confirma, coordinación contigo antes de
  publicar el fix y la divulgación.
- Crédito en el changelog si lo deseas (salvo que prefieras anonimato).

## Buenas prácticas de investigación

Al investigar esta API en buena fe:

- No explotes datos de usuarios ni realices pruebas de carga o denegación de
  servicio.
- No accedas a datos que no te pertenezcan; si encuentras datos expuestos,
  detente y reporta el hallazgo.
- Da tiempo al proyecto para parchear antes de cualquier divulgación pública.
- Respeta la privacidad: elimina cualquier dato accedido durante la prueba
  una vez reportado.

Estas medidas se consideran buena fe y no deberían interpretarse como
incumplimiento de la ley.

## Alcance

Están en el alcance:

- Autenticación y autorización (JWT, refresh tokens, control de acceso).
- Inyección SQL u otros inyectables vía la API.
- Gestión de secretos (no deben filtrarse en repositorio ni en respuestas).
- Validación de entrada y manejo de errores que expongan información interna.
- Dependencias con vulnerabilidades conocidas (Dependabot revisa el repo).

Fuera de alcance:

- El frontend `Warehouse-Client` (repositorio aparte).
- Problemas de configuración del entorno de despliegue (Docker, red, SO) que
  no sean imputables al código de este repositorio.
