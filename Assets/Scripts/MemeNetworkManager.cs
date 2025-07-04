using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Meta.XR.MRUtilityKit;
using System.IO;

public class MemeNetworkManager : NetworkBehaviour
{
    public static MemeNetworkManager Instance { get; private set; }

    [SerializeField] private MRUK mRUK;
    [SerializeField] private Logger logger;
    private readonly Dictionary<ulong, GameObject> playerMemes = new();
    private readonly Dictionary<ulong, int> playerMemeIndices = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RequestSpawnMeme(string memePath, int memeIndex, int playerId, Vector3 menuPosition, Quaternion menuRotation)
    {
        logger.Log($"RequestSpawnMeme called. memePath={memePath}, memeIndex={memeIndex}, playerId={playerId}, isServer={NetworkManager.Singleton.IsServer}", true, nameof(MemeNetworkManager));
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (NetworkManager.Singleton.IsServer)
        {
            logger.Log($"Calling SpawnMeme directly on server.", true, nameof(MemeNetworkManager));
            SpawnMeme(memePath, memeIndex, playerId, menuPosition, menuRotation, localClientId);
        }
        else
        {
            logger.Log($"Calling SpawnMemeServerRpc from client.", true, nameof(MemeNetworkManager));
            SpawnMemeServerRpc(memePath, memeIndex, playerId, menuPosition, menuRotation);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnMemeServerRpc(string memePath, int memeIndex, int playerId, Vector3 menuPosition, Quaternion menuRotation, ServerRpcParams rpcParams = default)
    {
        logger.Log($"SpawnMemeServerRpc received from clientId={rpcParams.Receive.SenderClientId}", true, nameof(MemeNetworkManager));

        ulong ownerClientId = rpcParams.Receive.SenderClientId;
        SpawnMeme(memePath, memeIndex, playerId, menuPosition, menuRotation, ownerClientId);
    }

    private void SpawnMeme(string memePath, int memeIndex, int playerId, Vector3 menuPosition, Quaternion menuRotation, ulong? ownerClientId = null)
    {
        if (ownerClientId.HasValue && playerMemes.TryGetValue(ownerClientId.Value, out var currentPlayerMeme) && currentPlayerMeme != null)
        {
            logger.Log($"[Server] Removing previous meme for clientId={ownerClientId.Value}", true, nameof(MemeNetworkManager));
            if (currentPlayerMeme.TryGetComponent<MemeObjectHandler>(out var handler))
            {
                handler.SaveState();
            }
            if (currentPlayerMeme.TryGetComponent<NetworkObject>(out var netObjToRemove))
            {
                if (netObjToRemove.IsOwner || NetworkManager.Singleton.IsServer)
                    netObjToRemove.Despawn(true);
            }
            else
            {
                Destroy(currentPlayerMeme);
            }
            playerMemes[ownerClientId.Value] = null;
            playerMemeIndices[ownerClientId.Value] = -1;
        }

        logger.Log($"SpawnMeme called. memePath={memePath}, memeIndex={memeIndex}, playerId={playerId}, ownerClientId={ownerClientId}", true, nameof(MemeNetworkManager));
        string prefabPath = $"{memePath}{playerId}/Meme{memeIndex + 1}";
        var prefab = Resources.Load<GameObject>(prefabPath);

        if (prefab == null)
        {
            logger.Log($"Prefab not found at {prefabPath}", true, nameof(MemeNetworkManager));
            return;
        }

        // Load state before instantiating
        MemeObjectHandler.MemeObjectState state = GetMemeStateServer(playerId, memeIndex);
        Vector3 spawnPos = menuPosition;
        Quaternion spawnRot = menuRotation;
        Vector3 spawnScale = prefab.transform.localScale;
        if (state != null)
        {
            logger.Log($"[Server] Applying saved state to meme before spawn: pos={state.localPosition}, rot={state.localRotation}, scale={state.scale}", true, nameof(MemeNetworkManager));
            if (mRUK != null && mRUK.GetCurrentRoom() != null && mRUK.GetCurrentRoom().FloorAnchor != null)
            {
                var anchor = mRUK.GetCurrentRoom().FloorAnchor.transform;
                spawnPos = anchor.TransformPoint(state.localPosition);
                spawnRot = anchor.rotation * state.localRotation;
            }
            else
            {
                spawnPos = state.localPosition;
                spawnRot = state.localRotation;
            }
            spawnScale = state.scale;
        }

        var obj = Instantiate(prefab, spawnPos, spawnRot);
        obj.transform.localScale = spawnScale;

        if (!obj.TryGetComponent<NetworkObject>(out var netObj))
        {
            logger.Log($"NetworkObject component missing on prefab {prefabPath}", true, nameof(MemeNetworkManager));
            return;
        }

        if (!obj.TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var netTransform))
        {
            logger.Log($"NetworkTransform component missing on prefab {prefabPath}, adding it.", true, nameof(MemeNetworkManager));
            return;
        }

        if (!obj.TryGetComponent<MemeObjectHandler>(out var handlerNew))
            handlerNew = obj.AddComponent<MemeObjectHandler>();

        handlerNew.SetLogger(logger);
        handlerNew.Init(memeIndex, $"Meme{memeIndex + 1}", playerId);

        if (mRUK != null && mRUK.GetCurrentRoom() != null && mRUK.GetCurrentRoom().FloorAnchor != null)
            handlerNew.anchorTransform = mRUK.GetCurrentRoom().FloorAnchor.transform;
        else
            logger.Log("mRUK or FloorAnchor is null on server!", true, nameof(MemeNetworkManager));

        if (ownerClientId.HasValue)
            netObj.SpawnWithOwnership(ownerClientId.Value, true);
        else
            netObj.Spawn(true);

        logger.Log($"Requested meme state load for playerId={playerId}, memeIndex={memeIndex}.", true, nameof(MemeNetworkManager));

        if (ownerClientId.HasValue)
        {
            playerMemes[ownerClientId.Value] = obj;
            playerMemeIndices[ownerClientId.Value] = memeIndex;
        }
        else if (!ownerClientId.HasValue && NetworkManager.Singleton.IsServer)
        {
            playerMemes[NetworkManager.Singleton.LocalClientId] = obj;
            playerMemeIndices[NetworkManager.Singleton.LocalClientId] = memeIndex;
        }

        // Only call RequestLoadState on the owner client (not on the server)
        // The client will call RequestLoadState in OnNetworkSpawn
    }

    public void RequestCloseCurrentMeme()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            CloseCurrentMemeInternal(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            CloseCurrentMemeServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseCurrentMemeServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        CloseCurrentMemeInternal(senderClientId);
    }

    private void CloseCurrentMemeInternal(ulong clientId)
    {
        if (playerMemes.TryGetValue(clientId, out var currentPlayerMeme) && currentPlayerMeme != null)
        {
            if (currentPlayerMeme.TryGetComponent<MemeObjectHandler>(out var handler))
            {
                handler.SaveState();
            }
            if (currentPlayerMeme.TryGetComponent<NetworkObject>(out var netObj))
            {
                if (netObj.IsOwner || NetworkManager.Singleton.IsServer)
                    netObj.Despawn(true);
            }
            else
            {
                Destroy(currentPlayerMeme);
            }
            playerMemes[clientId] = null;
            playerMemeIndices[clientId] = -1;
        }
    }

    public void SaveMemeStateServer(int playerId, int memeIndex, MemeObjectHandler.MemeObjectState state)
    {
        logger.Log($"[Server] Saved meme state for playerId={playerId}, memeIndex={memeIndex}", true, nameof(MemeNetworkManager));
        // Persist to disk only
        string dir = Path.Combine(Application.persistentDataPath, "memeObjectStates", playerId.ToString());
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"meme_{memeIndex}.json");
        string json = JsonUtility.ToJson(state);
        File.WriteAllText(filePath, json);
    }

    public MemeObjectHandler.MemeObjectState GetMemeStateServer(int playerId, int memeIndex)
    {
        // Always load from disk
        string filePath = Path.Combine(Application.persistentDataPath, "memeObjectStates", playerId.ToString(), $"meme_{memeIndex}.json");
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var loadedState = JsonUtility.FromJson<MemeObjectHandler.MemeObjectState>(json);
            logger.Log($"[Server] Loaded meme state for playerId={playerId}, memeIndex={memeIndex} (from disk)", true, nameof(MemeNetworkManager));
            return loadedState;
        }
        logger.Log($"[Server] No meme state found for playerId={playerId}, memeIndex={memeIndex}", true, nameof(MemeNetworkManager));
        return null;
    }
}
