using UnityEngine;
using System;
using Unity.Netcode;

public class MemeObjectHandler : NetworkBehaviour
{
    public int memeIndex;
    public string prefabName;
    public int playerId;
    public Transform anchorTransform;

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
        prefabName = prefab;
        this.playerId = playerId;
    }

    public void SaveState()
    {
        MemeObjectState state = new()
        {
            prefabName = prefabName,
            localPosition = anchorTransform ? anchorTransform.InverseTransformPoint(transform.position) : transform.position,
            localRotation = anchorTransform ? Quaternion.Inverse(anchorTransform.rotation) * transform.rotation : transform.rotation,
            scale = transform.localScale,
            memeIndex = memeIndex
        };
        MemeNetworkManager.Instance.SaveMemeStateServer(playerId, memeIndex, state);
    }

    public void SaveStateNetworked()
    {
        MemeObjectState state = new()
        {
            prefabName = prefabName,
            localPosition = anchorTransform ? anchorTransform.InverseTransformPoint(transform.position) : transform.position,
            localRotation = anchorTransform ? Quaternion.Inverse(anchorTransform.rotation) * transform.rotation : transform.rotation,
            scale = transform.localScale,
            memeIndex = memeIndex
        };
        if (IsServer)
        {
            MemeNetworkManager.Instance.SaveMemeStateServer(playerId, memeIndex, state);
        }
        else
        {
            SaveStateServerRpc(playerId, memeIndex, state);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SaveStateServerRpc(int playerId, int memeIndex, MemeObjectState state)
    {
        MemeNetworkManager.Instance.SaveMemeStateServer(playerId, memeIndex, state);
    }

    public void RequestLoadState()
    {
        if (IsServer)
        {
            var state = MemeNetworkManager.Instance.GetMemeStateServer(playerId, memeIndex);
            if (state != null)
                ApplyState(state);
        }
        else
        {
            LoadStateServerRpc(playerId, memeIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadStateServerRpc(int playerId, int memeIndex, ServerRpcParams rpcParams = default)
    {
        var state = MemeNetworkManager.Instance.GetMemeStateServer(playerId, memeIndex);
        if (state != null)
        {
            LoadStateClientRpc(playerId, memeIndex, state, rpcParams.Receive.SenderClientId);
        }
    }

    [ClientRpc]
    private void LoadStateClientRpc(int playerId, int memeIndex, MemeObjectState state, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            ApplyState(state);
        }
    }

    private void ApplyState(MemeObjectState state)
    {
        if (state != null)
        {
            if (anchorTransform)
            {
                transform.SetPositionAndRotation(anchorTransform.TransformPoint(state.localPosition), anchorTransform.rotation * state.localRotation);
            }
            else
            {
                transform.SetPositionAndRotation(state.localPosition, state.localRotation);
            }
            transform.localScale = state.scale;
        }
    }

    // Remove file-based Save/Load logic and OnMouseUpAsButton now uses SaveStateNetworked
    private void OnMouseUpAsButton()
    {
        SaveStateNetworked();
    }
}
