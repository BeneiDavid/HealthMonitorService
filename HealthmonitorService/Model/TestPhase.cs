namespace HealthMonitorService.Model
{
    // Reserved for future runner integration / recovery actions
    public enum TestPhase
    {
        None = 0,
        Setup,
        Running,
        BetweenTests,
        Cleanup,
        Finished
    }
}
