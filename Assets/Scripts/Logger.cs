using UnityEngine;
using Unity.Netcode;

public class Logger : NetworkBehaviour
{
    [Header("Logging Settings")]
    [SerializeField] private string logFileName = "log.txt";
    [SerializeField] private bool logToFile = true;
    [SerializeField] private bool logToConsole = true;

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

    public void Log(string message, bool includeTimestamp = true, string callerName = "")
    {
        string formattedMessage = message;

        if (!string.IsNullOrEmpty(callerName))
        {
            formattedMessage = $"[{callerName}] {formattedMessage}";
        }

        if (includeTimestamp)
        {
            formattedMessage = $"[{System.DateTime.Now}] {formattedMessage}";
        }

        // If not server, send to server for logging
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            LogToServerRpc(formattedMessage);
            return;
        }

        WriteLog(formattedMessage);
    }

    private void WriteLog(string formattedMessage)
    {
        if (logToConsole)
        {
            Debug.Log(formattedMessage);
        }
        if (logToFile)
        {
            System.IO.File.AppendAllText(logFilePath, formattedMessage + "\n");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LogToServerRpc(string formattedMessage, ServerRpcParams rpcParams = default)
    {
        WriteLog($"[CLIENT {rpcParams.Receive.SenderClientId}] {formattedMessage}");
    }
}
