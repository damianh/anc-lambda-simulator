using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AncLambdaSim.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {}

        public void Configure(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                await Task.Delay(5);
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}