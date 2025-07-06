using UnityEngine;
using System;
using Unity.Netcode;
using Unity.Collections;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Handles meme object state, placement, and network synchronization.
/// </summary>
public partial class MemeObjectHandler : NetworkBehaviour
{
    // --- Placement Mode ---
    public enum PlacementMode { World, Face, Background }
    public PlacementMode placementMode = PlacementMode.World;

    // --- State ---
    public MemeObjectState State;
    public Transform anchorTransform;

    // --- Logger ---
    private Logger logger;

    // --- Networking ---
    private readonly NetworkVariable<MemeObjectState.Networked> networkedState = new(
        new MemeObjectState.Networked { memeIndex = -1, playerId = -1, prefabName = "", placementMode = 0 }
    );

    // Expose property for external set/get (for spawn-time state sync)
    public MemeObjectState.Networked NetworkedState
    {
        get => networkedState.Value;
        set => networkedState.Value = value;
    }

    // --- Unity Lifecycle ---
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        logger = MemeNetworkManager.Instance != null ? MemeNetworkManager.Instance.logger : null;
        logger.LogDebug($"OnNetworkSpawn called. IsServer={IsServer} IsOwner={IsOwner} LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : null)} NetworkObjectId={NetworkObjectId}", nameof(MemeObjectHandler));
        networkedState.OnValueChanged += (oldVal, newVal) => ApplyNetworkedState(newVal);

        ApplyNetworkedState(networkedState.Value);

        logger.LogDebug($"After OnNetworkSpawn, world position: {transform.position}", nameof(MemeObjectHandler));
    }

    public void Initialize(MemeObjectState state, Logger logger)
    {
        State = state;
        this.logger = logger;
        // Sync the field with the state
        placementMode = state.placementMode;
        logger.LogDebug($"Initialize called - placementMode={placementMode}, scale={state.scale}", nameof(MemeObjectHandler));

        if (IsServer)
        {
            networkedState.Value = state.ToNetworked();
        }
        ApplyState(state);
    }

    private void ApplyNetworkedState(MemeObjectState.Networked netState)
    {
        State.memeIndex = netState.memeIndex;
        State.prefabName = netState.prefabName.ToString();
        State.playerId = netState.playerId;
        State.placementMode = (PlacementMode)netState.placementMode;
        // Sync the field with the state
        placementMode = State.placementMode;
        logger.LogDebug($"Applied networked state: memeIndex={State.memeIndex}, prefabName={State.prefabName}, playerId={State.playerId}, placementMode={State.placementMode}", nameof(MemeObjectHandler));
        ApplyState(State);
    }

    private void ApplyState(MemeObjectState state)
    {
        logger.LogDebug($"ApplyState called for playerId={state.playerId}, memeIndex={state.memeIndex}, placementMode={state.placementMode}", nameof(MemeObjectHandler));

        // Check if placement mode is changing from tagged anchor to world
        bool wasTaggedAnchor = placementMode != PlacementMode.World;
        bool isNowWorld = state.placementMode == PlacementMode.World;
        
        // Sync the field with the state
        placementMode = state.placementMode;

        // Unregister if changing from tagged anchor to world mode
        if (wasTaggedAnchor && isNowWorld && MemeNetworkManager.Instance != null)
        {
            MemeNetworkManager.Instance.UnregisterTaggedAnchorMeme(this);
        }

        if (state.placementMode == PlacementMode.World)
        {
            // Only apply position/rotation if we have an anchor transform
            // Otherwise, preserve the current world position (likely set by SpawnMeme)
            if (anchorTransform)
            {
                transform.SetPositionAndRotation(anchorTransform.TransformPoint(state.localPosition), anchorTransform.rotation * state.localRotation);
                logger.LogDebug($"Applied world position with anchor: {transform.position}", nameof(MemeObjectHandler));
            }
            else
            {
                logger.LogDebug($"No anchor transform available, preserving current world position: {transform.position}", nameof(MemeObjectHandler));
            }
        }
        else if (state.placementMode != PlacementMode.World)
        {
            AttachToTaggedAnchorLocal();
        }

        // Log final transform values for debugging
        logger.LogDebug($"Final transform - Position: {transform.position}, Rotation: {transform.rotation}, Scale: {transform.localScale}", nameof(MemeObjectHandler));

        // Force sync for NetworkTransform/ClientNetworkTransform
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null && netTransform.CanCommitToTransform)
        {
            netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }
    }

    // --- Placement Logic ---
    private void AttachToTaggedAnchorLocal()
    {
        logger.LogDebug($"AttachToTaggedAnchorLocal called for placement mode: {placementMode}", nameof(MemeObjectHandler));
        var taggedAnchor = AttachToTaggedAnchor();
        if (taggedAnchor != null)
        {
            anchorTransform = taggedAnchor;
            logger.LogDebug($"Attached to local tagged anchor with tag: {placementMode}", nameof(MemeObjectHandler));
            MemeNetworkManager.Instance?.RegisterTaggedAnchorMeme(this);
        }
        else
        {
            logger.LogError($"Tagged anchor not found on client for tag: {placementMode}", nameof(MemeObjectHandler));
        }
    }

    public void AttachToAnchor(Transform anchorTransform)
    {
        if (anchorTransform != null)
        {
            transform.SetParent(anchorTransform, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }

    public void DetachFromAnchor()
    {
        transform.SetParent(null, false);
        
        // Unregister from tagged anchor tracking when detaching
        if (placementMode != PlacementMode.World && MemeNetworkManager.Instance != null)
        {
            MemeNetworkManager.Instance.UnregisterTaggedAnchorMeme(this);
        }
    }

    public Transform AttachToTaggedAnchor()
    {
        // For World mode, don't look for tagged anchors
        if (placementMode == PlacementMode.World)
        {
            logger.LogDebug("AttachToTaggedAnchor called for World mode - returning null", nameof(MemeObjectHandler));
            return null;
        }

        // Find the anchor based on the placement mode name as tag
        string tagName = placementMode.ToString();
        var taggedAnchor = GameObject.FindGameObjectWithTag(tagName);
        
        if (taggedAnchor != null)
        {
            logger.LogDebug($"Found tagged anchor for tag: {tagName}", nameof(MemeObjectHandler));
            AttachToAnchor(taggedAnchor.transform);
        }
        else
        {
            logger.LogError($"No GameObject found with tag: {tagName}", nameof(MemeObjectHandler));
        }
        
        return taggedAnchor != null ? taggedAnchor.transform : null;
    }

    // Optionally, add a helper to get placement mode from prefab or state
    public static PlacementMode GetPlacementMode(GameObject obj)
    {
        var handler = obj.GetComponent<MemeObjectHandler>();
        return handler != null ? handler.placementMode : PlacementMode.World;
    }

    private void Update()
    {
        // If not in world mode, follow the tagged anchor every frame
        if (placementMode != PlacementMode.World && anchorTransform != null)
        {
            transform.SetPositionAndRotation(anchorTransform.position, anchorTransform.rotation);
        }
    }

    public void SetLogger(Logger logger)
    {
        this.logger = logger;
    }

    // --- Save/Load Logic ---
    public void SaveState()
    {
        if (State.placementMode != PlacementMode.World)
        {
            logger.LogDebug("Skipping SaveState for non-world mode.", nameof(MemeObjectHandler));
            return;
        }
        MemeObjectState state = State;
        state.localPosition = anchorTransform ? anchorTransform.InverseTransformPoint(transform.position) : transform.position;
        state.localRotation = anchorTransform ? Quaternion.Inverse(anchorTransform.rotation) * transform.rotation : transform.rotation;
        state.scale = transform.localScale;
        SaveStateInternal(state);
    }

    private void SaveStateInternal(MemeObjectState state)
    {
        if (IsServer)
        {
            MemeNetworkManager.Instance.SaveMemeStateServer(state.playerId, state.memeIndex, state);
        }
        else
        {
            SaveStateServerRpc(state);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SaveStateServerRpc(MemeObjectState state, ServerRpcParams rpcParams = default)
    {
        MemeNetworkManager.Instance.SaveMemeStateServer(state.playerId, state.memeIndex, state);
    }

    public void RequestLoadState()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            MemeObjectState? state = MemeNetworkManager.Instance.GetMemeStateServer(State.playerId, State.memeIndex);
            bool found = state.HasValue;
            logger.LogDebug($"Server/Host loading state for playerId={State.playerId}, memeIndex={State.memeIndex}: {(found ? "found" : "not found")}", nameof(MemeObjectHandler));
            if (found) ApplyState(state.Value);
        }
        else
        {
            logger.LogDebug($"Client requesting state load for playerId={State.playerId}, memeIndex={State.memeIndex}", nameof(MemeObjectHandler));
            LoadStateServerRpc(State.playerId, State.memeIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadStateServerRpc(int playerId, int memeIndex, ServerRpcParams rpcParams = default)
    {
        MemeObjectState? state = MemeNetworkManager.Instance.GetMemeStateServer(playerId, memeIndex);
        // Always send a non-nullable struct. Use memeIndex = -1 as sentinel for 'not found'.
        LoadStateClientRpc(state ?? new MemeObjectState { memeIndex = -1 }, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void LoadStateClientRpc(MemeObjectState state, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            if (state.memeIndex != -1)
                ApplyState(state);
            else
                logger.LogDebug("No state found (sentinel received).", nameof(MemeObjectHandler));
        }
    }

    private void OnMouseUpAsButton()
    {
        SaveState();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        logger.LogDebug($"OnDestroy called for playerId={State.playerId}, memeIndex={State.memeIndex}, name={gameObject.name}", nameof(MemeObjectHandler));
        
        // Unregister from tagged anchor tracking if this meme was attached to a tagged anchor
        if (placementMode != PlacementMode.World && MemeNetworkManager.Instance != null)
        {
            MemeNetworkManager.Instance.UnregisterTaggedAnchorMeme(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        logger.LogDebug($"OnNetworkDespawn called for playerId={State.playerId}, memeIndex={State.memeIndex}, name={gameObject.name}", nameof(MemeObjectHandler));
        
        // Unregister from tagged anchor tracking if this meme was attached to a tagged anchor
        if (placementMode != PlacementMode.World && MemeNetworkManager.Instance != null)
        {
            MemeNetworkManager.Instance.UnregisterTaggedAnchorMeme(this);
        }
    }

    // --- MemeObjectState definition ---
    [Serializable]
    public struct MemeObjectState : INetworkSerializable
    {
        public string prefabName;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 scale;
        public int memeIndex;
        public PlacementMode placementMode;
        public int playerId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref prefabName);
            serializer.SerializeValue(ref localPosition);
            serializer.SerializeValue(ref localRotation);
            serializer.SerializeValue(ref scale);
            serializer.SerializeValue(ref memeIndex);
            serializer.SerializeValue(ref placementMode);
            serializer.SerializeValue(ref playerId);
        }

        // For network sync
        [Serializable]
        public struct Networked : INetworkSerializable, IEquatable<Networked>
        {
            public int memeIndex;
            public int playerId;
            public FixedString64Bytes prefabName;
            public int placementMode;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref memeIndex);
                serializer.SerializeValue(ref playerId);
                serializer.SerializeValue(ref prefabName);
                serializer.SerializeValue(ref placementMode);
            }
            public bool Equals(Networked other)
            {
                return memeIndex == other.memeIndex && playerId == other.playerId && prefabName.Equals(other.prefabName) && placementMode == other.placementMode;
            }
        }
        public Networked ToNetworked()
        {
            return new Networked
            {
                memeIndex = memeIndex,
                playerId = playerId,
                prefabName = prefabName,
                placementMode = (int)placementMode
            };
        }
    }
}
