using System.Net;
using System.Net.Http.Json;
using Api.Warehouses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WMS.Application.Common;

namespace WMS.Api.Test;

public class WarehouseApiTests : IntegrationTestBase, IClassFixture<WarehouseApiFactory>
{
    public WarehouseApiTests(WarehouseApiFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await ClearDatabaseAsync();
        await AuthenticateAsync();
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/Warehouse");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns201()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Almacén Norte", Location = "Bilbao" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await ReadJsonAsync(response);

        Assert.Equal("Almacén Norte", created.GetProperty("name").GetString());
        Assert.Equal("Bilbao", created.GetProperty("location").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Post_DuplicateName_Returns400()
    {
        await CreateWarehouseAsync("Duplicado");

        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Duplicado" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("validation_error", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Get_WithSearch_FindsByNameOrLocation()
    {
        await CreateWarehouseAsync("Norte", "Bilbao");
        await CreateWarehouseAsync("Sur", "Sevilla");

        var response = await Client.GetAsync("/api/Warehouse?search=bilbao");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<WarehouseListViewModel>>(JsonOptions);

        Assert.Equal(1, result!.TotalItems);
        Assert.Equal("Norte", result.Items.Single().Name);
    }

    [Fact]
    public async Task Put_ExistingWarehouse_UpdatesAndReturns200()
    {
        var created = await CreateWarehouseAsync("Original");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync(
            $"/api/Warehouse/{id}",
            new { Name = "Renombrado", Location = "Valencia" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<WarehouseViewModel>(JsonOptions);

        Assert.Equal("Renombrado", updated!.Name);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/Warehouse/9999",
            new { Name = "Ghost" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenNameBecomesReusable()
    {
        var created = await CreateWarehouseAsync("Borrable");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await Client.DeleteAsync($"/api/Warehouse/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await Client.GetAsync("/api/Warehouse");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<WarehouseListViewModel>>(JsonOptions);

        Assert.Equal(0, result!.TotalItems);

        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Borrable" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, reuseResponse.StatusCode);
    }
}
