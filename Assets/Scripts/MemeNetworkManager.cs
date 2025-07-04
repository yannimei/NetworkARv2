using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Meta.XR.MRUtilityKit;

public class MemeNetworkManager : NetworkBehaviour
{
    public static MemeNetworkManager Instance { get; private set; }

    [SerializeField] private MRUK mRUK;
    [SerializeField] private Logger logger;
    private readonly Dictionary<ulong, GameObject> playerMemes = new();
    private readonly Dictionary<ulong, int> playerMemeIndices = new();

    // Server-side meme state storage: playerId -> memeIndex -> state
    private readonly Dictionary<int, Dictionary<int, MemeObjectHandler.MemeObjectState>> memeStates = new();

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

        var obj = Instantiate(prefab, menuPosition, Quaternion.identity);

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

        if (ownerClientId.HasValue)
            netObj.SpawnWithOwnership(ownerClientId.Value, true);
        else
            netObj.Spawn(true);

        if (!obj.TryGetComponent<MemeObjectHandler>(out var handlerNew))
            handlerNew = obj.AddComponent<MemeObjectHandler>();

        handlerNew.Init(memeIndex, $"Meme{memeIndex + 1}", playerId);

        if (mRUK != null && mRUK.GetCurrentRoom() != null && mRUK.GetCurrentRoom().FloorAnchor != null)
            handlerNew.anchorTransform = mRUK.GetCurrentRoom().FloorAnchor.transform;
        else
            logger.Log("mRUK or FloorAnchor is null on server!", true, nameof(MemeNetworkManager));
        
        handlerNew.RequestLoadState();
        logger.Log($"Requested meme state load for playerId={playerId}, memeIndex={memeIndex}.", true, nameof(MemeNetworkManager));

        if (obj.TryGetComponent<MemePlacement>(out var placement))
        {
            placement.SetLogger(logger);
            if (placement.placementMode == MemePlacement.PlacementMode.Face)
            {
                logger.Log($"Meme {memeIndex} set to follow face position.", true, nameof(MemeNetworkManager));
            }
        }
        else
        {
            logger.Log("MemePlacement component missing on spawned meme object.", true, nameof(MemeNetworkManager));
        }

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
        if (!memeStates.ContainsKey(playerId))
            memeStates[playerId] = new Dictionary<int, MemeObjectHandler.MemeObjectState>();
        memeStates[playerId][memeIndex] = state;
        logger.Log($"[Server] Saved meme state for playerId={playerId}, memeIndex={memeIndex}", true, nameof(MemeNetworkManager));
    }

    public MemeObjectHandler.MemeObjectState GetMemeStateServer(int playerId, int memeIndex)
    {
        if (memeStates.TryGetValue(playerId, out var playerStates) && playerStates.TryGetValue(memeIndex, out var state))
        {
            logger.Log($"[Server] Loaded meme state for playerId={playerId}, memeIndex={memeIndex}", true, nameof(MemeNetworkManager));
            return state;
        }
        logger.Log($"[Server] No meme state found for playerId={playerId}, memeIndex={memeIndex}", true, nameof(MemeNetworkManager));
        return null;
    }
}
