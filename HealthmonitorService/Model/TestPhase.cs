namespace HealthMonitorService.Model
{
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
