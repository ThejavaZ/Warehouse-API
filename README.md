# Warehouse API

API REST para gestión de almacén construida con **ASP.NET Core 8**, **EF Core** y **PostgreSQL**.

El módulo disponible actualmente es **Users**: CRUD con paginación, filtros dinámicos, borrado lógico y contraseñas hasheadas.

---

## Requisitos

- [.NET SDK 8.0.424](https://dotnet.microsoft.com/download/dotnet/8.0) (fijado en `global.json`)
- PostgreSQL 14 o superior
- Opcional: Docker y Docker Compose (para levantar todo sin instalar PostgreSQL local)

---

## Puesta en marcha

### Opción A — Docker (recomendada en desarrollo)

Levanta la API y una PostgreSQL 18 aislada, sin tocar tu base local:

```bash
cp .env.example .env       # ajusta POSTGRES_PASSWORD
docker compose up --build
```

- API y Swagger: `http://localhost:5259/swagger`
- PostgreSQL (para psql/otras herramientas): `localhost:5433`, credenciales del `.env`
- Las migraciones se aplican solas al arrancar (`Database:ApplyMigrationsOnStartup`).
- El volumen `pgdata` persiste los datos; `docker compose down -v` lo elimina.

> `.env` está en `.gitignore`: nunca se versiona. La cadena de conexión se arma en el compose a partir de esas variables.

### Opción B — Manual

#### 1. Restaurar y compilar

```bash
dotnet restore
dotnet build
```

#### 2. Configurar la cadena de conexión (fuera del código)

La cadena de conexión **no está en el repositorio**. En desarrollo se guarda con [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=warehouse_db;Username=<usuario>;Password=<password>" \
  --project src/Api
```

En producción usa una variable de entorno en lugar de user secrets:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
```

> **Nunca** subas credenciales a Git. Verifica con `git diff` que `appsettings.json` no contenga la cadena antes de commitear.

#### 3. Aplicar migraciones

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Api
```

#### 4. Ejecutar

```bash
dotnet run --project src/Api
```

- Swagger UI: `http://localhost:5259/swagger`
- Health/info: `GET /` → versión y entorno

---

## Endpoints

Base path: `/api/User`

| Método | Ruta | Descripción | Éxito |
|---|---|---|---|
| `GET` | `/api/User` | Listado paginado con filtros | `200` `PagedResult<UserListViewModel>` |
| `GET` | `/api/User?id={id}` | Detalle de un usuario | `200` `UserViewModel` |
| `POST` | `/api/User` | Crea un usuario | `201` `UserViewModel` |
| `PUT` | `/api/User/{id}` | Actualiza un usuario | `200` `UserViewModel` |
| `PATCH` | `/api/User/{id}` | Actualización parcial (mismo modelo que PUT) | `200` `UserViewModel` |
| `DELETE` | `/api/User/{id}` | Borrado lógico (`SoftDelete`) | `204` |

### Parámetros de consulta (`UserQuery`)

| Parámetro | Tipo | Default | Descripción |
|---|---|---|---|
| `id` | `int?` | `null` | Si viene con valor, devuelve el detalle en vez del listado |
| `search` | `string?` | `null` | Búsqueda case-insensitive por **nombre** o **correo** |
| `isActive` | `bool?` | `null` | Filtra por estado solo si se envía |
| `page` | `int` | `1` | Página (mínimo `1`) |
| `pageSize` | `int` | `10` | Tamaño de página (`1`–`100`) |
| `orderDesc` | `bool` | `true` | Orden por `Id` descendente/ascendente |

Los filtros se combinan entre sí. El conteo (`TotalItems`) se calcula **después** de filtrar y **antes** de paginar.

### Ejemplos

```bash
# Listado con filtros combinados
GET /api/User?search=ana&isActive=true&page=1&pageSize=20

# Detalle por id
GET /api/User?id=5

# Crear
POST /api/User
{ "name": "Ana Garcia", "email": "ana@test.com", "password": "Secret123!" }

# Actualizar (contraseña opcional: si se omite o va vacía, se conserva la actual)
PUT /api/User/5
{ "name": "Ana G.", "email": "ana@test.com", "password": "" }

# Borrado lógico
DELETE /api/User/5
```

---

## Manejo de errores

Todas las respuestas de error usan **ProblemDetails (RFC 7807)** con `Content-Type: application/problem+json` y el campo adicional `code`.

| Código HTTP | Origen | `code` |
|---|---|---|
| `400` | Validación de DTO / parámetros de paginación | `validation_error` |
| `404` | Usuario inexistente o dado de baja | `not_found` |
| `403` | `UnauthorizedAccessException` | `forbidden` |
| `409` | `DomainException` de conflicto | `conflict` |
| `500` | Excepción no controlada | `internal_error` |

Ejemplo de respuesta `404`:

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Not Found",
  "status": 404,
  "detail": "User with ID 42 not found.",
  "instance": "/api/User",
  "code": "not_found",
  "traceId": "0HN..."
}
```

**Los detalles internos de las excepciones nunca se devuelven al cliente**: quedan únicamente en los logs. La validación automática de `[ApiController]` está configurada en `ValidationConfiguration` para que también responda `application/problem+json`.

El manejo está centralizado en [`src/Api/middleware/ErrorHandlingMiddleware.cs`](src/Api/middleware/ErrorHandlingMiddleware.cs); los controladores no usan `try/catch`.

---

## Seguridad

- **Contraseñas**: se almacenan con **BCrypt** (salt integrado, work factor 11), nunca en texto plano.
  `BCryptPasswordHasher` implementa la abstracción `IPasswordHasher<User>` de ASP.NET Core Identity.
- **Nunca se expone `Password`**: ni en `UserViewModel` ni en `UserListViewModel` (verificado por pruebas).
- **Correos únicos**: índice único en `Email` **filtrado** por `SoftDelete IS NULL`, de modo que un correo dado de baja puede reutilizarse pero no duplicarse mientras esté activo.
- **Autenticación/Autorización**: Identity está registrado (`IdentityConfiguration`) y Swagger trae el esquema `Bearer` listo, pero aún **no hay endpoints de login** ni políticas aplicadas. Ese es el siguiente paso antes de exponer operaciones privadas.

### Validación

DTOs validados con Data Annotations y validados automáticamente por `[ApiController]`:

- `Name`: obligatorio, 2–100 caracteres
- `Email`: obligatorio, formato de correo
- `Password`: obligatorio en creación (mín. 8); opcional en actualización (mín. 8 si se envía)
- `page` / `pageSize`: `Range` sobre los límites permitidos

---

## Estructura del proyecto

```
Warehouse-API/
├── WarehouseApi.sln
├── src/
│   ├── Api/
│   │   ├── Program.cs                    # Pipeline y composición
│   │   ├── config/                       # Configuración por capas (BD, CORS, Swagger, Identity, validación…)
│   │   ├── Common/                       # PagedQuery, PagedResult, DomainException
│   │   ├── middleware/
│   │   │   └── ErrorHandlingMiddleware.cs  # ProblemDetails global
│   │   ├── security/
│   │   │   └── BCryptPasswordHasher.cs    # IPasswordHasher<User> con BCrypt
│   │   ├── data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── AppDbContextDesignTimeFactory.cs
│   │   │   └── migrations/
│   │   └── users/
│   │       ├── UserController.cs          # HTTP, sin try/catch
│   │       ├── UserService.cs             # Reglas de negocio + mapeo
│   │       ├── UserRepository.cs          # Consultas IQueryable + EF Core
│   │       ├── interfaces/                # IUserService, IUserRepository
│   │       └── models/                    # Entity, DTOs, Query, ViewModels
│   └── Test/
│       ├── UserServiceTests.cs            # Reglas de negocio
│       ├── UserRepositoryTests.cs         # Consultas contra SQLite en memoria
│       ├── ErrorHandlingMiddlewareTests.cs
│       ├── ValidationTests.cs
│       ├── BCryptPasswordHasherTests.cs
│       └── Integration/                   # WebApplicationFactory + SQLite
```

### Flujo de una petición

```
Controller  →  Service  →  Repository  →  EF Core  →  PostgreSQL
   HTTP      reglas de     IQueryable     SQL         datos
             negocio       + mapeo
```

- **Controller**: solo traduce HTTP. Lanza/mapea estados, delega al servicio.
- **Service**: reglas de negocio (existencia de correo, borrado lógico, hash), sanitiza la paginación y devuelve ViewModels.
- **Repository**: construye la consulta de forma progresiva con `IQueryable` y la ejecuta contra la BD.

---

## EF Core — decisiones

- `AsNoTracking()` en todas las lecturas (`GetAllUsersAsync`, `GetUserByIdAsync`, `GetUserByEmailAsync`).
- `CancellationToken` propagado desde el controlador hasta `SaveChangesAsync`.
- `CountAsync()` se ejecuta con los filtros aplicados y **antes** del `Skip`/`Take`.
- Un único `SaveChangesAsync` ya es atómico (transacción implícita de EF Core). `BeginTransactionAsync` solo haría falta si participaran varios `DbContext` o operaciones ajenas a EF Core.
- Diseño de migraciones resuelto sin arrancar la API vía `AppDbContextDesignTimeFactory`.

---

## Pruebas

```bash
dotnet test
```

**71 pruebas** con NUnit. Cubren:

- Reglas del `UserService` (hash, correos duplicados, mapeo, ausencia de `Password` en ViewModels).
- Consultas del repositorio contra **SQLite en memoria** (filtros combinados, paginación, borrado lógico, índice único de correo).
- `ErrorHandlingMiddleware` → 400/403/404/409/500 y ausencia de detalles internos en 500.
- Validaciones de Data Annotations (incluidos límites de paginación).
- **Integración HTTP** con `WebApplicationFactory`: 200/201/204/400/404 reales.

---

## Swagger / OpenAPI

Documentado en `SwaggerConfiguration` y con comentarios XML generados desde el código:

- Parámetros de `UserQuery`
- Códigos de estado por endpoint (`200`, `201`, `204`, `400`, `404`, `500`)
- Esquemas `ProblemDetails` / `ValidationProblemDetails`
- Esquema de seguridad `Bearer` (JWT) preparado para cuando exista autenticación

---

## Roadmap — plan maestro

> **Cómo leerlo**: `[x]` = hecho y verificado, `[ ]` = pendiente. Los comentarios explican el estado real y las decisiones tomadas, para que este documento sea útil al retomar el proyecto dentro de un mes.
>
> **Estado global hoy**: módulo Users completo (CRUD, paginación, filtros, errores centralizados, contraseñas con BCrypt, 71 pruebas) + Docker + documentación. Lo que falta de Users es **autenticación (JWT)**; después toca **Products** y **Warehouses**.

### Fase 1 — Fundamentos y estructura

#### Sprint 0. Creación del proyecto y entorno
*Objetivo: una API que arranque y un entorno reproducible.*

- [x] Crear proyecto ASP.NET Core Web API — estructura en `src/Api` y `src/Test`.
- [x] Fijar y mantener la versión de .NET — `global.json` con SDK `8.0.424`; el Dockerfile usa el mismo tag para no desincronizar.
- [x] Configurar PostgreSQL — cadena de conexión en User Secrets (dev) y variables de entorno (Docker/prod).
- [x] Instalar paquetes EF Core para PostgreSQL — `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.0.
- [x] Organizar configuraciones de desarrollo y producción — `appsettings.json` + `appsettings.Development.json`, sin secretos dentro.
- [x] Configurar Git y `.gitignore` — ignora `bin/`, `obj/`, `.vs/` y `.env`.
- [x] Verificar que la API arranque y responda vía Swagger — `GET /` y `/swagger` responden en local y en contenedor.
- [x] Evitar subir secretos al repositorio — User Secrets para dev; `.env` (gitignored) + `.env.example` para Docker; verificado con `git check-ignore`.

#### Sprint 1. Arquitectura y responsabilidades
*Objetivo: que cada capa tenga un trabajo claro.*

- [x] Estructura de carpetas por funcionalidad — `users/`, `config/`, `middleware/`, `security/`, `data/`, `Common/`.
- [x] Crear Controllers — `UserController` solo traduce HTTP; sin `try/catch` (el middleware centraliza todo).
- [x] Crear Services — `UserService` con reglas de negocio y mapeo a ViewModels.
- [x] Crear Repositories e interfaces — `UserRepository` + `IUserRepository`, `IUserService`.
- [x] Configurar inyección de dependencias — métodos de extensión por capa (`AddApplicationServices`, `AddDatabaseConfiguration`, etc.).
- [x] Definir reglas de dependencia — Controller → Service → Repository → `AppDbContext`; el Controller nunca ve el DbContext.
- [x] Separar modelos de persistencia, DTOs y ViewModels — `User` (entidad), `UserCreateDTO`/`UserUpdateDTO` (entrada), `UserViewModel`/`UserListViewModel` (salida).
- [x] Evitar lógica de negocio en Controllers — el Controller delega siempre en `IUserService`.
- [x] Revisar responsabilidad única de cada clase — verificado en la revisión de cierre del sprint.

#### Sprint 2. Modelos, DbContext y migraciones
*Objetivo: el esquema de la BD refleje los modelos y cambie de forma controlada.*

- [x] Crear `AppDbContext` — `src/Api/data/AppDbContext.cs`.
- [x] Configurar conexión con PostgreSQL — `UseNpgsql` en `DatabaseConfiguration`.
- [x] Registrar entidades — `DbSet<User>` declarado como `public new` para ocultar el `Users` heredado de `IdentityDbContext`.
- [x] Configurar Identity — `IdentityConfiguration` registra `IdentityUser`/`IdentityRole` (base para futura autenticación).
- [x] Crear y ejecutar migraciones — tras un squash, una única migración `20261017175658_InitialMigration`.
- [x] Revisar tipos, claves y restricciones — `Id` (PK), `UUID` (único), `Email` (único filtrado), longitudes de `Name`/`Email`/`Password`.
- [x] Definir convenciones de nombres — tabla `users` en minúsculas con `[Table("users")]`; columnas en PascalCase (nativo de PostgreSQL/Npgsql).
- [x] Verificar que las migraciones reflejan los modelos — `dotnet ef migrations has-pending-model-changes` sin cambios.
- [x] Resolver el conflicto de `AppDbContext.Users` — propiedad `Users` propia con `new` (oculta la de Identity sin romperla).

### Fase 2 — Usuarios y operaciones CRUD

#### Sprint 3. Diseño del módulo Users
*Objetivo: contratos de entrada/salida que representen bien el recurso sin exponer internos.*

- [x] Entidad `User` — con `SoftDelete`, `CreatedAt`, `UpdatedAt`, `IsActive`.
- [x] DTOs de operación — `UserCreateDTO` (Name, Email, Password obligatorios) y `UserUpdateDTO` (Password opcional).
- [x] ViewModels — `UserViewModel` (detalle) y `UserListViewModel` (listado, sin Password).
- [x] Interfaz del Repository — `IUserRepository` con métodos `IQueryable` y asíncronos con `CancellationToken`.
- [x] Interfaz del Service — `IUserService`.
- [x] Conexión entre capas — registrado en `AddApplicationServices`.
- [x] Campos obligatorios vs opcionales — documentado en la sección de Validación.
- [x] Diferenciar usuarios de negocio y de Identity — decisión: `User` es la entidad de negocio; NO se fusiona con `IdentityUser` (Identity queda listo para cuando exista login, pero los usuarios del CRUD no dependen de ella).
- [x] Definir qué no debe devolverse nunca — `Password` ausente de ambos ViewModels (verificado por pruebas).
- [x] Asignar `Name` desde `Name` del DTO, no desde el correo — mapeo corregido en `UserService.CreateUserAsync`.

#### Sprint 4. Crear, consultar, actualizar y eliminar
*Objetivo: CRUD completo y consistente.*

- [x] Creación de usuarios — `POST` con hash de contraseña y `201`.
- [x] Consulta por ID — `GET ?id=` devuelve `404` con ProblemDetails si no existe.
- [x] Actualización — `PUT`/`PATCH`; si `Password` viene vacío se conserva el hash actual.
- [x] Eliminación lógica (`SoftDelete`) — marca el registro; no se borra físicamente.
- [x] Casos de ID inexistente — `404` en GET/PUT/PATCH/DELETE.
- [x] Correo duplicado — el Service lanza `DomainException` → `409 Conflict` (más semántico que un `400` genérico).
- [x] Usuario borrado lógicamente no aparece en consultas normales — filtro `SoftDelete == null` en el Repository.
- [x] Campos que el cliente no controla — `Id`, `UUID`, `CreatedAt`, `UpdatedAt` se generan/actualizan en servidor; el DTO no los acepta.

#### Sprint 5. Contratos HTTP y comportamiento del CRUD
*Objetivo: que la API sea predecible para quien la consume.*

- [x] Endpoints principales definidos — `GET`, `GET?id=`, `POST`, `PUT/{id}`, `PATCH/{id}`, `DELETE/{id}` bajo `/api/User`.
- [x] Códigos `200/201/204/400/404/409` revisados — mapeados y probados (incluidos en pruebas de integración).
- [x] `201 Created` al crear — con `CreatedAtAction` apuntando a `Get` y devolviendo la ubicación del recurso.
- [x] Diferencia `PUT` vs `PATCH` — hoy comparten el mismo DTO y la misma lógica (actualización completa tolerante a campos vacíos); se documentó así a propósito para no inventar una semántica de parcheo parcial sin necesidad real.
- [x] Nombres y formatos de rutas — singular `api/[controller]` → `/api/User`; documentado en README y `Api.http`.
- [x] Probar cada operación — vía Swagger, `Api.http` y pruebas de integración.

### Fase 3 — Consultas, paginación y filtros

#### Sprint 6. Paginación reutilizable
*Objetivo: no devolver tablas enteras de golpe.*

- [x] `PagedQuery` — en `Common/`, con `Page` y `PageSize`.
- [x] `PagedResult<T>` — genérico, con metadatos de paginación.
- [x] `UserQuery` hereda de `PagedQuery` — reutilizable para futuros módulos (Products heredará lo mismo).
- [x] `Skip()`/`Take()` — aplicados en el Repository, nunca en memoria.
- [x] `TotalItems`/`TotalPages` calculados en el flujo de consulta — `CountAsync` con filtros aplicados y antes del paginado.
- [x] Página sin resultados — se verificó; decisión: colección vacía devuelve `TotalPages = 0` (no 1) para no sugerir que existe una página con ítems.
- [x] Límites de `Page`/`PageSize` — `SanitizeQuery` en el Service (Page ≥ 1, PageSize 1–100) + atributos `Range` en el query.
- [x] Orden estable antes de paginar — orden por `Id` antes de `Skip`/`Take` para que las páginas no se solapen ni se salten registros.

#### Sprint 7. Búsqueda y filtros combinables
*Objetivo: que el GET sirva para distintos escenarios reales.*

- [x] `Search` por nombre y correo — case-insensitive con `EF.Functions.ILike` (PostgreSQL) / `Contains` sensible al motor en SQLite de pruebas.
- [x] `IsActive` como filtro opcional — solo aplica si el parámetro viene con valor (`bool?`).
- [x] Filtro de `SoftDelete` en consultas normales — siempre activo en el Repository.
- [x] Combinar búsqueda, estado y paginación — los filtros se encadenan en `IQueryable` antes del conteo.
- [x] `CountAsync()` después de filtros y antes de paginar — garantiza metadatos correctos.
- [x] Sin materializar antes de filtrar — todo se queda como `IQueryable` hasta la ejecución final.
- [x] Probar vacíos, sin coincidencias y combinaciones — cubierto en `UserRepositoryTests` (SQLite) e integración.

#### Sprint 8. Ordenamiento, campos y forma de la respuesta
*Objetivo: flexibilidad sin endpoints ni abstracciones innecesarias.*

- [x] `OrderDesc` implementado — orden por `Id` ascendente/descendente, verificado.
- [ ] Evaluar `SortBy` con lista blanca de columnas — pendiente; solo si aparece una necesidad real de ordenar por otra columna (p. ej. `Email` o `CreatedAt`).
- [ ] `fields` (selección de columnas) — decisión tomada: **no implementar por ahora**; agregarlo solo si un consumidor lo pide explícitamente y con lista blanca estricta.
- [x] Respuesta de colecciones estandarizada — se usa `PagedResult<T>` directamente (sin wrapper extra `data`/`pagination`); se decidió no añadir otra capa de envoltorio porque no aporta nada hoy.

### Fase 4 — Calidad, seguridad y confiabilidad

#### Sprint 9. Validación de entradas
*Objetivo: rechazar datos malos antes de la lógica de negocio.*

- [x] Validación de rango en `PagedQuery` — `Range(1, int.MaxValue)` en Page, `Range(1, 100)` en PageSize + saneo en Service.
- [x] Campos obligatorios de DTOs — `[Required]` en Name, Email, Password (creación).
- [x] Formato y longitud de correo — `[EmailAddress]`, longitud máxima 100.
- [x] Longitudes mínimas/máximas — Name 2–100, Password mínimo 8.
- [x] Reglas de unicidad — correo único vía índice filtrado en BD + comprobación en Service → `409`.
- [x] Comportamiento de `[ApiController]` — respuestas automáticas `400` con `ValidationProblemDetails`; configurado en `ValidationConfiguration` para servir `application/problem+json` (consistente con el middleware).
- [x] Diferenciar validación de negocio — validación → `400`; negocio (correo duplicado, usuario inexistente) → `409`/`404`.

#### Sprint 10. Manejo centralizado de errores
*Objetivo: una sola estrategia de errores, respuestas seguras.*

- [x] `ErrorHandlingMiddleware` — registrado primero en el pipeline; atrapa todas las excepciones.
- [x] Sin manejo genérico duplicado en Controllers — se eliminó todo `try/catch` de los Controllers a propósito; el middleware es el único punto.
- [x] Estrategia única — mapeo: `KeyNotFound`→404, `ArgumentException`→400, `UnauthorizedAccessException`→403, `DomainException`→status propio (409), resto→500.
- [x] `ProblemDetails` RFC 7807 — con campo extra `code` y `traceId` para correlación.
- [x] Errores conocidos con su significado — tabla en la sección de Manejo de errores.
- [x] Logs de errores internos — el middleware registra el `500` con excepción completa en logs; solo al cliente se le da el `traceId`.
- [x] Sin stack traces al cliente — el `500` devuelve un mensaje genérico; verificado por prueba (`ErrorHandlingMiddlewareTests`).

#### Sprint 11. Seguridad y autenticación
*Objetivo: credenciales protegidas y acceso controlado.*

- [x] Integración User/Identity revisada — decisión: no fusionar; `User` es la entidad de negocio e Identity queda disponible para el login futuro.
- [x] Nunca contraseñas en texto plano — **resuelto**: `BCryptPasswordHasher` (`IPasswordHasher<User>`, work factor 11, salt integrado). Nota del roadmap original sobre "contraseñas en texto plano" → ya no aplica.
- [x] Identity para gestionar credenciales/hashes — se usa su abstracción `IPasswordHasher<User>`; `AddIdentity` registrado como base.
- [x] Secretos fuera del repositorio — User Secrets + `.env` (gitignored).
- [x] Revisar campos expuestos — `Password` nunca sale en ViewModels.
- [ ] Configurar autenticación real (JWT: login/refresh) — pendiente; Swagger ya expone el esquema `Bearer` listo para cuando exista.
- [ ] Proteger endpoints con roles/políticas — pendiente; hoy todos los endpoints son públicos (aceptable mientras no haya datos sensibles de producción).
- [ ] Revisar CORS para producción — hoy `AllowAll` (solo válido en dev); debe restringirse a orígenes conocidos antes de desplegar.
- [ ] Rate limiting — pendiente; evaluar `AspNetCoreRateLimit` o el middleware nativo de .NET 8 cuando haya usuarios reales.

#### Sprint 12. Pruebas automatizadas
*Objetivo: detectar rotos sin depender de pruebas manuales.*

- [x] Pruebas unitarias del Service — creación, actualización, búsqueda, hash, correo duplicado, ausencia de `Password` en ViewModels.
- [x] IDs inexistentes y errores de negocio — cubiertos en Service y middleware.
- [x] Paginación y filtros combinados — `UserRepositoryTests` contra SQLite en memoria.
- [x] Eliminación lógica — verificada en repo y en integración.
- [x] Integración de endpoints — `WebApplicationFactory` con SQLite; códigos HTTP reales 200/201/204/400/404.
- [x] Estructura de respuestas y validaciones — incluido ProblemDetails de errores centralizados.
- [x] Base de datos de prueba adecuada — SQLite en memoria (suficiente para este alcance; no hace falta Postgres de pruebas todavía).

### Fase 5 — Inventario y evolución

> Users es el módulo de aprendizaje. Products y Warehouses repiten los patrones ya probados, sin copiar código ciegamente.

#### Sprint 13. Módulo Products
*Objetivo: administrar productos del inventario.*

- [ ] Definir entidad `Product` — propiedades mínimas: `SKU`, `Name`, `Description?`, `Price`, `IsActive`, timestamps y `SoftDelete` (mismas convenciones que `User`).
- [ ] Configurar restricciones y tipos — `SKU` único (índice filtrado por `SoftDelete`, igual que `Email`), precio con precisión decimal (no `double`).
- [ ] Crear DTOs y ViewModels — `ProductCreateDTO`, `ProductUpdateDTO`, `ProductViewModel`, `ProductListViewModel`; `ProductQuery : PagedQuery`.
- [ ] Implementar Repository, Service y Controller — reutilizando el patrón de Users.
- [ ] CRUD completo — mismos códigos HTTP y ProblemDetails que Users.
- [ ] Paginación, búsqueda, filtros y orden — `Search` por nombre/SKU, filtro por estado, reutilizando `PagedQuery`.
- [ ] Validar SKU único — Service + índice en BD, mismo tratamiento que el correo.
- [ ] Migración y pruebas — nueva migración; suite de pruebas equivalente a la de Users.

#### Sprint 14. Módulos Warehouses e inventario
*Objetivo: representar dónde están los productos y cuánto hay.*

- [ ] Definir entidad `Warehouse` — nombre, ubicación/descripción, `IsActive`, timestamps.
- [ ] Relaciones almacén–producto — entidad intermedia `InventoryItem` (decisión tomada de antemano: tabla puente explícita, no relación many-to-many oculta, para poder poner cantidad y auditoría).
- [ ] Representar cantidad disponible — `Quantity` entero ≥ 0 con validación; nunca negativo.
- [ ] Reglas contra cantidades inválidas — validación en Service antes de `SaveChangesAsync`.
- [ ] Consultas de existencias — por producto y por almacén, con paginación si crece.
- [ ] Operaciones de entrada/salida de inventario — endpoints dedicados (p. ej. `POST /api/InventoryItem/restock` y `/consume`), no editar `Quantity` a mano desde un PUT genérico.
- [ ] Concurrencia — evaluar `UPDATE ... WHERE` condicional o tokens de concurrencia para evitar dos retiros simultáneos que dejen saldo negativo.
- [ ] Pruebas de movimientos — cubrir saldo resultante, operaciones inválidas y, si se implementa, la condición de carrera.

#### Sprint 15. Documentación, rendimiento y preparación para crecer
*Objetivo: primera versión mantenible por otra persona.*

- [x] Documentar endpoints con OpenAPI — Swagger con comentarios XML generados desde el código.
- [x] Ejemplos de peticiones y respuestas — en `src/Api/Api.http` y en este README.
- [x] Documentar códigos de error — tabla de ProblemDetails en la sección de Manejo de errores.
- [ ] Documentar autenticación — pendiente hasta que exista JWT (Sprint 11).
- [x] Revisar índices de PostgreSQL — `Email` único filtrado, `UUID` único.
- [x] Revisar unicidad e integridad referencial — unicidad resuelta; integridad referencial cuando existan relaciones (Sprint 14).
- [x] `AsNoTracking()` en lecturas — aplicado en todos los `Get*` del Repository.
- [x] `CancellationToken` en operaciones asíncronas — propagado desde el Controller hasta `SaveChangesAsync`.
- [x] Revisar SQL generado por EF Core — verificado durante el desarrollo (conteo antes de paginar, sin materialización prematura).
- [ ] Medir rendimiento antes de optimizar — pendiente; no hay métricas reales todavía, y no debe optimizarse a ciegas.
- [ ] Configurar logs y variables de entorno para producción — pendiente; falta definir nivel de logs y secretos del entorno real de despliegue.
- [x] Instrucciones para ejecutar desde cero — README con Opción A (Docker) y Opción B (manual).

### Temas de arquitectura para después

> No meter todos de una vez. Cada tema se estudia cuando aparezca un problema real que lo justifique.

| Tema | Cuándo estudiarlo |
|---|---|
| SOLID y separación de responsabilidades | Durante los sprints 1–5 *(ya ejercitado sin nombrarlo)* |
| LINQ y SQL generado por EF Core | Durante los sprints 6–8 *(ya ejercitado; profundizar con `ToQueryString()`)* |
| CQRS | Después de consolidar los servicios actuales, si Services crecen demasiado |
| Clean Architecture / hexagonal | En un proyecto experimental posterior o si el tamaño lo justifica; **no migrar Warehouse API solo por conocerla** |
| Caché | Cuando identifiques consultas costosas y repetidas (p. ej. existencias por almacén) |
| Docker y despliegue | Hecho a nivel de desarrollo; falta pipeline de despliegue real |
| Multiempresa (multi-tenant) | Cuando definas ese requisito para el ERP |
| Observabilidad y rendimiento | Cuando existan flujos y métricas reales que analizar |

### Cómo se trabaja cada sprint

1. **Objetivo**: qué problema se resuelve.
2. **Conceptos**: lo mínimo que hay que entender antes de programar.
3. **Tareas pequeñas**: una responsabilidad por paso.
4. **Implementación**: se escribe el código y se explica cada decisión.
5. **Pruebas**: casos normales y de error.
6. **Cierre**: qué se aprendió, qué quedó pendiente y qué desbloquea el siguiente.

### Siguiente paso recomendado

1. **Completar Sprint 11** — implementar login/refresh con JWT y proteger los endpoints de Users.
2. **Sprint 13 (Products)** — reutilizar todo el patrón ya probado en un recurso nuevo.
3. **Sprint 14 (Warehouses/Inventory)** — primera relación real entre entidades y primer problema de concurrencia.
