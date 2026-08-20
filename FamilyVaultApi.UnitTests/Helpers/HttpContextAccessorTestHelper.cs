using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Helpers
{
    public static class HttpContextAccessorTestHelper
    {
        public static Mock<IHttpContextAccessor> WithUser(ClaimsPrincipal user)
        {
            var context = new DefaultHttpContext { User = user };
            return WithContext(context);
        }

        public static Mock<IHttpContextAccessor> WithNoHttpContext()
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(x => x.HttpContext).Returns((HttpContext?)null);
            return accessor;
        }

        private static Mock<IHttpContextAccessor> WithContext(HttpContext context)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(x => x.HttpContext).Returns(context);
            return accessor;
        }
    }
}
