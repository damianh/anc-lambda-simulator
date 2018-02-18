using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace AncLambdaSim.Tests
{
    public class LambdaSimulatorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public LambdaSimulatorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task LoadTest()
        {
            /* The very nature of this test is non-deterministic. AWS controls
               the lifecycle of a function instance but is entirely that is
               entirely at the behest of AWS. Given a set of values around
               maximum permitted concurrency, when the requests are made within
               a window, some of the responses will be 200 OK, others will be
               429 Too Many Requests. Due to the parallel operations and varying
               executions, the exact numbers are not deterministic.
             */

            CreateWebHostBuilder createWebHostBuilder = () =>
                WebHost.CreateDefaultBuilder()
                    .UseStartup<Startup>();

            // Tweaking these values will result is differing distributions of
            // 200 and 429 responses.

            // increase to reduce the count of 429s. No 429s when >= numberOfRequests
            var maxConcurrency = 10;

            // should be longer than coldStartDelay otherwise no instances are
            // re-used and less than maxDelayBeforeRequest to simulate instance
            // disposing and new instances statting. Typically an active instance
            // will be kept warm for much longer than this.
            var instanceLifespan = TimeSpan.FromMilliseconds(750); 

            // Total number of requests issued.
            var numberOfRequests = 250;

            //  The maxium delay before a request is issued.
            var maxDelayBeforeRequest = 1000;

            // The delay experienced when cold starting a lambda function. Realy
            // world typically 3-8 seconds depending on cold start Olonger) or
            // from hibernation (shorter).
            var coldStartDelay = 300;

            // The time taken to execute a request. Typically 30-150ms.
            var requestExecutionDuration = 9; 

            using (var simulator = new LambdaSimulator(
                createWebHostBuilder,
                maxConcurrency,
                instanceLifespan,
                coldStartDelay,
                requestExecutionDuration))
            {
                var httpClient = simulator.CreateHttpClient();
                var requestDelay = new Random();

                // Fire off a number of concurrent requests, distributed over a window (maxDelayBeforeRequest)
                var tasks = Enumerable.Range(0, numberOfRequests)
                    .Select(async _ =>
                    {
                        await Task.Delay(requestDelay.Next(0, maxDelayBeforeRequest));
                        return await httpClient.GetAsync("https://example.com/");
                    });
                var responses = await Task.WhenAll(tasks);

                var tooManyRequestsCount = responses.Count(r => r.StatusCode == (HttpStatusCode) 429);
                var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
                var uniqueServers = responses
                    .Where(r => r.StatusCode == HttpStatusCode.OK)
                    .Select(r => r.Headers.GetValues("ServerInstanceId").Single()).Distinct().Count();

                tooManyRequestsCount.ShouldBeGreaterThan(0);
                okCount.ShouldBeGreaterThanOrEqualTo(maxConcurrency);
                uniqueServers.ShouldBeGreaterThanOrEqualTo(maxConcurrency); 

                _testOutputHelper.WriteLine($"Status code 429: {tooManyRequestsCount}");
                _testOutputHelper.WriteLine($"Status code 200: {okCount}");
                _testOutputHelper.WriteLine($"Unique servers spawned: {uniqueServers}");
            }
        }
    }
}