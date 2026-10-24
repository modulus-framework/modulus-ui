using Xunit;

// AntDesign keeps its DOM event subscriptions in one static store. Test classes that run in parallel tear down their components at
// the same time, and the store's list changes while it is enumerated. The Blazor tests run one class at a time for that reason.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
