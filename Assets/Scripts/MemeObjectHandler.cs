using UnityEngine;
using System;
using System.IO;
using Unity.Netcode;

public class MemeObjectHandler : NetworkBehaviour
{
    public int memeIndex;
    public string prefabName;
    public int playerId;
    public Transform anchorTransform;
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, $"{playerId}/meme_{memeIndex}.json");

    [Serializable]
    public class MemeObjectState
    {
        public string prefabName;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 scale;
        public int memeIndex;
    }

    public void Init(int index, string prefab, int playerId)
    {
        memeIndex = index;
        prefabName = prefab;
        this.playerId = playerId;
    }

    public void SaveState()
    {
        MemeObjectStateList stateList = MemeObjectStateList.Load(SaveFilePath);
        MemeObjectState state = new()
        {
            prefabName = prefabName,
            localPosition = anchorTransform ? anchorTransform.InverseTransformPoint(transform.position) : transform.position,
            localRotation = anchorTransform ? Quaternion.Inverse(anchorTransform.rotation) * transform.rotation : transform.rotation,
            scale = transform.localScale,
            memeIndex = memeIndex
        };
        stateList.SetState(memeIndex, state);
        stateList.Save(SaveFilePath);
    }

    public bool LoadStateWithResult()
    {
        MemeObjectStateList stateList = MemeObjectStateList.Load(SaveFilePath);
        MemeObjectState state = stateList.GetState(memeIndex);
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
            return true;
        }
        return false;
    }

    [Serializable]
    public class MemeObjectStateList
    {
        public System.Collections.Generic.List<MemeObjectState> states = new();

        public MemeObjectState GetState(int index)
        {
            return states.Find(s => s.memeIndex == index);
        }
        public void SetState(int index, MemeObjectState state)
        {
            int idx = states.FindIndex(s => s.memeIndex == index);
            if (idx >= 0) states[idx] = state;
            else states.Add(state);
        }
        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonUtility.ToJson(this);
            File.WriteAllText(path, json);
        }
        public static MemeObjectStateList Load(string path)
        {
            if (!File.Exists(path)) return new MemeObjectStateList();
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<MemeObjectStateList>(json);
        }
    }

    public void SaveStateNetworked()
    {
        if (IsServer)
        {
            SaveState();
        }
        else
        {
            SaveStateServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SaveStateServerRpc()
    {
        SaveState();
    }

    private void OnMouseUpAsButton()
    {
        SaveStateNetworked();
    }
}
