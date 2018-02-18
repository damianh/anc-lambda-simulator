using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AncLambdaSim
{
    /// <summary>
    ///     Represents an HTTP Message Handler that can forward a request
    ///     to a <see cref="ServerInstance"/>. It does this by cloning
    ///     the incoming HttRequestMessage and forwarding it to
    ///     the server instance.
    /// </summary>
    internal class ServerInstanceHandler : HttpMessageHandler
    {
        private readonly Func<ServerInstance> _getServer;
        private readonly Action<ServerInstance> _onFinish;
        private readonly int _requestExecutionDuration;

        /// <summary>
        ///     Initialized a new instance of <see
        ///     cref="ServerInstanceHandler"/>
        /// </summary>
        /// <param name="getServer">
        ///     Called to get a ServerInstances.
        /// </param>
        /// <param name="onFinish">
        ///     Called when request is complete allowing the ServerInstance be
        ///     returned to the pool.
        /// </param>
        /// <param name="requestExecutionDuration">
        ///     The duration the request takes to complete.
        /// </param>
        public ServerInstanceHandler(
            Func<ServerInstance> getServer,
            Action<ServerInstance> onFinish,
            int requestExecutionDuration)
        {
            _getServer = getServer;
            _onFinish = onFinish;
            _requestExecutionDuration = requestExecutionDuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var serverInstance = _getServer();
            if(serverInstance == null) 
            {
                // no Server instance retured because of concurrency limits
                return new HttpResponseMessage((HttpStatusCode)429);
            }

            request = await CloneHttpRequestMessageAsync(request);
            var client = await serverInstance.GetHttpClient();
            var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            response.Headers.Add("ServerInstanceId", serverInstance.InstanceId);
            await Task.Delay(_requestExecutionDuration, cancellationToken);
            _onFinish(serverInstance); // returns the instance back to the pool to handle subsequent tests.

            return response;
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);

            // Copy the request's content (via a MemoryStream) into the cloned object
            var ms = new MemoryStream();
            if (req.Content != null)
            {
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);

                // Copy the content headers
                if (req.Content.Headers != null)
                {
                    foreach (var h in req.Content.Headers)
                    {
                        clone.Content.Headers.Add(h.Key, h.Value);
                    }
                }
            }

            clone.Version = req.Version;

            foreach (var prop in req.Properties)
            {
                clone.Properties.Add(prop);
            }

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}