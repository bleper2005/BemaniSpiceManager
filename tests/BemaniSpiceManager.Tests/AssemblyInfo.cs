// Server modules are shared singletons and some tests start real processes on fixed ports,
// so tests run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
