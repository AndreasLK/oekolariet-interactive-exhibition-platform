using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// Registers authentication middleware at the front of the test host pipeline.
/// </summary>
internal sealed class AutoAuthenticateStartupFilter : IStartupFilter
{
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                        app.UseAuthentication();
                        next(obj: app);
                };
}

