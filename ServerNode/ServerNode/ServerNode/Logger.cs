namespace ServerNode;

public class Logger
{
    #region Fields
    
    private static readonly object _messageLock = new();
    
    #endregion

    #region Properties

    public static bool IsLoggerEnabled { get; set; }= true;
    public static bool IsDebugEnabled { get; set; } = true;

    #endregion

    #region Public Methods

    public static void ErrorMessage(string? message)
    {
        LogToConsole("[Error] " + message, ConsoleColor.DarkRed);
    }
        
    public static void HighlightMessage(string? message)
    {
        LogToConsole("[Status] " + message, ConsoleColor.DarkGreen);
    }
        
    public static void TcpInMessage(string? message)
    {
        LogToConsole("[TCP:in] " + message, ConsoleColor.DarkCyan);
    }
    
    public static void InfoMessage(string? message)
    {
        if(!IsDebugEnabled)
            return;
        
        LogToConsole("[Info] " + message, ConsoleColor.Gray);
    }

    #endregion

    #region Private Method

    private static void LogToConsole(string? message, ConsoleColor color)
    {
        if(!IsLoggerEnabled)
            return;
        
        lock (_messageLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    #endregion
}