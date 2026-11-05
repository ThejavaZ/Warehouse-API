## Qué cambia

<!-- Describe el cambio en una o dos frases. Si cierra un issue, usa "Closes #123". -->

## Por qué

<!-- El problema que resuelve o la decisión que refleja. -->

## Cómo se ha verificado

<!-- Marca lo que hayas ejecutado de verdad. -->

- [ ] `dotnet build WarehouseApi.sln` sin errores ni advertencias
- [ ] `dotnet test WarehouseApi.sln` en verde
- [ ] Comprobado a mano el flujo afectado (Swagger o frontend en `http://localhost:5174`)
- [ ] Añadidos o actualizados tests para el cambio

## Contrato HTTP

<!-- Rellena solo si tocas endpoints. El frontend ya está terminado: si el contrato
     cambia, Warehouse-Client tiene que actualizarse en el mismo ciclo. -->

- [ ] Este cambio **no** modifica el contrato HTTP
- [ ] Este cambio **sí** lo modifica:
  - Método y ruta:
  - Cambio en la respuesta:
  - ¿Warehouse-Client actualizado?

## Checklist

- [ ] Los commits siguen [Conventional Commits](https://www.conventionalcommits.org/)
- [ ] No he subido secretos (revisado con `git diff` en `appsettings.json` y `.env`)
- [ ] La documentación afectada (README, `Api.http`) queda actualizada
