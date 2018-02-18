using Microsoft.AspNetCore.Hosting;

namespace AncLambdaSim
{
    /// <summary>
    ///     A delegate to create a <see cref="IWebHostBuilder"/>. Called too
    ///     instantiate the AspNetCore application.
    /// </summary>
    /// <returns></returns>
    public delegate IWebHostBuilder CreateWebHostBuilder();
}