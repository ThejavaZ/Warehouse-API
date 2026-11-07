# Warehouse API

[![CI](https://github.com/ThejavaZ/Warehouse-API/actions/workflows/ci.yml/badge.svg)](https://github.com/ThejavaZ/Warehouse-API/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Licencia: MIT](https://img.shields.io/badge/licencia-MIT-yellow.svg)](LICENSE)
[![Código de conducta](https://img.shields.io/badge/código%20de%20conducta-Contributor%20Covenant-ff69b4.svg)](CODE_OF_CONDUCT.md)

API REST para gestión de almacén construida con **ASP.NET Core 8**, **EF Core** y **PostgreSQL**.

Módulos disponibles:

| Módulo | Rutas | Descripción |
|---|---|---|
| **Auth** | `/api/Auth` | Registro, login JWT, renovación y revocación de tokens |
| **Users** | `/api/User` | CRUD con paginación, filtros dinámicos, borrado lógico y contraseñas con BCrypt |
| **Products** | `/api/Product` | CRUD con SKU único, precio decimal y los mismos patrones que Users |
| **Warehouses** | `/api/Warehouse` | CRUD de almacenes (nombre, ubicación, estado) |
| **Inventory** | `/api/InventoryItem` | Existencias por par almacén–producto y movimientos `restock`/`consume` |

Todos los endpoints salvo `/api/Auth/register`, `/login`, `/refresh` y `/revoke` requieren un **access token Bearer** (ver [Autenticación](#autenticación-jwt)).

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
cp .env.example .env       # ajusta POSTGRES_PASSWORD y AUTH_SECRET
docker compose up --build
```

- API y Swagger: `http://localhost:5259/swagger`
- PostgreSQL (para psql/otras herramientas): `localhost:5433`, credenciales del `.env`
- Las migraciones se aplican solas al arrancar (`Database:ApplyMigrationsOnStartup`).
- El volumen `pgdata` persiste los datos; `docker compose down -v` lo elimina.

> `.env` está en `.gitignore`: nunca se versiona. La cadena de conexión y el secreto JWT se pasan al contenedor desde ese archivo (`ConnectionStrings__DefaultConnection`, `Auth__Secret`). **La API no arranca si falta `AUTH_SECRET`**: el compose lo exige con `${AUTH_SECRET:?...}`.

### Opción B — Manual

#### 1. Restaurar y compilar

```bash
dotnet restore
dotnet build
```

#### 2. Configurar secretos (fuera del código)

La cadena de conexión y el secreto JWT **no están en el repositorio**. En desarrollo se guardan con [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=warehouse_db;Username=<usuario>;Password=<password>" \
  --project src/WMS.Api

# Secreto de firma JWT (mínimo 32 caracteres). Genera uno con: openssl rand -hex 32
dotnet user-secrets set "Auth:Secret" "<valor de 32+ caracteres>" --project src/WMS.Api
```

En producción usa variables de entorno en lugar de user secrets:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
export Auth__Secret="<valor de 32+ caracteres>"
```

> **Nunca** subas credenciales a Git. Verifica con `git diff` que `appsettings.json` no contenga ninguno de los dos valores antes de commitear.

Si falta `Auth:Secret`, la API **no arranca**: lanza un error claro indicando cómo configurarlo.

#### 3. Aplicar migraciones

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/WMS.Infrastructure --startup-project src/WMS.Api
```

#### 4. Ejecutar

```bash
dotnet run --project src/WMS.Api
```

- Swagger UI: `http://localhost:5259/swagger`
- Health/info: `GET /` → versión y entorno

---

## Autenticación (JWT)

Flujo de sesión (Sprint 11):

```
POST /api/Auth/register  ──►  usuario creado (201, sin tokens)
POST /api/Auth/login     ──►  accessToken (JWT, 15 min) + refreshToken (7 días)
        │
        ├──  Authorization: Bearer <accessToken>  ──►  endpoints protegidos
        │
POST /api/Auth/refresh   ──►  rota el refresh token y devuelve uno nuevo + accessToken
POST /api/Auth/revoke    ──►  logout (204); el refresh token queda invalidado
```

| Método | Ruta | Auth | Éxito | Descripción |
|---|---|---|---|---|
| `POST` | `/api/Auth/register` | pública | `201` `UserViewModel` | Crea un usuario (no emite tokens) |
| `POST` | `/api/Auth/login` | pública | `200` `AuthViewModel` | Devuelve `accessToken`, `refreshToken`, `expiresIn`, `tokenType`, `userId`, `email` |
| `POST` | `/api/Auth/refresh` | pública | `200` `AuthViewModel` | Canjea el refresh token por uno nuevo (rotación) |
| `POST` | `/api/Auth/revoke` | pública | `204` | Revoca el refresh token (logout) |
| `GET` | `/api/Auth/me` | Bearer | `200` `UserViewModel` | Perfil del usuario del token |

Decisiones tomadas:

- **Access token**: JWT HMAC-SHA256 con `Auth:Secret`, vida corta (`Auth:AccessTokenMinutes`, 15 por defecto). No guarda estado: no consulta la BD al validar.
- **Refresh token**: 64 bytes aleatorios en base64url. En BD solo se guarda su **hash SHA-256**, por lo que una filtración de la tabla no permite usarlos.
- **Rotación y detección de robo**: cada `refresh` revoca el token usado y emite otro. Reutilizar un token ya rotado devuelve `401` (señal de que alguien más lo tiene).
- **Revocación adicional**: cambiar la contraseña (`PUT /api/User/{id}`) y darse de baja revocan todos los refresh tokens del usuario.
- **Registro abierto**: se puede desactivar con `Auth:RegistrationEnabled=false` → `403` `registration_disabled`.
- **Sin roles ni políticas**: hoy solo se distingue autenticado/no autenticado (`[Authorize]` plano). Los roles se añadirán cuando exista un requisito real.
- **Retos y prohibiciones** responden ProblemDetails (`401` `unauthorized`, `403` `forbidden`) desde `AuthenticationConfiguration`, en vez del `401` vacío por defecto de JWT.

> `Auth:Secret` es obligatorio (mínimo 32 caracteres) y **nunca se versiona**: user-secrets en desarrollo, `Auth__Secret` en despliegue. Ver [Puesta en marcha](#puesta-en-marcha).

---

## Endpoints

Base paths: `/api/User`, `/api/Product`, `/api/Warehouse`, `/api/InventoryItem`.

### Users — `/api/User`

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

# Actualizar (la contraseña es opcional: hay que OMITIR el campo para
# conservar la actual; enviar "" produce un 400 por MinLength(8))
PUT /api/User/5
{ "name": "Ana G.", "email": "ana@test.com" }

# Cambiar la contraseña (mínimo 8 caracteres; revoca los refresh tokens del usuario)
PUT /api/User/5
{ "name": "Ana G.", "email": "ana@test.com", "password": "NuevaClave1!" }

# Borrado lógico
DELETE /api/User/5
```

### Products — `/api/Product`

Mismo patrón que Users: `GET`, `GET?id=`, `POST`, `PUT/{id}`, `PATCH/{id}`, `DELETE/{id}`.

| Parámetro | Tipo | Default | Descripción |
|---|---|---|---|
| `id` | `int?` | `null` | Detalle en vez de listado |
| `search` | `string?` | `null` | Búsqueda case-insensitive por **nombre** o **SKU** |
| `isActive` | `bool?` | `null` | Filtra por estado solo si se envía |
| `page` / `pageSize` / `orderDesc` | — | — | Igual que en Users (`PagedQuery`) |

| Campo | Tipo | Notas |
|---|---|---|
| `SKU` | `string` | Obligatorio, 2–50; **único** (índice filtrado por `SoftDelete`, igual que el correo) |
| `Name` | `string` | Obligatorio, 2–100 |
| `Description` | `string?` | Opcional, máx. 500 |
| `Price` | `decimal` | Obligatorio, `0`–`999999999`; se almacena como `decimal(18,2)` (nunca `double`) |

```bash
# Crear
POST /api/Product
{ "sku": "SKU-0001", "name": "Teclado mecánico", "description": "TKL", "price": 49.90 }

# SKU duplicado → 400 ProblemDetails (mismo tratamiento que el correo duplicado)
GET /api/Product?search=tecl&isActive=true&orderDesc=false
```

### Warehouses — `/api/Warehouse`

Mismo patrón que Users.

| Parámetro | Tipo | Descripción |
|---|---|---|
| `id` | `int?` | Detalle en vez de listado |
| `search` | `string?` | Búsqueda case-insensitive por **nombre** o **ubicación** |
| `isActive` | `bool?` | Filtra por estado solo si se envía |

| Campo | Tipo | Notas |
|---|---|---|
| `Name` | `string` | Obligatorio, 2–100; **único** filtrado por `SoftDelete` |
| `Location` | `string?` | Opcional, máx. 200 |

```bash
POST /api/Warehouse
{ "name": "Almacén Norte", "location": "Nave 2, bahía B" }
```

### Inventory — `/api/InventoryItem`

| Método | Ruta | Éxito | Descripción |
|---|---|---|---|
| `GET` | `/api/InventoryItem` | `200` `PagedResult<InventoryItemViewModel>` | Existencias; filtros `warehouseId`, `productId`, `page`, `pageSize`, `orderDesc` |
| `POST` | `/api/InventoryItem/restock` | `200` `InventoryItemViewModel` | Entrada: **suma** `quantity`; crea la ficha si no existe |
| `POST` | `/api/InventoryItem/consume` | `200` `InventoryItemViewModel` | Salida: **resta** `quantity` |

```bash
POST /api/InventoryItem/restock
{ "warehouseId": 1, "productId": 1, "quantity": 25 }

POST /api/InventoryItem/consume
{ "warehouseId": 1, "productId": 1, "quantity": 5 }
```

Los movimientos **nunca** se hacen editando `Quantity` desde un `PUT` genérico: hay dos endpoints dedicados para que la entrada y la salida sean explícitas y auditables.

Concurrencia y reglas de negocio:

- **Saldo nunca negativo**: el consumo se ejecuta como `UPDATE ... WHERE Quantity >= @qty`; si no alcanza, no se modifica nada y responde `409` `insufficient_stock`.
- **Upsert con reintento**: si dos peticiones crean la misma ficha a la vez, violan el índice único `(WarehouseId, ProductId)` y el repositorio reintenta la actualización (`409` `conflict` si el reintento también pierde).
- **Cantidades**: `quantity ≥ 1` en los movimientos (validación de DTO) y `CHECK (Quantity >= 0)` en BD.
- **Almacén/producto inactivos o dados de baja** → `400`; inexistentes → `404`.
- FKs con `DeleteBehavior.Restrict`: un producto o almacén con existencias no se puede borrar en cascada.

---

## Manejo de errores

Todas las respuestas de error usan **ProblemDetails (RFC 7807)** con `Content-Type: application/problem+json` y el campo adicional `code`.

| Código HTTP | Origen | `code` |
|---|---|---|
| `400` | Validación de DTO / parámetros de paginación / cantidad inválida | `validation_error` |
| `401` | Sin token, token inválido o caducado, credenciales incorrectas, refresh token usado o revocado | `unauthorized`, `invalid_credentials` |
| `403` | `UnauthorizedAccessException` o registro abierto desactivado | `forbidden`, `registration_disabled` |
| `404` | Usuario, producto, almacén o ficha de existencias inexistente o dado de baja | `not_found` |
| `409` | Conflicto de negocio: SKU/nombre duplicado, saldo insuficiente, carrera de concurrencia | `conflict`, `insufficient_stock` |
| `429` | Límite de peticiones por IP superado (ver [Rate limiting](#rate-limiting)) | `rate_limit_exceeded` |
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

El manejo está centralizado en [`src/WMS.Api/middleware/ErrorHandlingMiddleware.cs`](src/WMS.Api/middleware/ErrorHandlingMiddleware.cs); los controladores no usan `try/catch`.

---

## Seguridad

- **Contraseñas**: se almacenan con **BCrypt** (salt integrado, work factor 11), nunca en texto plano.
  `BCryptPasswordHasher` implementa la abstracción `IPasswordHasher<User>` de ASP.NET Core Identity.
- **Nunca se expone `Password`**: ni en `UserViewModel` ni en `UserListViewModel` (verificado por pruebas).
- **Correos únicos**: índice único en `Email` **filtrado** por `SoftDelete IS NULL`, de modo que un correo dado de baja puede reutilizarse pero no duplicarse mientras esté activo.
- **Autenticación/Autorización**: login y renovación con **JWT** (módulo `auth/`), refresh tokens rotatorios guardados solo como hash y `[Authorize]` en todos los endpoints salvo los públicos de `/api/Auth`. Swagger trae el esquema `Bearer` listo para probarlos desde la UI.

### CORS

Configurado en `CorsConfiguration` según el entorno:

- **Development**: cualquier origen (`AllowAnyOrigin`), métodos y cabeceras.
- **Resto de entornos**: solo los orígenes de `Cors:AllowedOrigins`. Si la lista está **vacía**, no se admite ningún origen externo (más seguro que `AllowAll`) y el arranque escribe un aviso en el log.

```bash
export Cors__AllowedOrigins="https://app.example.com,https://admin.example.com"
```

### Rate limiting

Middleware nativo de .NET 8 (`RateLimitConfiguration`), ventana fija por dirección IP, con **contadores separados** para `/api/Auth` y para el resto (los intentos de login no consumen la cuota general):

| Clave | Default | Uso |
|---|---|---|
| `RateLimit:PermitLimit` | `100` | Peticiones por ventana en el resto de rutas |
| `RateLimit:AuthPermitLimit` | `10` | Peticiones por ventana en `/api/Auth` |
| `RateLimit:WindowSeconds` | `60` | Duración de la ventana |

Al superarlo responde `429` con ProblemDetails (`code = rate_limit_exceeded`) y cabecera `Retry-After`.

### Validación

DTOs validados con Data Annotations y validados automáticamente por `[ApiController]`:

- `Name`: obligatorio, 2–100 caracteres
- `Email`: obligatorio, formato de correo
- `Password`: obligatorio en creación (mín. 8); opcional en actualización (mín. 8 si se envía)
- `page` / `pageSize`: `Range` sobre los límites permitidos

---

## Estructura del proyecto

Arquitectura hexagonal (ver [MIGRATION.md](MIGRATION.md) y [ADR-0001](docs/adr/0001-arquitectura-hexagonal.md)
para el estado de la migración):

```
Warehouse-API/
├── WarehouseApi.sln
├── Directory.Build.props                # Nullable/ImplicitUsings compartidos
├── .editorconfig                        # Estilo del código
├── Dockerfile / docker-compose.yml      # Imagen y PostgreSQL
├── MIGRATION.md                         # Estado de la migración hexagonal
├── docs/adr/                            # Architecture Decision Records
├── src/
│   ├── WMS.Api/                         # Composition root (host)
│   │   ├── Program.cs                   # Pipeline; aplica migraciones al arrancar
│   │   ├── Controllers/                 # Auth, User, Product, Warehouse, Inventory
│   │   ├── Config/                      # BD, Auth, CORS, RateLimit, Swagger, Identity…
│   │   └── middleware/ErrorHandlingMiddleware.cs
│   ├── WMS.Domain/                      # Entidades (sin dependencias)
│   │   ├── Entities/                    # User, RefreshToken, Product, Warehouse, InventoryItem y scaffolds (Inventory, Category…)
│   │   └── Common/                      # Model.cs (base común) y DomainException.cs
│   ├── WMS.Application/                 # Casos de uso y puertos
│   │   ├── Users/                       # UserService, IUserService, IUserRepository y Models/ (DTOs, VM, Query)
│   │   ├── Products/                    # ProductService, IProductService, IProductRepository y Models/
│   │   ├── Warehouses/                  # WarehouseService, IWarehouseService, IWarehouseRepository y Models/
│   │   ├── Inventory/                   # InventoryService, IInventoryService, IInventoryRepository y Models/
│   │   ├── Auth/                        # AuthService, IAuthService, IJwtTokenService, AuthOptions, IRefreshTokenRepository y Models/
│   │   └── Common/                      # PagedQuery, PagedResult
│   ├── WMS.Infrastructure/              # Adaptadores
│   │   ├── Auth/                        # JwtTokenService (firma JWT) y BCryptPasswordHasher (IPasswordHasher<User>)
│   │   └── Persistence/
│   │       ├── AppDbContext.cs          # + AppDbContextDesignTimeFactory
│   │       ├── Configurations/          # IEntityTypeConfiguration<T>
│   │       ├── Migrations/
│   │       └── Repositories/            # User, Product, Warehouse, Inventory, RefreshToken
│   ├── Api/                             # Solo csproj + appsettings (desaparece en T15)
│   ├── Test/                            # NUnit: unit tests de servicios, repos y middleware
│   │   └── Fakes/                       # Dobles de repositorio para los servicios
│   ├── WMS.Api.Test/                    # Feature tests HTTP (xUnit): IntegrationTestBase, WarehouseApiFactory y 5 suites
│   └── WMS.*.Test/                      # Tests por módulo (NUnit)
```

### Flujo de una petición

```
Controller          Service              Repository            EF Core        PostgreSQL
(WMS.Api)      →   (WMS.Application)  →  (WMS.Infrastructure)  →  SQL      →    datos
   HTTP             reglas de             IQueryable
                    negocio               + mapeo
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

**196 pruebas** en verde, sin advertencias de compilación (137 NUnit en `src/Test`,
52 feature tests xUnit en `WMS.Api.Test`, 5 NUnit en `WMS.Infrastructure.Test` y
2 marcadores xUnit que se sustituirán con los tests de cada módulo según avance
la migración). Cubren:

- **Unitarias — servicios**: `UserService`, `ProductService`, `WarehouseService`, `InventoryService`, `AuthService` (login, registro, rotación y revocación de refresh tokens) y `JwtTokenService` (firma, vida del token, hash de refresh tokens).
- **Unitarias — repositorios contra SQLite en memoria**: paginación y filtros de Users/Products, índice único filtrado por `SoftDelete`, y los `UPDATE` condicionales de inventario (`WHERE Quantity >= @qty`) junto con el reintento en carrera por el índice `(WarehouseId, ProductId)`.
- **Unitarias — infraestructura**: `ErrorHandlingMiddleware` (400/403/404/409/500 y ausencia de detalles internos), validaciones de Data Annotations y `BCryptPasswordHasher` (el adaptador real de `WMS.Infrastructure/Auth`, API de Identity: formato `$2`, sin texto plano, sal distinto por hash y verificación correcta/incorrecta).
- **Integración HTTP (xUnit, en `WMS.Api.Test`)** con `WebApplicationFactory` + SQLite en memoria: `AuthApiTests`, `UserApiTests`, `ProductApiTests`, `WarehouseApiTests` e `InventoryApiTests`. Cubren el flujo completo de sesión (register → login → llamada autenticada → refresh con rotación → revoke), `401` sin token y con token manipulado, `400/404/409` de cada módulo y los movimientos de inventario.

La base común (`IntegrationTestBase`) arranca la API, limpia la BD entre pruebas y monta el Bearer; `WarehouseApiFactory` inyecta un `Auth:Secret` en memoria y sube los límites de rate limiting para que la suite no se corte sola.

---

## Swagger / OpenAPI

Documentado en `SwaggerConfiguration` y con comentarios XML generados desde el código:

- Parámetros de consulta de cada módulo (`UserQuery`, `ProductQuery`, `WarehouseQuery`, `InventoryQuery`)
- Códigos de estado por endpoint (`200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `429`, `500`)
- Esquemas `ProblemDetails` / `ValidationProblemDetails`
- Esquema de seguridad `Bearer` (JWT) **activo**: desde Swagger UI se puede pulsar *Authorize*, pegar el access token y probar cualquier endpoint protegido

---

## Roadmap — plan maestro

> **Cómo leerlo**: `[x]` = hecho y verificado, `[ ]` = pendiente. Los comentarios explican el estado real y las decisiones tomadas, para que este documento sea útil al retomar el proyecto dentro de un mes.
>
> **Estado global hoy**: los cinco módulos están implementados y documentados — **Auth** (JWT con refresh token rotatorio), **Users**, **Products**, **Warehouses** e **Inventory** (restock/consume con concurrencia) — más CORS por entorno, rate limiting y **196 pruebas en verde** con compilación sin advertencias. La arquitectura hexagonal está **en migración**: composition root y persistencia ya están en `WMS.*`, el detalle por tarea vive en [MIGRATION.md](MIGRATION.md). Siguen abiertos solo los ítems que dependen de métricas o requisitos reales (Sprint 15 y el `SortBy`/`fields` del Sprint 8).

### Fase 1 — Fundamentos y estructura

#### Sprint 0. Creación del proyecto y entorno
*Objetivo: una API que arranque y un entorno reproducible.*

- [x] Crear proyecto ASP.NET Core Web API — estructura en `src/Api` y `src/Test` (hoy: proyectos `WMS.*` y `src/Test` pendiente de reubicar en T15).
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

- [x] Crear `AppDbContext` — `src/Api/data/AppDbContext.cs` (hoy en `src/WMS.Infrastructure/Persistence/AppDbContext.cs`).
- [x] Configurar conexión con PostgreSQL — `UseNpgsql` en `DatabaseConfiguration`.
- [x] Registrar entidades — `DbSet<User>` declarado como `public new` para ocultar el `Users` heredado de `IdentityDbContext`.
- [x] Configurar Identity — `IdentityConfiguration` registra `IdentityUser`/`IdentityRole` (base para futura autenticación).
- [x] Crear y ejecutar migraciones — tras un squash, una única migración `20261017175658_InitialMigration`.
- [x] Revisar tipos, claves y restricciones — `Id` (PK), `UUID` (único), `Email` (único filtrado), longitudes de `Name`/`Email`/`Password`.
- [x] Definir convenciones de nombres — tabla `users` en minúsculas con `[Table("users")]`; columnas en PascalCase (nativo de PostgreSQL/Npgsql).
- [x] Verificar que las migraciones reflejan los modelos — `dotnet ef migrations has-pending-model-changes --project src/WMS.Infrastructure --startup-project src/WMS.Api` sin cambios.
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

- [x] Integración User/Identity revisada — decisión: no fusionar; `User` es la entidad de negocio e Identity queda disponible como base (aporta `IPasswordHasher<User>` y el registro de esquemas de autenticación).
- [x] Nunca contraseñas en texto plano — **resuelto**: `BCryptPasswordHasher` (`IPasswordHasher<User>`, work factor 11, salt integrado). Nota del roadmap original sobre "contraseñas en texto plano" → ya no aplica.
- [x] Identity para gestionar credenciales/hashes — se usa su abstracción `IPasswordHasher<User>`; `AddIdentity` registrado como base.
- [x] Secretos fuera del repositorio — User Secrets + `.env` (gitignored); ahora también `Auth:Secret` / `AUTH_SECRET`.
- [x] Revisar campos expuestos — `Password` nunca sale en ViewModels.
- [x] Configurar autenticación real (JWT: login/refresh) — **hecho**: módulo `auth/` con `POST /api/Auth/register|login|refresh|revoke` y `GET /me`. Access token HMAC-SHA256 de 15 min + refresh token opaco de 7 días guardado como hash SHA-256, con **rotación** (reutilizar un token usado → `401`) y revocación al cambiar la contraseña. El `Auth:Secret` es obligatorio y el arranque falla sin él.
- [x] Proteger endpoints con roles/políticas — **hecho (alcance acordado)**: `[Authorize]` en `User`, `Product`, `Warehouse` e `InventoryItem`; `/api/Auth/{register,login,refresh,revoke}` públicas. Sin roles: solo autenticado/no autenticado, hasta que exista un requisito real. Nota: `AddIdentity` fija el esquema por defecto a la cookie `Identity.Application`; `AuthenticationConfiguration` lo anula a `Bearer` (si no, el reto devolvía `401` pero la autenticación se resolvía contra la cookie y todo caía en `401`).
- [x] Revisar CORS para producción — **hecho**: `AllowAll` solo en Development; en el resto solo `Cors:AllowedOrigins` (vacío = ningún origen externo + aviso en el log al arrancar). Configurable con `Cors__AllowedOrigins`.
- [x] Rate limiting — **hecho** con el middleware nativo de .NET 8: ventana fija por IP, contador aparte para `/api/Auth` (10/min por defecto) frente al general (100/min), `429` con ProblemDetails `rate_limit_exceeded` y cabecera `Retry-After`. Ajustable con la sección `RateLimit`.

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

- [x] Definir entidad `Product` — `SKU`, `Name`, `Description?`, `Price`, `IsActive`, timestamps y `SoftDelete`, con las mismas convenciones que `User`.
- [x] Configurar restricciones y tipos — `SKU` único (índice filtrado por `SoftDelete`, igual que `Email`) y `Price` con `decimal(18,2)` (`HasPrecision`), nunca `double`.
- [x] Crear DTOs y ViewModels — `ProductCreateDTO`, `ProductUpdateDTO`, `ProductViewModel`, `ProductListViewModel` y `ProductQuery : PagedQuery`.
- [x] Implementar Repository, Service y Controller — mismo patrón de capas que Users, sin `try/catch` en el controlador.
- [x] CRUD completo — mismos códigos HTTP y ProblemDetails que Users (`200/201/204/400/404`).
- [x] Paginación, búsqueda, filtros y orden — `Search` por nombre/SKU, filtro `IsActive`, `PagedQuery` reutilizado.
- [x] Validar SKU único — Service (→ `400` `validation_error`) + índice filtrado en BD, mismo tratamiento que el correo.
- [x] Migración y pruebas — migración `20261101051548_AddProductsWarehousesAndInventory`; `ProductServiceTests`, `ProductRepositoryTests` (SQLite) y `ProductApiTests`.

#### Sprint 14. Módulos Warehouses e inventario
*Objetivo: representar dónde están los productos y cuánto hay.*

- [x] Definir entidad `Warehouse` — `Name` (único filtrado), `Location?`, `IsActive`, timestamps y `SoftDelete`.
- [x] Relaciones almacén–producto — entidad intermedia `InventoryItem` (tabla puente explícita, no many-to-many oculta, para poder guardar cantidad y auditoría); FKs con `DeleteBehavior.Restrict`.
- [x] Representar cantidad disponible — `Quantity` entero con `CHECK (Quantity >= 0)` en BD; nunca negativo.
- [x] Reglas contra cantidades inválidas — validación en el DTO (`Range(1, …)` en los movimientos) y en el Service antes de `SaveChangesAsync`; almacén/producto inactivo → `400`, inexistente → `404`.
- [x] Consultas de existencias — `GET /api/InventoryItem` con filtros `warehouseId` y `productId`, paginación y nombres/SKU resueltos en la respuesta.
- [x] Operaciones de entrada/salida de inventario — `POST /api/InventoryItem/restock` y `/consume`; `Quantity` no se edita desde un `PUT` genérico.
- [x] Concurrencia — `ExecuteUpdateAsync` condicional (`UPDATE … WHERE Quantity >= @qty`) para que dos retiros simultáneos no dejen saldo negativo, e upsert con reintento si dos peticiones crean la misma ficha a la vez (índice único `(WarehouseId, ProductId)` → `409`).
- [x] Pruebas de movimientos — saldo resultante, saldo insuficiente (`409` `insufficient_stock`), ficha inexistente (`404`), cantidad inválida (`400`) y carrera de actualización en `InventoryRepositoryTests` (SQLite) + `InventoryApiTests`.

#### Sprint 15. Documentación, rendimiento y preparación para crecer
*Objetivo: primera versión mantenible por otra persona.*

- [x] Documentar endpoints con OpenAPI — Swagger con comentarios XML generados desde el código.
- [x] Ejemplos de peticiones y respuestas — en `src/WMS.Api/Api.http` (Auth, Users, Products, Warehouses, Inventory) y en este README.
- [x] Documentar códigos de error — tabla de ProblemDetails en la sección de Manejo de errores (incluidos `401`, `409` y `429`).
- [x] Documentar autenticación — sección [Autenticación (JWT)](#autenticación-jwt) con el flujo completo, decisiones tomadas y ejemplo de uso; `Api.http` incluye register/login/me/refresh/revoke.
- [x] Revisar índices de PostgreSQL — `Email`, `SKU` y `Name` únicos filtrados por `SoftDelete`; `UUID` únicos; `(WarehouseId, ProductId)` único en inventario; FKs indexadas (`UserId`, `ProductId`, `WarehouseId`).
- [x] Revisar unicidad e integridad referencial — unicidad resuelta con índices filtrados; integridad referencial con `DeleteBehavior.Restrict` en `InventoryItem` (no se puede borrar en cascada un producto/almacén con existencias) y `Cascade` en `refresh_tokens`.
- [x] `AsNoTracking()` en lecturas — aplicado en todos los `Get*` de los repositories.
- [x] `CancellationToken` en operaciones asíncronas — propagado desde el Controller hasta `SaveChangesAsync`.
- [x] Revisar SQL generado por EF Core — verificado durante el desarrollo (conteo antes de paginar, sin materialización prematura; `ExecuteUpdateAsync` condicional para inventario).
- [ ] Medir rendimiento antes de optimizar — **deferido a propósito**: no hay métricas ni volumen real que justifique optimizar, y hacerlo a ciegas solo añadiría complejidad. Revisar cuando existan flujos reales (candidatos: `GET /api/InventoryItem` con muchos almacenes y `search` en listados grandes).
- [ ] Configurar logs y variables de entorno para producción — **parcial**: las variables ya están definidas, cableadas y documentadas (`ConnectionStrings__DefaultConnection`, `Auth__Secret` con arranque obligatorio, `Cors__AllowedOrigins`, `Database__ApplyMigrationsOnStartup`, sección `RateLimit`) y `appsettings.json` fija `Default: Information` / `Microsoft.AspNetCore: Warning`. Falta elegir el **destino** de los logs (consola, Seq, ELK, servicio de la nube) cuando exista el entorno real de despliegue; no se decide a ciegas.
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

Los sprints 11, 13, 14 y el bloque implementable del 15 están **cerrados y verificados** (`dotnet test` → 196 pruebas, build sin advertencias). Lo que queda, en orden de utilidad:

1. **Desplegar y observar** — levantar en un entorno real con `ASPNETCORE_ENVIRONMENT=Production`, `Auth__Secret` y `Cors__AllowedOrigins` definidos; elegir entonces el destino de los logs (ítem abierto del Sprint 15).
2. **Métricas de rendimiento** — medir antes de optimizar (ítem abierto del Sprint 15). Sin volumen real no hay nada que justifique tocar el código.
3. **Sprint 8 (pendientes menores)** — `SortBy` con lista blanca y `fields` solo si un consumidor lo pide explícitamente.
4. **Sprint 12 ampliado** — si se toca inventario con más tráfico, subir el nivel de las pruebas de concurrencia a paralelismo real entre peticiones.
5. **Roles y políticas** — cuando haya más de un tipo de usuario; hoy solo se distingue autenticado/no autenticado.
