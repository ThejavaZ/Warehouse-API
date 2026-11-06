# ADR-0001: Adopción de arquitectura hexagonal

- **Estado**: Aceptado
- **Fecha**: 2026-11-05
- **Contexto**: migración del monolito `src/Api` a las capas `WMS.*`

## Contexto

La API nació como un monolito organizado por módulos funcionales dentro de
`src/Api` (`users/`, `products/`, `auth/`…), donde cada módulo contenía
controlador, servicio, repositorios, interfaces y modelos mezclados con el
acceso a datos de EF Core. Eso producía:

- **Acoplamiento a EF Core** en todas las capas: los servicios y repositorios
  dependían directamente del `DbContext` y de `Microsoft.EntityFrameworkCore`.
- **Dificultad para sustituir infraestructura** (otro proveedor de persistencia,
  otra estrategia de tokens) sin tocar el código de negocio.
- **Tests de unidad** que necesitaban montar EF (SQLite en memoria) para
  probar reglas de negocio que no dependen de la persistencia.

Además, la migración llega con restricciones duras: el **contrato HTTP está
congelado** (el frontend `Warehouse-Client` está terminado) y el **esquema de
base de datos está congelado** (dos migraciones existentes, ninguna nueva).

## Decisión

Adoptar una **arquitectura hexagonal (puertos y adaptadores)** con cuatro
proyectos de producción y un composition root:

| Proyecto | Contenido | Dependencias |
|---|---|---|
| `WMS.Domain` | Entidades y `Model` (base de identidad) | ninguna |
| `WMS.Application` | Casos de uso (servicios) y **puertos** (`Ports/`, interfaces) | `WMS.Domain` |
| `WMS.Infrastructure` | **Adaptadores**: `AppDbContext` + configuraciones + migraciones + repositorios (EF/Npgsql), `JwtTokenService`, `BcryptPasswordHasher` | `WMS.Application` |
| `WMS.Api` | Composition root: `Program`, configuración, middleware y controladores | las tres capas |
| `src/Api` | **Puente temporal**: casos de uso, entidades y DTOs aún no migrados | — (sin referencias) |

Reglas derivadas:

1. **Las dependencias apuntan hacia dentro**: los casos de uso definen los
   puertos; los adaptadores los implementan; el composition root es el único
   sitio que conoce las cuatro capas.
2. **Entity Framework vive únicamente en `WMS.Infrastructure`**
   (`Persistence/`: contexto, `IEntityTypeConfiguration<T>`, migraciones y
   repositorios). EF descubre las migraciones en el assembly del contexto, por
   lo que las migraciones acompañan al `AppDbContext`.
3. Los casos de uso dependen de **interfaces** (`IUserRepository`,
   `IPasswordHasher`, `ITokenService`), nunca de clases concretas de
   adaptadores.
4. `WMS.Infrastructure` referencia a `src/Api` solo como **puente transitorio**
   (las entidades siguen allí); desaparece al migrar el último módulo.

Durante la migración se mantienen además el esquema congelado y el contrato
HTTP congelado, con los gates documentados en [MIGRATION.md](../../MIGRATION.md).

## Consecuencias

**Positivas**

- Los casos de uso se prueban sin EF (fakes/NUnit); los adaptadores se prueban
  aparte (SQLite para repositorios, JWT real para el token service).
- Sustituir persistencia o estrategia de tokens = implementar otro adaptador.
- Las dependencias de compilación hacen visible la arquitectura: un `using`
  prohibido en la capa equivocada no compila.

**Negativas y riesgos**

- Más proyectos y más indirección para una API pequeña: cada módulo repartido
  en cuatro sitios.
- Estado intermedio con **código duplicado en dos mundos** (`src/Api` y
  `WMS.*`) hasta que T9–T15 completen el traslado.
- Mover entidades obliga a actualizar los ficheros generados de EF (snapshot y
  `Designer` referencian tipos por nombre completo en strings), con el riesgo
  asociado al esquema congelado.

## Alternativas consideradas

- **No mover nada (monolito ordenado por carpetas)** — rechazado: no resuelve
  el acoplamiento a EF ni la sustitución de adaptadores, y el coste de la
  migración ya se está pagando por partes.
- **Reescritura grande de una vez** — rechazado: rompería el contrato HTTP y el
  esquema congelado a la vez; inviable con el frontend ya terminado.
- **Capas clásicas (N-tier: Data → Business → Presentation)** — descartado a
  favor de hexagonal: en N-tier las interfaces suelen definirse "hacia arriba"
  y el caso de uso termina dependiendo de la capa de datos; en hexagonal los
  puertos pertenecen a la aplicación y los adaptadores se enganchan desde
  fuera, que es exactamente lo que permite migrar módulo a módulo.
