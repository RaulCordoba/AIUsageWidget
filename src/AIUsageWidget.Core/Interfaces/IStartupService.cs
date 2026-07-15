namespace AIUsageWidget.Core.Interfaces;

public interface IStartupService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
