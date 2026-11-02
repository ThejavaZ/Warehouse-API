using System.Net;
using System.Net.Http.Json;
using Api.Common;
using Api.Warehouses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Test.Integration;

[TestFixture]
public class WarehouseApiTests : IntegrationTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await ClearDatabaseAsync();
        await AuthenticateAsync();
    }

    [Test]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/Warehouse");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Post_Returns201()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Almacén Norte", Location = "Bilbao" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await ReadJsonAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(created.GetProperty("name").GetString(), Is.EqualTo("Almacén Norte"));
            Assert.That(created.GetProperty("location").GetString(), Is.EqualTo("Bilbao"));
            Assert.That(created.GetProperty("isActive").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Post_DuplicateName_Returns400()
    {
        await CreateWarehouseAsync("Duplicado");

        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Duplicado" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("validation_error"));
    }

    [Test]
    public async Task Get_WithSearch_FindsByNameOrLocation()
    {
        await CreateWarehouseAsync("Norte", "Bilbao");
        await CreateWarehouseAsync("Sur", "Sevilla");

        var response = await Client.GetAsync("/api/Warehouse?search=bilbao");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<WarehouseListViewModel>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Name, Is.EqualTo("Norte"));
        });
    }

    [Test]
    public async Task Put_ExistingWarehouse_UpdatesAndReturns200()
    {
        var created = await CreateWarehouseAsync("Original");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync(
            $"/api/Warehouse/{id}",
            new { Name = "Renombrado", Location = "Valencia" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<WarehouseViewModel>(JsonOptions);

        Assert.That(updated!.Name, Is.EqualTo("Renombrado"));
    }

    [Test]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/Warehouse/9999",
            new { Name = "Ghost" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_ThenNameBecomesReusable()
    {
        var created = await CreateWarehouseAsync("Borrable");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await Client.DeleteAsync($"/api/Warehouse/{id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var listResponse = await Client.GetAsync("/api/Warehouse");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<WarehouseListViewModel>>(JsonOptions);

        Assert.That(result!.TotalItems, Is.Zero);

        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = "Borrable" },
            JsonOptions);

        Assert.That(reuseResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }
}
