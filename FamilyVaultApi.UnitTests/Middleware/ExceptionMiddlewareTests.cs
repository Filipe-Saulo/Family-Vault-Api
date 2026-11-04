using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace FamilyVaultApi.UnitTests.Middleware
{
    public class ExceptionMiddlewareTests
    {
        private static async Task<int> InvokeAsync(Exception toThrow)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new ExceptionMiddleware(_ => throw toThrow, NullLogger<ExceptionMiddleware>.Instance);

            await middleware.InvokeAsync(context);

            return context.Response.StatusCode;
        }

        private static async Task<(int Status, string Body)> InvokeWithBodyAsync(Exception toThrow)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new ExceptionMiddleware(_ => throw toThrow, NullLogger<ExceptionMiddleware>.Instance);

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            return (context.Response.StatusCode, body);
        }

        // ArgumentException continua 500 de propósito: não é tratada explicitamente.
        [Fact]
        public async Task InvokeAsync_WhenArgumentException_ShouldReturn500()
            => (await InvokeAsync(new ArgumentException("senha não coincide"))).Should().Be(500);

        [Fact]
        public async Task InvokeAsync_WhenSecurityTokenException_ShouldReturn401()
            => (await InvokeAsync(new SecurityTokenException("Refresh token inválido"))).Should().Be(401);

        [Fact]
        public async Task InvokeAsync_WhenDbUpdateException_ShouldReturn409WithConflictMessage()
        {
            var (status, body) = await InvokeWithBodyAsync(new DbUpdateException("FK violation"));

            status.Should().Be(409);
            using var json = System.Text.Json.JsonDocument.Parse(body);
            json.RootElement.GetProperty("message").GetString().Should().Be("Operação conflita com registros relacionados.");
        }

        [Fact]
        public async Task InvokeAsync_WhenConflictException_ShouldReturn409WithExceptionMessage()
        {
            var (status, body) = await InvokeWithBodyAsync(new ConflictException("Categoria possui 3 transações."));

            status.Should().Be(409);
            using var json = System.Text.Json.JsonDocument.Parse(body);
            json.RootElement.GetProperty("message").GetString().Should().Be("Categoria possui 3 transações.");
        }
    }
}
