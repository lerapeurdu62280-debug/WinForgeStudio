using System;

namespace WinForge.Exceptions;

public class WinForgeBuildException : Exception
{
    public string Stage { get; }

    public WinForgeBuildException(string stage, string message, Exception? inner = null)
        : base(message, inner)
    {
        Stage = stage;
    }
}
