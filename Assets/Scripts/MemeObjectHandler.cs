using UnityEngine;
using System;
using Unity.Netcode;

public class MemeObjectHandler : NetworkBehaviour
{
    public int memeIndex;
    public string prefabName;
    public int playerId;
    public Transform anchorTransform;
    private Logger logger;

    [Serializable]
    public class MemeObjectState : INetworkSerializable
    {
        public string prefabName;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 scale;
        public int memeIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref prefabName);
            serializer.SerializeValue(ref localPosition);
            serializer.SerializeValue(ref localRotation);
            serializer.SerializeValue(ref scale);
            serializer.SerializeValue(ref memeIndex);
        }
    }

    public void Init(int index, string prefab, int playerId)
    {
        memeIndex = index;
        this.prefabName = prefab;
        this.playerId = playerId;
    }

    public void SaveState()
    {
        // Only save state for world mode
        if (TryGetComponent<MemePlacement>(out var placement) && placement.placementMode != MemePlacement.PlacementMode.World)
        {
            logger.Log("[MemeObjectHandler] Skipping SaveState for non-world mode.");
            return;
        }
        MemeObjectState state = CreateCurrentState();
        SaveStateInternal(state);
    }

    private MemeObjectState CreateCurrentState()
    {
        return new MemeObjectState
        {
            prefabName = prefabName,
            localPosition = anchorTransform ? anchorTransform.InverseTransformPoint(transform.position) : transform.position,
            localRotation = anchorTransform ? Quaternion.Inverse(anchorTransform.rotation) * transform.rotation : transform.rotation,
            scale = transform.localScale,
            memeIndex = memeIndex
        };
    }

    private void SaveStateInternal(MemeObjectState state)
    {
        if (IsServer)
        {
            MemeNetworkManager.Instance.SaveMemeStateServer(playerId, memeIndex, state);
        }
        else
        {
            SaveStateServerRpc(state);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SaveStateServerRpc(MemeObjectState state, ServerRpcParams rpcParams = default)
    {
        MemeNetworkManager.Instance.SaveMemeStateServer(playerId, memeIndex, state);
    }

    public void RequestLoadState()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            var state = MemeNetworkManager.Instance.GetMemeStateServer(playerId, memeIndex);
            logger.Log($"[MemeObjectHandler] Server/Host loading state for playerId={playerId}, memeIndex={memeIndex}: {(state != null ? "found" : "not found")}");
            ApplyState(state);
        }
        else
        {
            logger.Log($"[MemeObjectHandler] Client requesting state load for playerId={playerId}, memeIndex={memeIndex}");
            LoadStateServerRpc(playerId, memeIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadStateServerRpc(int playerId, int memeIndex, ServerRpcParams rpcParams = default)
    {
        var state = MemeNetworkManager.Instance.GetMemeStateServer(playerId, memeIndex);
        LoadStateClientRpc(state, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void LoadStateClientRpc(MemeObjectState state, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            ApplyState(state);
        }
    }

    private void ApplyState(MemeObjectState state)
    {
        logger.Log($"[MemeObjectHandler] ApplyState called for playerId={playerId}, memeIndex={memeIndex}, state null? {state == null}");
        // Only apply state for world mode
        if (TryGetComponent<MemePlacement>(out var placement) && placement.placementMode != MemePlacement.PlacementMode.World)
        {
            logger.Log("[MemeObjectHandler] Skipping state apply for non-world mode.");
            return;
        }
        if (state != null)
        {
            logger.Log($"[MemeObjectHandler] Applying loaded state: pos={state.localPosition}, rot={state.localRotation}, scale={state.scale}");
            if (anchorTransform)
            {
                transform.SetPositionAndRotation(anchorTransform.TransformPoint(state.localPosition), anchorTransform.rotation * state.localRotation);
            }
            else
            {
                transform.SetPositionAndRotation(state.localPosition, state.localRotation);
            }
            transform.localScale = state.scale;
            // Force sync for NetworkTransform/ClientNetworkTransform
            var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null && !IsServer)
            {
                logger.Log($"[MemeObjectHandler] Teleporting NetworkTransform to new state.");
                netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
            }
        }
        else
        {
            logger.Log($"[MemeObjectHandler] No state found.");
        }
    }

    public void SetLogger(Logger logger)
    {
        this.logger = logger;
    }

    private void OnMouseUpAsButton()
    {
        SaveState();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        logger = MemeNetworkManager.Instance != null ? MemeNetworkManager.Instance.logger : null;
        if (!IsServer)
        {
            RequestLoadState();
        }
        // Attach to face anchor if in Face mode
        if (TryGetComponent<MemePlacement>(out var placement) && placement.placementMode == MemePlacement.PlacementMode.Face)
        {
            var faceAnchor = placement.AttachToFaceAnchor();
            if (faceAnchor != null)
            {
                anchorTransform = faceAnchor;
            }
            else
            {
                logger.Log("[MemeObjectHandler] Face anchor not found on spawn.");
            }
        }
    }
}
