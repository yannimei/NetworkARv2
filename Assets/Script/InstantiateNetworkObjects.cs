using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class InstantiateNetworkObjects : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject myPrefab;
    private GameObject myPrefabTransform = null;
    public GameObject myFacePrefab;
    private GameObject faceSwapTransform = null;

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
            if (faceOn.Value == false)
            {
                Transform cameraAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
                FaceSwapServerRpc(cameraAnchor.position, cameraAnchor.rotation, NetworkManager.Singleton.LocalClientId);

                faceOn.Value = !faceOn.Value;
            } else
            {
                DespawnFaceSwapServerRpc();
                faceOn.Value = !faceOn.Value;
            }
                
        }

        //let the face follow the camera
        //if (faceSwapTransform != null && faceOn.Value)
        //{
        //    Transform cameraAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
        //    FollowCamera(cameraAnchor.position,cameraAnchor.rotation) ;
        //}
        
        Transform camAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
        this.IFollowCamera(camAnchor.position, camAnchor.rotation);
    }


    public void IFollowCamera(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }

    public void FollowCamera(Vector3 position, Quaternion rotation)
    {
        faceSwapTransform.transform.position = position;
        faceSwapTransform.transform.rotation = rotation;
    }

    // the client need to call server to instantiate object
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
    public void FaceSwapServerRpc(Vector3 _position, Quaternion _rotation, ulong clientId)
    {
        
        faceSwapTransform = Instantiate(myFacePrefab, _position, _rotation);

        //ulong clientId = rpcParams.Receive.SenderClientId;

        if (faceSwapTransform != null)
        {
            faceSwapTransform.GetComponent<NetworkObject>().SpawnWithOwnership(clientId,true);
        }
        else return;
    }

    [ServerRpc]
    public void DespawnFaceSwapServerRpc()
    {
        if (faceSwapTransform != null)
        {
            faceSwapTransform.GetComponent<NetworkObject>().Despawn(true);
        } else return;
    }

    [ServerRpc]
    public void FollowCameraServerRpc(Vector3 position, Quaternion rotation)
    {
        faceSwapTransform.transform.position = position;
        faceSwapTransform.transform.rotation = rotation;
    }

}

