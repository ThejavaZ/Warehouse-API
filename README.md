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

## Roadmap pendiente

- [ ] Endpoints de autenticación (login/refresh) con JWT y políticas de autorización.
- [ ] Migrar a hash las contraseñas existentes en texto plano de datos previos.
- [ ] Rate limiting y CORS restrictivo en producción.
- [ ] Endpoints de productos/almacén sobre esta misma arquitectura.
