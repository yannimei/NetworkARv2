using UnityEngine;
using Unity.Netcode;

public class Logger : NetworkBehaviour
{
    public enum LogLevel { Debug, Info, Warning, Error, None }

    [Header("Logging Settings")]
    [SerializeField] private string logFileName = "log.txt";
    [SerializeField] private bool logToFile = true;
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private LogLevel minimumLogLevel = LogLevel.Info;

    private string logFilePath;

    private void Awake()
    {
        logFilePath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
        if (logToFile)
        {
            if (!System.IO.File.Exists(logFilePath))
            {
                System.IO.File.WriteAllText(logFilePath, "[" + System.DateTime.Now + "] Logger initialized.\n");
            }
        }
    }

    public void Log(string message, bool includeTimestamp = true, string callerName = "", LogLevel level = LogLevel.Info)
    {
        if (level < minimumLogLevel || minimumLogLevel == LogLevel.None)
            return;

        string formattedMessage = message;

        if (!string.IsNullOrEmpty(callerName))
        {
            formattedMessage = $"[{callerName}] {formattedMessage}";
        }

        if (includeTimestamp)
        {
            formattedMessage = $"[{System.DateTime.Now}] {formattedMessage}";
        }

        formattedMessage = $"[{level}] {formattedMessage}";

        // If not server, send to server for logging
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            LogToServerRpc(formattedMessage, level);
            return;
        }
        else
        {
            // If server, log directly
            formattedMessage = $"[SERVER/HOST] {formattedMessage}";
        }

        WriteLog(formattedMessage, level);
    }

    public void LogDebug(string message, string callerName = "") => Log(message, true, callerName, LogLevel.Debug);
    public void LogInfo(string message, string callerName = "") => Log(message, true, callerName, LogLevel.Info);
    public void LogWarning(string message, string callerName = "") => Log(message, true, callerName, LogLevel.Warning);
    public void LogError(string message, string callerName = "") => Log(message, true, callerName, LogLevel.Error);

    private void WriteLog(string formattedMessage, LogLevel level)
    {
        if (logToConsole)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;
                default:
                    Debug.Log(formattedMessage);
                    break;
            }
        }
        if (logToFile)
        {
            System.IO.File.AppendAllText(logFilePath, formattedMessage + "\n");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LogToServerRpc(string formattedMessage, LogLevel level, ServerRpcParams rpcParams = default)
    {
        WriteLog($"[CLIENT {rpcParams.Receive.SenderClientId}] {formattedMessage}", level);
    }
}
