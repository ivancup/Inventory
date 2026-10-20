using Inventory.Api.Controllers;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Inventory.UnitTest.Api;

[TestClass]
public sealed class CategoriesControllerTests
{
    [TestMethod]
    public async Task CreateCategory_UsesAuthenticatedSubjectAndReturnsCreated()
    {
        var service = new Mock<IInventoryService>();
        var request = new CategoryWriteRequest("Office");
        var response = new CategoryResponse(10, "Office");
        service.Setup(item => item.CreateCategoryAsync(
                request,
                new ActorIdentity("keycloak-subject", "Example User", "example@example.local"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = CreateController(service.Object);

        var result = await controller.CreateCategory(request, CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        Assert.AreEqual(response, ((CreatedAtActionResult)result.Result!).Value);
        service.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteCategory_UsesAuthenticatedSubject()
    {
        var service = new Mock<IInventoryService>();
        var response = new CategoryResponse(10, "Office");
        service.Setup(item => item.DeleteCategoryAsync(
                10,
                new ActorIdentity("keycloak-subject", "Example User", "example@example.local"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = CreateController(service.Object);

        var result = await controller.DeleteCategory(10, CancellationToken.None);

        Assert.AreEqual(response, result);
        service.VerifyAll();
    }

    private static CategoriesController CreateController(IInventoryService service) =>
        new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("sub", "keycloak-subject"),
                        new Claim("name", "Example User"),
                        new Claim("email", "example@example.local")
                    ], "test"))
                }
            }
        };
}
