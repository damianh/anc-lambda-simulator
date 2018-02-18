using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;

namespace AncLambdaSim
{
    /// <summary>
    ///     Represents an instance of an AspNetCore server that would
    ///     live in a lambda function.
    /// </summary>
    internal class ServerInstance : IDisposable
    {
        private readonly int _coldStartDelay;
        private readonly TestServer _testServer;
        private int _isFirstTime;
        private HttpClient _httpClient;

        public ServerInstance(
            CreateWebHostBuilder createWebHostBuilder,
            int coldStartDelay)
        {
            _coldStartDelay = coldStartDelay;
            _testServer = new TestServer(createWebHostBuilder());
            ActivationTime = DateTime.UtcNow;
        }

        /// <summary>
        ///     The time this server was activated.
        /// </summary>
        public DateTime ActivationTime { get; }

        /// <summary>
        ///     An HTTP client to invoke the server.
        /// </summary>
        public async Task<HttpClient> GetHttpClient()
        {
            if (Interlocked.CompareExchange(ref _isFirstTime, 1, 0) == 0)
            {
                await Task.Delay(_coldStartDelay);
                _httpClient = _testServer.CreateClient();
            }

            return _httpClient;
        }

        public string InstanceId { get; } = Guid.NewGuid().ToString();

        public void Dispose()
        {
            _testServer.Dispose();
        }
    }
}