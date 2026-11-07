# Migración a arquitectura hexagonal

> **Estado: en curso.** Documento vivo: se actualiza en cada tarea de la
> migración. Última actualización: **2026-11-06**.

## Por qué

`Warehouse-API` nació como monolito en `src/Api`: controladores, servicios,
repositorios y EF Core convivían en el mismo proyecto. La migración busca
aislamiento real de responsabilidades y testabilidad sin tocar lo que ya
funciona:

- **Dominio y casos de uso sin dependencias de infraestructura** (EF, JWT,
  BCrypt) detrás de interfaces.
- **Adaptadores reemplazables** en `WMS.Infrastructure`.
- Un **composition root único** (`WMS.Api`) que ensambla las capas.

Ver [ADR-0001](docs/adr/0001-arquitectura-hexagonal.md) para la decisión
completa y sus alternativas.

## Reglas inviolables de la migración

1. **El contrato HTTP no cambia.** El frontend (`Warehouse-Client`) está
   terminado: rutas, códigos de estado y shape de respuestas son la fuente de
   verdad. Si algo obligara a romperlo, se detiene la migración y se coordina
   con el cliente en el mismo ciclo.
2. **El esquema de BD está congelado salvo migración explícita aprobada.**
   Prohibidas las migraciones sorpresa: si un módulo exige una — precedente
   T9, `users.description` heredada de `Model`, columna nueva, aditiva,
   nullable y reversible, sin efecto sobre el contrato HTTP — se aprueba y
   revisa en su tarea (su `Up()`/`Down()`) y el gate vuelve a verde. Gate:
   `dotnet ef migrations has-pending-model-changes`
   debe responder *"No changes have been made to the model since the last
   migration"*.
3. **Build y tests siempre en verde.** Cada tarea se cierra con la solución
   compilando sin errores ni advertencias y toda la suite en verde.
4. **Pasos pequeños y revisables.** Un commit por paso, Conventional Commits,
   con los gates ejecutados antes de proponerlo.

## Estado de las tareas

| ID | Tarea | Estado | Commit |
|---|---|---|---|
| T1 | Composition root en `WMS.Api` (Program, Config, middleware, controladores) | ✅ | `58a2ffb` |
| T2 | CI (`ci.yml`: build + test + artefactos) | ✅ | `f790db9` |
| T3 | Plantillas de issues/PR y Dependabot | ✅ | `962f058` |
| R1 | Partir `AppDbContext` en `IEntityTypeConfiguration<T>` | ✅ | `9d8c9bc` |
| R2 | Unit tests NUnit del hasher (`WMS.Infrastructure.Test`) | ✅ | `be2f1c5` |
| T4 | `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` | ✅ | `7c0fef4` |
| T5 | `Directory.Build.props` + `.editorconfig` + formato normalizado | ✅ | `66397aa` |
| T6 | `MIGRATION.md` + ADR-0001 + README (badges, estructura, recuentos) | ✅ | este commit |
| — | **Traslado del clúster EF a `WMS.Infrastructure`** (con namespaces `WMS.*`) | ✅ | `51442c1` |
| — | Adaptador `JwtTokenService` (código muerto, opción A) | ✅ | `75fd07f` |
| T7 | Feature tests a xUnit en `WMS.Api.Test` (base, factory y 5 suites) | ✅ | `3a0f616` |
| T8 | Limpieza tras el traslado de tests: docs, paquetes y recuentos | ✅ | este commit |
| T9 | Módulo Users completo a `WMS.Application` (servicio, interfaces y DTOs), entidad `User`/`RefreshToken` y `Api.Common` repartidos en `WMS.Domain`/`WMS.Application`, puerto `IRefreshTokenRepository` adelantado y migración `AddDescriptionToUsers` | ✅ | este commit |
| T10 | Módulo Products completo a `WMS.Application`; `Product` = scaffold `: Model` (−location, +`[Table("products")]`) que sustituye a la entity de `Api`, strings EF reescritos | ✅ | este commit |
| T11 | Módulo Warehouses completo a `WMS.Application`; `Warehouse` = scaffold `: Model` (namespace corregido) con migración `AddDescriptionToWarehouses` | ✅ | este commit |
| T12 | Módulo Inventory a `WMS.Application`; `InventoryItem` (tabla puente, sin `Model`) → `WMS.Domain.Entities`, cero migración, y usings muertos `Api.Auth` fuera de Infrastructure | ✅ | `ffd530a` |
| T13 | Auth + security a `WMS.Application`/`WMS.Infrastructure/Auth`; borrado el código muerto de `75fd07f` (adaptadores alternativos + sus puertos); `src/Api` queda sin código; cero migración | ✅ | este commit |
| T14–T15 | Teardown del puente `Infrastructure → Api` y de `src/Api`/`src/Test` | ⬜ | |

## Qué queda por mover (T14–T15) y por qué así

Desde T12, `WMS.Infrastructure` **no usa ningún tipo de `Api.*` en código**:
solo queda la referencia de proyecto (el "puente"), que retira T14. Y desde
T13 `src/Api` **ya no contiene ningún `.cs`** (solo el `Api.csproj` y los
`appsettings`). El cierre:

- **Entidades** → `WMS.Domain/Entities`. *Ojo*: el `AppDbContextModelSnapshot`
  y los `Designer` de las migraciones referencian cada entidad por **nombre
  completo en strings** (`modelBuilder.Entity("WMS.Domain.Entities.InventoryItem")`).
  Renombrarlas exige actualizar esos ficheros generados y pasar el gate del
  esquema; T9–T12 ya lo hicieron para `User`, `RefreshToken`, `Product`,
  `Warehouse` e `InventoryItem`.
  (`WMS.Domain/Entities` aún contiene scaffolds sin migrar — `Inventory.cs`,
  `Location.cs`, `Category.cs`… — que aún no se referencian. `Movement.cs`,
  `DependencyInjection.cs` y `Persistence/AppDbContext.cs` estuvieron vacíos
  como marcadores; los dos primeros siguen esperando su módulo.)
- **Casos de uso y servicios** → `WMS.Application`: **hecho** (T9 Users,
  T10 Products, T11 Warehouses, T12 Inventory, T13 Auth).
- **Interfaces de repositorio** → junto a sus casos de uso (los puertos de
  EF) y sus implementaciones quedan ya en `WMS.Infrastructure/Persistence/Repositories`.
- T14 retira la referencia `WMS.Infrastructure → Api`; T15 hace desaparecer
  `src/Api` y reubica `src/Test`.

## Gates de verificación (ejecutar en cada tarea)

```bash
# 1. Compilación sin errores ni advertencias
dotnet build WarehouseApi.sln

# 2. Toda la suite en verde (hoy: 196 pruebas)
dotnet test WarehouseApi.sln

# 3. Esquema congelado
dotnet ef migrations has-pending-model-changes \
  --project src/WMS.Infrastructure --startup-project src/WMS.Api

# 4. Formato conforme al .editorconfig
dotnet format WarehouseApi.sln --verify-no-changes
```

Si la tarea toca `.csproj`, `Directory.Build.props` o el `Dockerfile`:

```bash
docker build -t warehouse-api .
```

## Decisiones ya tomadas

| Decisión | Por qué |
|---|---|
| **EF completo en `WMS.Infrastructure`** (contexto, configuraciones, migraciones, repositorios) | Es el adapter de persistencia; además EF descubre las migraciones en el assembly del contexto, así que viajan juntas |
| **Entidades fuera de `WMS.Domain` hasta T9–T15** | El snapshot las referencia por nombre completo en strings: moverlas sin actualizar los generados rompería el gate del esquema |
| **Cópias de RFID: opción A** (`JwtTokenService` en `WMS.Infrastructure`) | Dejadas como código muerto que compila; se reescriben contra `AuthOptions` real al migrar Auth |
| **`TreatWarningsAsErrors` en `false`** (documentado en `Directory.Build.props`) | No bloquear el build con avisos heredados durante la migración; se activa al cerrarla |
| **No reutilizar del proyecto RFID**: `ValidationException`/middleware (Warehouse ya tiene `DomainException`), `Login.ExecuteAsync` (rompería los tests existentes), seeders `admin/123`, `UseSnakeCaseNamingConvention` (reventaría el esquema congelado), DI `AddInfrastructure` (clave `Database` vs `DefaultConnection`), `Domain/Ports` (aquí es `<módulo>/interfaces/`) | Cada pieza encaja con las convenciones y decisiones ya establecidas en este repo |

## Cómo se verá al terminar (T15)

```text
src/
├── WMS.Api            # composition root: host, controladores, config
├── WMS.Domain         # entidades y reglas de dominio (sin dependencias)
├── WMS.Application    # casos de uso + puertos (interfaces)
├── WMS.Infrastructure # adaptadores: EF, JWT, BCrypt (sin referencias a src/Api)
└── WMS.*.Test         # tests por módulo (NUnit) + WMS.Api.Test (feature, xUnit)
```

`src/Api` habrá desaparecido y `src/Test` se habrá vaciado hacia los proyectos
de test de cada módulo.
