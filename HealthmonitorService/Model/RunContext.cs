namespace HealthMonitorService.Model
{
    public record class RunContext
    {
        public string? BuildId { get; init; }           // ID of the build being tested
        public TestPhase CurrentPhase { get; init; }    // Current phase of the test run (e.g. "during test", "between tests" )
        public bool IsActive { get; init; }             // Indicates whether testing is currently active (true during tests, false otherwise)
    }
}
