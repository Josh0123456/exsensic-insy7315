// Run the browser tests one at a time: they all hit the same small staging server from one machine,
// and sign-ins from one IP share the API's login rate limit (5 per minute).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
