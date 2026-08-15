using Xunit;

// The integration host owns process-local singleton operation gates and stores. Tests that need
// concurrency still create concurrent requests inside one test; test cases themselves must not
// share those synthetic runtime singletons across parallel fixtures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
