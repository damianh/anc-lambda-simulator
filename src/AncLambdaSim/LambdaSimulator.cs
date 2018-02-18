using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace AncLambdaSim
{
    // https://docs.aws.amazon.com/lambda/latest/dg/concurrent-executions.html

    /// <summary>
    ///     Represents a deployed AspNetCore lambda based application.
    /// </summary>
    public sealed class LambdaSimulator : IDisposable
    {
        private readonly CreateWebHostBuilder _createWebHostBuilder;
        private readonly int _maxConcurrency;
        private readonly TimeSpan _maxInstanceLifetime;
        private readonly int _instanceColdStartDelay;
        private readonly int _requestExecutionDuration;
        private readonly ConcurrentQueue<ServerInstance> _concurrentQueue = new ConcurrentQueue<ServerInstance>();
        private int _currentExecutionCount;
        private bool _isDisposed;

        /// <summary>
        ///     Initialized a new instance of <see cref="LambdaSimulator"/>
        /// </summary>
        /// <param name="createWebHostBuilder">
        ///     A delegate to create the web host builder of the hosted
        ///     AspNetCore application.
        /// </param>
        /// <param name="maxConcurrency">
        ///     The maximum number of concurrent in-flight requests being
        ///     handled. Each in-flight request will have be handled by an
        ///     independent AspNetCore application (aka ServerInstance).
        /// </param>
        /// <param name="maxInstanceLifetime">
        ///     The maximum liftime of a Server Instance. Generally AWS will
        ///     keep used lambda function instances 'warm', however their
        ///     lifecycle is completely at the behest of AWS. Set this to a low
        ///     value simulate the instance being shut down and a new one
        ///     starting.
        /// </param>
        /// <param name="instanceColdStartDelay">
        ///     The delay experiences when starting an AspNetCore lambda
        ///     function from cold. Typically, this takes 4-8 seconds.
        /// </param>
        public LambdaSimulator(
            CreateWebHostBuilder createWebHostBuilder,
            int maxConcurrency,
            TimeSpan maxInstanceLifetime,
            int instanceColdStartDelay,
            int requestExecutionDuration)
        {
            _createWebHostBuilder = createWebHostBuilder ?? throw new ArgumentNullException(nameof(createWebHostBuilder));
            if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
            _maxConcurrency = maxConcurrency;
            _maxInstanceLifetime = maxInstanceLifetime;
            _instanceColdStartDelay = instanceColdStartDelay;
            _requestExecutionDuration = requestExecutionDuration;
        }

        private ServerInstance TryStart()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LambdaSimulator));

            if (_currentExecutionCount >= _maxConcurrency)
            {
                return null;
            }
            _currentExecutionCount ++;
            if (_concurrentQueue.TryDequeue(out var serverInstance))
            {
                // If instance is too old, recycle it.
                if (serverInstance.ActivationTime.Add(_maxInstanceLifetime) < DateTime.UtcNow)
                {
                    serverInstance.Dispose();
                    serverInstance = new ServerInstance(_createWebHostBuilder, _instanceColdStartDelay);
                }
            }
            else
            {
                serverInstance = new ServerInstance(_createWebHostBuilder, _instanceColdStartDelay);
            }

            return serverInstance;
        }

        private void Finish(ServerInstance serverInstance)
        {
            if (!_isDisposed)
            {
                _concurrentQueue.Enqueue(serverInstance);
                _currentExecutionCount--;
            }
        }

        public HttpClient CreateHttpClient()
        {
            var handler = new ServerInstanceHandler(TryStart, Finish, _requestExecutionDuration);
            return new HttpClient(handler);
        }

        public void Dispose()
        {
            _isDisposed = true;
            while (_concurrentQueue.TryDequeue(out var serverInstance))
            {
                serverInstance.Dispose();
            }
        }
    }
}
