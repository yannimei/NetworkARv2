using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpawnTest : NetworkBehaviour
{
    //timestamp variable
    private float lastMemeSpawn;
    private float clientLastMemeSpawn;

    private GameObject myPrefabTransform = null;
    private GameObject clientPrefabTransform = null;


    [Header("Server (Host) Prefabs & Buttons")]
    public List<GameObject> memePrefabs;
    public List<GameObject> clientMemePrefabs;
    public List<Button> buttons;
    public Button despawnButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        for (int i = 0; i < memePrefabs.Count; i++)
        {
            buttons[i].onClick.RemoveAllListeners();
        }
        despawnButton.onClick.RemoveAllListeners();

        if (NetworkManager.Singleton.IsServer)
        {
            for (int i=0; i < memePrefabs.Count; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(()=>OnSpawnButtonClicked(index));
            }
            despawnButton.onClick.AddListener(OnClickDespawn);
        } else {
            for (int i = 0; i < memePrefabs.Count; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => OnClientSpawnButtonClicked(index));
            }
            despawnButton.onClick.AddListener(ClientOnClickDespawn);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //if (OVRInput.GetDown(OVRInput.Button.One))
        //{
        //    if (NetworkManager.Singleton.IsServer)
        //    {
        //        DepawnServerRpc();
        //    }
        //    else
        //    {
        //        ClientDepawnServerRpc();
        //    }
        //}
    }


    void OnSpawnButtonClicked(int memeIndex)
    {
        if (!IsOwner) return;

        DepawnServerRpc();

        //check if last timestamp is e.g. less than 0.5 seconds ago. If yes, then only update timestamp and return without spawning
        if ((Time.time - lastMemeSpawn) < 0.5f)
        {
            lastMemeSpawn = Time.time;
            return;
        }
 
        Vector3 _position = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.position;
        Quaternion _rotation = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.rotation;
        TestSpawnServerRpc(_position, _rotation,memeIndex);

        //update timestamp
        lastMemeSpawn = Time.time;
    }

    void OnClientSpawnButtonClicked(int memeIndex)
    {
        if (!IsOwner) return;

        ClientDepawnServerRpc();

        //check if last timestamp is e.g. less than 0.5 seconds ago. If yes, then only update timestamp and return without spawning
        if ((Time.time - clientLastMemeSpawn) < 0.5f)
        {
            clientLastMemeSpawn = Time.time;
            return;
        }
        Vector3 _position = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.position;
        Quaternion _rotation = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.rotation;
        ClientTestSpawnServerRpc(_position, _rotation, NetworkManager.Singleton.LocalClientId, memeIndex) ;

        //update timestamp
        clientLastMemeSpawn = Time.time;
    }

    void OnClickDespawn()
    {
        DepawnServerRpc();
    }

    void ClientOnClickDespawn()
    {
        ClientDepawnServerRpc();
    }



    [ServerRpc]
    private void TestSpawnServerRpc(Vector3 position, Quaternion rotation, int memeId)
    {
        if (memeId < 0 || memeId >= memePrefabs.Count) return;

        myPrefabTransform = Instantiate(memePrefabs[memeId], position, rotation);

        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    [ServerRpc]
    private void ClientTestSpawnServerRpc(Vector3 position, Quaternion rotation, ulong clientID, int clientMemeId)
    {
        if (clientMemeId < 0 || clientMemeId >= clientMemePrefabs.Count) return;

        clientPrefabTransform = Instantiate(clientMemePrefabs[clientMemeId], position, rotation);

        if (clientPrefabTransform != null)
        {
            clientPrefabTransform.GetComponent<NetworkObject>().SpawnWithOwnership(clientID);
        }
    }

    [ServerRpc]
    public void DepawnServerRpc()
    {
        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Despawn(true);
            myPrefabTransform.SetActive(false);
        }
    }

    [ServerRpc]
    public void ClientDepawnServerRpc()
    {
        if (clientPrefabTransform != null)
        {
            clientPrefabTransform.GetComponent<NetworkObject>().Despawn(true);
            clientPrefabTransform.SetActive(false);
        }
    }
}

