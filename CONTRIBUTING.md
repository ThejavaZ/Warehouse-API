# Contribuir a Warehouse API

Gracias por tu interés en contribuir a Warehouse API.

Este documento describe las pautas básicas para contribuir al proyecto.

## 1. Código de conducta

Se espera que toda persona contribuyente mantenga un entorno respetuoso,
colaborativo y constructivo. Consulta el [Código de conducta](CODE_OF_CONDUCT.md).

## 2. Antes de contribuir

Antes de empezar:

1. Revisa los issues abiertos (hay plantillas de bug y de feature) para no duplicar trabajo.
2. Discute los cambios significativos antes de implementarlos.
3. Mantén las contribuciones enfocadas y fáciles de revisar.
4. Sigue la arquitectura y las convenciones existentes.
5. **El contrato HTTP es intocable**: el frontend (`Warehouse-Client`) ya está
   terminado. Si un cambio modifica rutas, códigos de estado o el shape de una
   respuesta, se coordina en el mismo ciclo con el cliente.

## 3. Entorno de desarrollo

Requisitos:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (para PostgreSQL vía `docker compose`)
- `dotnet-ef` solo si vas a trabajar con migraciones:
  `dotnet tool install --global dotnet-ef`

La forma más rápida de levantar el entorno:

```bash
docker compose up -d          # PostgreSQL en localhost:5433 (credenciales en .env)
dotnet run --project src/WMS.Api   # API en http://localhost:5259/swagger
```

La cadena de conexión y el secreto JWT **no están en el repositorio**: en
desarrollo se configuran con [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
y en producción con variables de entorno. El README (sección de configuración)
tiene los comandos exactos. **Nunca** subas secretos a Git.

## 4. Comandos esenciales

| Comando | Para qué |
|---|---|
| `dotnet build WarehouseApi.sln` | Compilar toda la solución |
| `dotnet test WarehouseApi.sln` | Ejecutar todos los tests |
| `dotnet run --project src/WMS.Api` | Arrancar la API (puerto 5259) |
| `dotnet ef migrations has-pending-model-changes --project src/WMS.Infrastructure --startup-project src/WMS.Api` | Verificar que el modelo EF coincide con las migraciones |
| `dotnet ef database update --project src/WMS.Infrastructure --startup-project src/WMS.Api` | Aplicar migraciones pendientes |

## 5. Estructura y arquitectura

El proyecto sigue una **arquitectura hexagonal** (puertos y adaptadores):

| Proyecto | Responsabilidad |
|---|---|
| `src/WMS.Api` | Composition root: host, controladores, configuración, middleware |
| `src/WMS.Domain` | Entidades de dominio (sin dependencias) |
| `src/WMS.Application` | Casos de uso y **puertos** (interfaces) |
| `src/WMS.Infrastructure` | **Adaptadores**: persistencia EF, JWT, hashing BCrypt |
| `src/Api` | Monolito original **en migración** — servicios y entidades que aún no han pasado a las capas `WMS.*` |
| `src/Test` | Tests (NUnit) del código que aún vive en `src/Api` |
| `src/WMS.*.Test` | Tests por módulo (NUnit); **`WMS.Api.Test`**: feature tests HTTP (xUnit) |

Reglas que respeta cualquier contribución:

- Las dependencias apuntan hacia dentro: `Api → Application → Domain` en sentido
  inverso; `Infrastructure` implementa los puertos de `Application`.
- **Entity Framework solo en `WMS.Infrastructure`** (el `AppDbContext`, sus
  configuraciones y las migraciones viven en `Persistence/`).
- Los servicios y casos de uso dependen de **interfaces** (`IUserRepository`),
  nunca de clases concretas de adaptadores.
- `src/Api` es un puente temporal de la migración: no le añandas capacidades
  nuevas que puedan ir directamente a `WMS.*`.

Sigue SOLID y KISS sin introducir complejidad prematura.

## 6. Estrategia de ramas

Usa nombres descriptivos:

| Prefijo | Propósito |
|---|---|
| `feature/` | Nueva funcionalidad |
| `fix/` | Corrección de bugs |
| `refactor/` | Mejoras de estructura sin cambiar comportamiento |
| `docs/` | Cambios de documentación |
| `chore/` | Mantenimiento y configuración |
| `test/` | Añadido o mejora de tests |

Ejemplos:

```text
feature/refresh-token-rotation
fix/auth-expired-token-message
refactor/ef-repository
docs/update-contributing
chore/ci-cache-nuget
```

## 7. Convención de commits

Sigue [Conventional Commits](https://www.conventionalcommits.org/) y escribe los
mensajes **en inglés**:

```text
<type>(<scope>): <description>
```

| Tipo | Descripción |
|---|---|
| `feat` | Nueva funcionalidad |
| `fix` | Corrección de bugs |
| `refactor` | Reestructuración de código |
| `docs` | Documentación |
| `test` | Tests |
| `chore` | Mantenimiento |
| `ci` | Integración continua |
| `style` | Formato |

Scopes habituales: `api`, `auth`, `users`, `products`, `warehouses`,
`inventory`, `infrastructure`, `application`, `domain`, `ci`, `github`.

Ejemplos reales del repositorio:

```text
refactor(infrastructure): move ef persistence to wms.infrastructure
test(infrastructure): add password hasher unit tests
ci: add dotnet build and test workflow
```

## 8. Tests (obligatorio)

- **Unit tests en NUnit** en todos los módulos. Hoy viven en `src/Test`
  (`[TestFixture]` / `Assert.That` / `Method_Condition_Expectation`); los módulos
  migrados los reciben en su proyecto `WMS.*.Test`.
- **Feature tests en xUnit** (`WMS.Api.Test`) con `WebApplicationFactory` y
  SQLite in-memory.
- Toda funcionalidad nueva lleva sus tests; un cambio sin tests solo se acepta
  si no es testeable de forma razonable y se justifica en el PR.

### Gates antes de abrir un PR

1. `dotnet build WarehouseApi.sln` → **0 errores y 0 advertencias**
2. `dotnet test WarehouseApi.sln` → **en verde**
3. Si tocas EF: `dotnet ef migrations has-pending-model-changes ...` →
   *"No changes have been made to the model since the last migration"*
4. `git diff` sin secretos (`appsettings.json`, `.env`, user secrets)

La CI (`.github/workflows/ci.yml`) ejecuta build y test en cada push y PR.

## 9. Pull Requests

Los PRs deben:

1. Explicar el propósito del cambio.
2. Describir lo implementado.
3. Referenciar los issues relacionados (`Closes #123`).
4. Marcar en la checklist cómo se ha verificado.
5. Indicar si tocan el contrato HTTP (ver sección 2).

Usa la plantilla de PR del repositorio. Mantén cada PR enfocado en una sola
característica o mejora.

## 10. Licencia

Al contribuir a Warehouse API aceptas que tus contribuciones se licencien bajo
la **Licencia MIT** (ver [LICENSE](LICENSE)).

Conservas la titularidad de tus contribuciones y otorgas los derechos que
establece la licencia del proyecto.

## 11. Preguntas y sugerencias

Para dudas, sugerencias o bugs, usa los [Issues](https://github.com/ThejavaZ/Warehouse-API/issues)
con las plantillas disponibles.

Para reportar **vulnerabilidades de seguridad**, no abras un issue: consulta la
[política de seguridad](SECURITY.md).

¡Gracias por ayudar a mejorar Warehouse API!
