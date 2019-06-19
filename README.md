# AspNetCore Lambda Simulator

An experiment in simulating lambda lifecycle that can help testing the behaviour
of an AspNetCore lambda based application.

## How it works

Best thing to do is run / debug / step through, the [`LambdaSimulatorTests`](https://github.com/damianh/anc-lambda-simulator/blob/master/src/AncLambdaSim.Tests/LambdaSimulatorTests.cs#L23) to
see how this works.

 1. Each HTTP request is handled by precisely one AspNetCore function at a time.

 1. When a request arrives at the simulator, it checks for an available
    `ServerInstance` which represents a warm idle function.

 1. If none available, a new `ServerInstance` is attempted to be created. If the
    max concurrency limit is reached no more are created and the client gets a
    `429 Too Many Requests`.

 1. If one is created, the _first_ request to it is delayed by the
    `coldStartDelay`. Real world indicates this is typically ~2-8 seconds depending
    on the RAM (and CPU) allocated to the lambda.

 1. If one was already available, the instace's age is checked against the
    `instanceLifespan` and if older, then it is disposed and a new one created.
    If younger, then it is reused. Then the request is delayed by
    `requestExecutionDuration` before finally being handled. Real world
    indicates this is typically ~100ms.

 1. When a request is completed, the `ServerInstance` is placed back in the pool
    (implemented as a queue) for re-use by subsequent requests.

## Things considered:

 1. Evey _concurrent_ HTTP request will be executed in it's own independent and
    isolated AspNetCore Lambda instance.

 1. While AspNetCore is fully multi-threaded / supports concurrent requests, an
    AspNetCore based lambda application will strictly handle one HTTP request at
    a time.

 1. There are concurrency limits with respect to the number of lambda functions
    running at function level and AWS account level.

 1. AWS keeps lambda functions alive for a period after for performance reasons.
    However there is a cold start cost for each _new_ lambda function instance.

 1. Inactive Lambda functions are hibernated at first and then deactived
    entirely later. Re-activating from hibernation is typically ~3 seconds.

 1. Concurrent requests to a de-activated functon will result in multiple
    functions being started - all of them cold starts with associated delays.

## Things not considered:

 1. Any sort of **static** state / caching in the ApsNetCore appliction that might
    leak A) between `ServerInstance`s or B) betwen disposing and creating a new
    `ServerInstance`.

 2. APIGW and any sort of throttling / handling in front of an AspNetCore lambda
    function.

Any questions, ping [me on Twitter](https://twitter.com/randompunter).
