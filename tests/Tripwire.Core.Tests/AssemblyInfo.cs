using Xunit;

// Some tests switch process-wide settings (UI language, Udon event names, the type-check hook): run classes one at a
// time so no test sees another's setting. The whole suite takes well under a second.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
