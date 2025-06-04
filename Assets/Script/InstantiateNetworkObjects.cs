using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class InstantiateNetworkObjects : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject myPrefab;
    private GameObject myPrefabTransform = null;
    public GameObject myFacePrefab;
    public GameObject clientFacePrefab;
    private GameObject faceSwapTransform = null;
    private GameObject clientFaceSwapTransform = null;

    [SerializeField] private NetworkVariable<bool> memesOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [SerializeField] private NetworkVariable<bool> faceOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    

    [SerializeField] public List<GameObject> prefabList;

    private int currentIndex = 0;

    void Start()
    {
        
    }

    // to check whether play prefab is spawned in both client and host
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        transform.position = new Vector3(Random.Range(0, 1), 0, Random.Range(0, 1));
    }

    // Update is called once per frame
    void Update()
    {
        // only the owner can call function, other then host will control client
        if (!IsOwner) return;

        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            if (memesOn.Value == false)
            {
                // get the position of right cotroller
                Vector3 _position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
                Quaternion _rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

                // instantiate network gameobject
                InstantiatePrefabServerRpc(currentIndex, _position, _rotation);

                // update memes situation
                memesOn.Value = !memesOn.Value;

                // Advance and wrap index locally
                currentIndex = (currentIndex + 1) % prefabList.Count;
            } else
            {
                DespawnPrefabServerRpc();
               
                // update memes situation
                memesOn.Value = !memesOn.Value;
            }
            
        }

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            if (NetworkManager.Singleton.IsServer)
            {
                if (faceOn.Value == false)
                {
                    Transform cameraAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
                    FaceSwapServerRpc(cameraAnchor.position, cameraAnchor.rotation);

                    //faceSwapTransform.transform.localScale *= NetworkManager.Singleton.LocalClientId + 1;

                    faceOn.Value = !faceOn.Value;
                }
                else
                {
                    DespawnFaceSwapServerRpc();
                    faceOn.Value = !faceOn.Value;
                }
            }
            else
            {
                if (faceOn.Value == false)
                {
                    Transform cameraAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
                    ClientFaceSwapServerRpc(NetworkManager.Singleton.LocalClientId, cameraAnchor.position, cameraAnchor.rotation);

                    faceOn.Value = !faceOn.Value;
                }
                else
                {
                    ClientDespawnFaceSwapServerRpc();
                    faceOn.Value = !faceOn.Value;
                }
            }
        }
    }

   

    [ServerRpc]
    public void InstantiatePrefabServerRpc(int index, Vector3 position, Quaternion rotation)
    {

        if (index < 0 || index >= prefabList.Count) return;
       
       // GameObject spawnedPrefab = prefabList[index];
        myPrefabTransform = Instantiate(prefabList[index], position, rotation);

        if (myPrefabTransform != null) 
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        }
        else return;
    }


    [ServerRpc]
    public void DespawnPrefabServerRpc()
    {
        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Despawn(true);
        }
        else return;
    }

    //face swap
    [ServerRpc]
    public void FaceSwapServerRpc(Vector3 _position, Quaternion _rotation)
    {

        faceSwapTransform = Instantiate(myFacePrefab, _position, _rotation);

        //ulong clientId = rpcParams.Receive.SenderClientId;

        if (faceSwapTransform != null)
        {
            //faceSwapTransform.GetComponent<NetworkObject>().SpawnWithOwnership(clientId, true);
            faceSwapTransform.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    [ServerRpc]
    public void ClientFaceSwapServerRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        clientFaceSwapTransform = Instantiate(clientFacePrefab, position, rotation);

        if (clientFaceSwapTransform != null)
        {
            clientFaceSwapTransform.GetComponent<NetworkObject>().SpawnWithOwnership(clientId, true);
        }
    }


    [ServerRpc]
    public void DespawnFaceSwapServerRpc()
    {
        if (faceSwapTransform != null)
        {
            faceSwapTransform.GetComponent<NetworkObject>().Despawn(true);
            faceSwapTransform.SetActive(false);
        }
    }

    [ServerRpc]
    public void ClientDespawnFaceSwapServerRpc()
    {
        if (clientFaceSwapTransform != null)
        {
            //clientFaceSwapTransform.GetComponent<NetworkObject>().ChangeOwnership(NetworkManager.ServerClientId);
            clientFaceSwapTransform.GetComponent<NetworkObject>().Despawn(true);
            clientFaceSwapTransform.SetActive(false);
        }
    }
}

