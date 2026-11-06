using System.ComponentModel.DataAnnotations;
using WMS.Application.Common;
using WMS.Application.Users;

namespace Api.Test;

[TestFixture]
public class ValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        return results;
    }

    [Test]
    public void UserCreateDTO_WithValidData_HasNoErrors()
    {
        var dto = new UserCreateDTO
        {
            Name = "Juan Perez",
            Email = "juan@test.com",
            Password = "Secret123!"
        };

        Assert.That(Validate(dto), Is.Empty);
    }

    [Test]
    public void UserCreateDTO_WithoutPassword_HasError()
    {
        var dto = new UserCreateDTO { Name = "Juan Perez", Email = "juan@test.com", Password = null! };

        Assert.That(Validate(dto), Has.Some.Property("MemberNames").Contains("Password"));
    }

    [Test]
    public void UserCreateDTO_WithShortPassword_HasError()
    {
        var dto = new UserCreateDTO { Name = "Juan Perez", Email = "juan@test.com", Password = "1234567" };

        Assert.That(Validate(dto), Has.Some.Property("MemberNames").Contains("Password"));
    }

    [Test]
    public void UserCreateDTO_WithInvalidEmail_HasError()
    {
        var dto = new UserCreateDTO { Name = "Juan Perez", Email = "not-an-email", Password = "Secret123!" };

        Assert.That(Validate(dto), Has.Some.Property("MemberNames").Contains("Email"));
    }

    [Test]
    public void UserCreateDTO_WithTooShortName_HasError()
    {
        var dto = new UserCreateDTO { Name = "A", Email = "juan@test.com", Password = "Secret123!" };

        Assert.That(Validate(dto), Has.Some.Property("MemberNames").Contains("Name"));
    }

    [Test]
    public void UserUpdateDTO_WithoutPassword_IsValid()
    {
        var dto = new UserUpdateDTO { Name = "Juan Perez", Email = "juan@test.com" };

        Assert.That(Validate(dto), Is.Empty);
    }

    [Test]
    public void UserUpdateDTO_WithShortPassword_HasError()
    {
        var dto = new UserUpdateDTO { Name = "Juan Perez", Email = "juan@test.com", Password = "123" };

        Assert.That(Validate(dto), Has.Some.Property("MemberNames").Contains("Password"));
    }

    [TestCase(1, 1, true)]
    [TestCase(1, 100, true)]
    [TestCase(1, 0, false)]
    [TestCase(1, -1, false)]
    [TestCase(1, 101, false)]
    [TestCase(0, 10, false)]
    [TestCase(-2, 10, false)]
    public void PagedQuery_PaginationLimits_AreValidated(int page, int pageSize, bool expectedValid)
    {
        var query = new PagedQuery { Page = page, PageSize = pageSize };
        var results = Validate(query);

        if (expectedValid)
        {
            Assert.That(results, Is.Empty);
        }
        else
        {
            Assert.That(results, Is.Not.Empty);
        }
    }
}
