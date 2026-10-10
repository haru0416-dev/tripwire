using Xunit;

// Some tests switch process-wide settings (UI language, Udon event names, the type-check hook): run classes one at a
// time so no test sees another's setting. The whole suite takes about ten seconds, most of it PropertyTests on big triggers.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
