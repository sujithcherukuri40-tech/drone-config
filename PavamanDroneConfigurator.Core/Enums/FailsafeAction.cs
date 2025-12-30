namespace PavamanDroneConfigurator.Core.Enums;

public enum FailsafeAction
{
    Disabled = 0,
    ReportOnly = 1,
    RTL = 2,
    Land = 3,
    SmartRTL = 4,
    SmartRTLOrRTL = 5,
    SmartRTLOrLand = 6,
    Terminate = 7,
    Continue = 8
}
