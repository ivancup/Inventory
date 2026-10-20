using Inventory.Api.Controllers;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Inventory.UnitTest.Api;

[TestClass]
public sealed class ProductsControllerTests
{
    [TestMethod]
    public async Task CreateProduct_UsesAuthenticatedSubjectAndReturnsCreated()
    {
        var service = new Mock<IInventoryService>();
        var request = new ProductWriteRequest("New item", 2);
        var response = new ProductResponse(10, "New item", 2, "Electronics", 0, false);
        service.Setup(item => item.CreateProductAsync(
                request,
                new ActorIdentity("keycloak-subject", "Example User", "example@example.local"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new ProductsController(service.Object)
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

        var result = await controller.CreateProduct(request, CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        service.VerifyAll();
    }
}
