using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Meta.XR.MRUtilityKit;
using System.IO;

public class MemeNetworkManager : NetworkBehaviour
{
    public static MemeNetworkManager Instance { get; private set; }

    [SerializeField] private MRUK mRUK;
    public Logger logger;
    private readonly Dictionary<ulong, GameObject> playerMemes = new();
    private readonly Dictionary<ulong, int> playerMemeIndices = new();

    // --- Tagged anchor meme tracking ---
    private readonly List<MemeObjectHandler> taggedAnchorMemes = new();
    public void RegisterTaggedAnchorMeme(MemeObjectHandler handler)
    {
        if (!taggedAnchorMemes.Contains(handler))
            taggedAnchorMemes.Add(handler);
    }
    public void UnregisterTaggedAnchorMeme(MemeObjectHandler handler)
    {
        taggedAnchorMemes.Remove(handler);
    }

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
        logger.LogDebug($"RequestSpawnMeme called. memePath={memePath}, memeIndex={memeIndex}, playerId={playerId}, isServer={NetworkManager.Singleton.IsServer}", nameof(MemeNetworkManager));
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (NetworkManager.Singleton.IsServer)
        {
            logger.LogDebug($"Calling SpawnMeme directly on server.", nameof(MemeNetworkManager));
            SpawnMeme(memePath, memeIndex, playerId, menuPosition, menuRotation, localClientId);
        }
        else
        {
            logger.LogDebug($"Calling SpawnMemeServerRpc from client.", nameof(MemeNetworkManager));
            SpawnMemeServerRpc(memePath, memeIndex, playerId, menuPosition, menuRotation);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnMemeServerRpc(string memePath, int memeIndex, int playerId, Vector3 menuPosition, Quaternion menuRotation, ServerRpcParams rpcParams = default)
    {
        logger.LogDebug($"SpawnMemeServerRpc received from clientId={rpcParams.Receive.SenderClientId}", nameof(MemeNetworkManager));

        ulong ownerClientId = rpcParams.Receive.SenderClientId;
        SpawnMeme(memePath, memeIndex, playerId, menuPosition, menuRotation, ownerClientId);
    }

    private void RemovePreviousMeme(ulong clientId)
    {
        if (!playerMemes.TryGetValue(clientId, out var currentPlayerMeme) || currentPlayerMeme == null)
        {
            logger.LogDebug($"[RemovePreviousMeme] No previous meme for clientId={clientId}", nameof(MemeNetworkManager));
            return;
        }
        logger.LogDebug($"[RemovePreviousMeme] Removing meme for clientId={clientId}, memeName={currentPlayerMeme.name}, instanceID={currentPlayerMeme.GetInstanceID()}", nameof(MemeNetworkManager));
        if (currentPlayerMeme.TryGetComponent<MemeObjectHandler>(out var handler))
        {
            logger.LogDebug($"[RemovePreviousMeme] Calling SaveState on meme instanceID={currentPlayerMeme.GetInstanceID()}", nameof(MemeNetworkManager));
            handler.SaveState();
            
            // Unregister from tagged anchor tracking if this meme was attached to a tagged anchor
            if (handler.placementMode != MemeObjectHandler.PlacementMode.World)
            {
                UnregisterTaggedAnchorMeme(handler);
            }
        }
        if (currentPlayerMeme.TryGetComponent<NetworkObject>(out var netObjToRemove))
        {
            logger.LogDebug($"[RemovePreviousMeme] Despawning NetworkObject instanceID={currentPlayerMeme.GetInstanceID()}, IsOwner={netObjToRemove.IsOwner}, IsServer={NetworkManager.Singleton.IsServer}", nameof(MemeNetworkManager));
            if (netObjToRemove.IsOwner || NetworkManager.Singleton.IsServer)
                netObjToRemove.Despawn(true);
        }
        else
        {
            logger.LogDebug($"[RemovePreviousMeme] Destroying meme GameObject instanceID={currentPlayerMeme.GetInstanceID()} (no NetworkObject)", nameof(MemeNetworkManager));
            Destroy(currentPlayerMeme);
        }
        playerMemes[clientId] = null;
        playerMemeIndices[clientId] = -1;
        logger.LogDebug($"[RemovePreviousMeme] playerMemes[{clientId}] set to null", nameof(MemeNetworkManager));
    }

    private void RegisterMeme(ulong clientId, GameObject obj, int memeIndex)
    {
        logger.LogDebug($"[RegisterMeme] Registering meme for clientId={clientId}, memeName={obj.name}, instanceID={obj.GetInstanceID()}, memeIndex={memeIndex}", nameof(MemeNetworkManager));
        playerMemes[clientId] = obj;
        playerMemeIndices[clientId] = memeIndex;
        logger.LogDebug($"[RegisterMeme] playerMemes[{clientId}] now instanceID={obj.GetInstanceID()}", nameof(MemeNetworkManager));
    }

    private void SpawnMeme(string memePath, int memeIndex, int playerId, Vector3 menuPosition, Quaternion menuRotation, ulong? ownerClientId = null)
    {
        logger.LogDebug($"SpawnMeme called with memePath={memePath}, memeIndex={memeIndex}, playerId={playerId}, ownerClientId={ownerClientId}", nameof(MemeNetworkManager));
        if (ownerClientId.HasValue)
            RemovePreviousMeme(ownerClientId.Value);

        string prefabPath = $"{memePath}{playerId}/Meme{memeIndex + 1}";
        var prefab = Resources.Load<GameObject>(prefabPath);

        if (prefab == null)
        {
            logger.LogError($"Prefab not found at {prefabPath}", nameof(MemeNetworkManager));
            return;
        }

        MemeObjectHandler.PlacementMode placementMode = MemeObjectHandler.GetPlacementMode(prefab);
        bool isWorldMode = placementMode == MemeObjectHandler.PlacementMode.World;

        // Track if state was loaded from disk
        bool loadedFromDisk = false;
        MemeObjectHandler.MemeObjectState? state = null;
        Transform anchor = (mRUK != null && mRUK.GetCurrentRoom() != null && mRUK.GetCurrentRoom().FloorAnchor != null)
            ? mRUK.GetCurrentRoom().FloorAnchor.transform : null;
        if (isWorldMode)
        {
            var loadedState = GetMemeStateServer(playerId, memeIndex);
            if (loadedState.HasValue)
            {
                state = loadedState;
                loadedFromDisk = true;
            }
            else
            {
                // Create a default state for world mode if none exists, using menu position/rotation
                state = new MemeObjectHandler.MemeObjectState
                {
                    prefabName = $"Meme{memeIndex + 1}",
                    localPosition = anchor ? anchor.InverseTransformPoint(menuPosition) : menuPosition,
                    localRotation = anchor ? Quaternion.Inverse(anchor.rotation) * menuRotation : menuRotation,
                    scale = prefab.transform.localScale,
                    memeIndex = memeIndex,
                    placementMode = placementMode,
                    playerId = playerId
                };
            }
        }
        // For face mode, create a default state if none exists
        else
        {
            // For tagged anchor modes, create a default state if none exists
            state = new MemeObjectHandler.MemeObjectState
            {
                prefabName = $"Meme{memeIndex + 1}",
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity,
                scale = prefab.transform.localScale,
                memeIndex = memeIndex,
                placementMode = placementMode,
                playerId = playerId // Ensure playerId is set
            };
        }

        Vector3 spawnPos = menuPosition;
        Quaternion spawnRot = menuRotation;
        Vector3 spawnScale = prefab.transform.localScale;

        if (state.HasValue && isWorldMode)
        {
            if (loadedFromDisk && anchor)
            {
                spawnPos = anchor.TransformPoint(state.Value.localPosition);
                spawnRot = anchor.rotation * state.Value.localRotation;
            }
            else
            {
                spawnPos = menuPosition;
                spawnRot = menuRotation;
            }
            spawnScale = state.Value.scale;
        }
        // For tagged anchor modes, spawn at menu position/rotation, scale from state
        if (state.HasValue && !isWorldMode)
        {
            spawnScale = state.Value.scale;
        }

        var obj = Instantiate(prefab, spawnPos, spawnRot);
        obj.transform.localScale = spawnScale;
        logger.LogDebug($"[SpawnMeme] Instantiated meme prefab: name={obj.name}, instanceID={obj.GetInstanceID()}, position={spawnPos}, rotation={spawnRot}, scale={spawnScale}", nameof(MemeNetworkManager));

        if (!obj.TryGetComponent<NetworkObject>(out var netObj))
        {
            logger.LogError($"NetworkObject component missing on prefab {prefabPath}", nameof(MemeNetworkManager));
            return;
        }

        if (!obj.TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var netTransform))
        {
            logger.LogError($"NetworkTransform component missing on prefab {prefabPath}, adding it.", nameof(MemeNetworkManager));
            return;
        }

        if (!obj.TryGetComponent<MemeObjectHandler>(out var handlerNew))
        {
            logger.LogError($"MemeObjectHandler component missing on prefab {prefabPath}", nameof(MemeNetworkManager));
            return;
        }
        // Only set anchorTransform for world mode BEFORE initializing (server/host never tries to parent to tagged anchors)
        if (isWorldMode && mRUK != null && mRUK.GetCurrentRoom() != null && mRUK.GetCurrentRoom().FloorAnchor != null)
            handlerNew.anchorTransform = mRUK.GetCurrentRoom().FloorAnchor.transform;

        if (state.HasValue)
        {
            // Set the networked state BEFORE spawning the object
            handlerNew.State = state.Value;
            if (NetworkManager.Singleton.IsServer)
            {
                handlerNew.SetLogger(logger);
                handlerNew.NetworkedState = state.Value.ToNetworked();
            }
            handlerNew.Initialize(state.Value, logger);
        }

        if (ownerClientId.HasValue)
        {
            logger.LogDebug($"[SpawnMeme] Spawning meme with ownership for clientId={ownerClientId.Value}, instanceID={obj.GetInstanceID()}", nameof(MemeNetworkManager));
            netObj.SpawnWithOwnership(ownerClientId.Value);
            RegisterMeme(ownerClientId.Value, obj, memeIndex);
        }
        else if (!ownerClientId.HasValue && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
            RegisterMeme(NetworkManager.Singleton.LocalClientId, obj, memeIndex);
        }
        logger.LogDebug($"[SpawnMeme] Finished for memeName={obj.name}, instanceID={obj.GetInstanceID()}", nameof(MemeNetworkManager));
    }

    public void RequestCloseCurrentMeme()
    {
        logger.LogDebug($"[RequestCloseCurrentMeme] Called. IsServer={NetworkManager.Singleton.IsServer}", nameof(MemeNetworkManager));
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
        logger.LogDebug($"[CloseCurrentMemeServerRpc] Called for clientId={senderClientId}", nameof(MemeNetworkManager));
        CloseCurrentMemeInternal(senderClientId);
    }

    private void CloseCurrentMemeInternal(ulong clientId)
    {
        logger.LogDebug($"[CloseCurrentMemeInternal] Called for clientId={clientId}", nameof(MemeNetworkManager));
        RemovePreviousMeme(clientId);
    }

    public void SaveMemeStateServer(int playerId, int memeIndex, MemeObjectHandler.MemeObjectState state)
    {
        logger.LogDebug($"[Server] Saved meme state for playerId={playerId}, memeIndex={memeIndex}", nameof(MemeNetworkManager));
        string dir = Path.Combine(Application.persistentDataPath, "memeObjectStates", playerId.ToString());
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"meme_{memeIndex}.json");
        string json = JsonUtility.ToJson(state);
        File.WriteAllText(filePath, json);
    }

    public MemeObjectHandler.MemeObjectState? GetMemeStateServer(int playerId, int memeIndex)
    {
        string filePath = Path.Combine(Application.persistentDataPath, "memeObjectStates", playerId.ToString(), $"meme_{memeIndex}.json");
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var loadedState = JsonUtility.FromJson<MemeObjectHandler.MemeObjectState>(json);
            logger.LogDebug($"[Server] Loaded meme state for playerId={playerId}, memeIndex={memeIndex} (from disk)", nameof(MemeNetworkManager));
            return loadedState;
        }
        logger.LogDebug($"[Server] No meme state found for playerId={playerId}, memeIndex={memeIndex}", nameof(MemeNetworkManager));
        return null;
    }

    // --- Quick Selection Persistence ---
    [Serializable]
    public struct QuickSelectionData
    {
        public string[] selectedMemeNames; // Store prefab names instead of references
        public int playerId;
    }

    // Network serializable wrapper for RPC calls
    [Serializable]
    public struct NetworkQuickSelectionData : INetworkSerializable
    {
        public int playerId;
        public string[] selectedMemeNames;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerId);
            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                selectedMemeNames = new string[count];
                for (int i = 0; i < count; i++)
                {
                    serializer.SerializeValue(ref selectedMemeNames[i]);
                }
            }
            else
            {
                int count = selectedMemeNames?.Length ?? 0;
                serializer.SerializeValue(ref count);
                for (int i = 0; i < count; i++)
                {
                    serializer.SerializeValue(ref selectedMemeNames[i]);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SaveQuickSelectionServerRpc(NetworkQuickSelectionData networkData, ServerRpcParams rpcParams = default)
    {
        logger.LogDebug($"SaveQuickSelectionServerRpc received for player {networkData.playerId} from client {rpcParams.Receive.SenderClientId}", nameof(MemeNetworkManager));
        SaveQuickSelectionServer(networkData.playerId, networkData.selectedMemeNames);
    }

    [ServerRpc(RequireOwnership = false)]
    public void LoadQuickSelectionServerRpc(int playerId, ServerRpcParams rpcParams = default)
    {
        logger.LogDebug($"LoadQuickSelectionServerRpc received for player {playerId} from client {rpcParams.Receive.SenderClientId}", nameof(MemeNetworkManager));
        var data = GetQuickSelectionServer(playerId);
        var networkData = data.HasValue 
            ? new NetworkQuickSelectionData 
            { 
                playerId = data.Value.playerId, 
                selectedMemeNames = data.Value.selectedMemeNames 
            }
            : new NetworkQuickSelectionData { playerId = -1, selectedMemeNames = new string[0] };
        logger.LogDebug($"Sending quick selection response to client {rpcParams.Receive.SenderClientId}, found data: {data.HasValue}", nameof(MemeNetworkManager));
        LoadQuickSelectionClientRpc(networkData, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    public void LoadQuickSelectionClientRpc(NetworkQuickSelectionData networkData, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            logger.LogDebug($"LoadQuickSelectionClientRpc received for player {networkData.playerId}, target client: {clientId}, local client: {NetworkManager.Singleton.LocalClientId}", nameof(MemeNetworkManager));
            
            // Find MemeMenu and notify it of the loaded data
            var memeMenu = FindFirstObjectByType<MemeMenu>();
            if (memeMenu != null)
            {
                if (networkData.playerId != -1)
                {
                    // Convert NetworkQuickSelectionData back to QuickSelectionData
                    var data = new QuickSelectionData
                    {
                        playerId = networkData.playerId,
                        selectedMemeNames = networkData.selectedMemeNames
                    };
                    memeMenu.OnQuickSelectionDataReceived(data);
                }
                else
                {
                    logger.LogDebug("No quick selection data found (sentinel received).", nameof(MemeNetworkManager));
                    memeMenu.OnQuickSelectionDataReceived(null);
                }
            }
            else
            {
                logger.LogWarning("MemeMenu not found to deliver quick selection data", nameof(MemeNetworkManager));
            }
        }
    }

    public void SaveQuickSelectionServer(int playerId, string[] selectedMemeNames)
    {
        logger.LogDebug($"[Server] Saving quick selection for playerId={playerId}", nameof(MemeNetworkManager));
        var data = new QuickSelectionData
        {
            playerId = playerId,
            selectedMemeNames = selectedMemeNames ?? new string[0]
        };
        
        string dir = Path.Combine(Application.persistentDataPath, "quickSelections");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"player_{playerId}_quick_selection.json");
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(filePath, json);
        logger.LogDebug($"[Server] Quick selection saved for playerId={playerId} with {selectedMemeNames?.Length ?? 0} memes", nameof(MemeNetworkManager));
    }

    public QuickSelectionData? GetQuickSelectionServer(int playerId)
    {
        string filePath = Path.Combine(Application.persistentDataPath, "quickSelections", $"player_{playerId}_quick_selection.json");
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var loadedData = JsonUtility.FromJson<QuickSelectionData>(json);
            logger.LogDebug($"[Server] Loaded quick selection for playerId={playerId} with {loadedData.selectedMemeNames?.Length ?? 0} memes", nameof(MemeNetworkManager));
            return loadedData;
        }
        logger.LogDebug($"[Server] No quick selection found for playerId={playerId}", nameof(MemeNetworkManager));
        return null;
    }
}
