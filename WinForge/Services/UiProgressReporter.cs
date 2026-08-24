using System;

namespace WinForge.Services;

public class UiProgressReporter : IProgressReporter
{
    private readonly MainWindow _mainWindow;
    private readonly string _logPrefix;
    private readonly Action<double>? _progressSink;

    public UiProgressReporter(MainWindow mainWindow, string logPrefix, Action<double>? progressSink = null)
    {
        _mainWindow = mainWindow;
        _logPrefix = logPrefix;
        _progressSink = progressSink;
    }

    public void Log(string message) => _mainWindow.AppendLog($"[{_logPrefix}] {message}");

    public void SetStatus(string message) => _mainWindow.DispatcherQueue.TryEnqueue(() => _mainWindow.SetStatus(message));

    public void SetProgress(double value) => _mainWindow.DispatcherQueue.TryEnqueue(() => _progressSink?.Invoke(value));
}
