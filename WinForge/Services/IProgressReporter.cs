namespace WinForge.Services;

public interface IProgressReporter
{
    void Log(string message);
    void SetStatus(string message);
    void SetProgress(double value);
}
