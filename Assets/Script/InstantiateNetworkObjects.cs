using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class InstantiateNetworkObjects : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject myPrefab;
    private GameObject myPrefabTransform = null;
    public NetworkVariable<bool> memesOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [SerializeField] public List<GameObject> prefabList;
    private int currentIndex = 0;

    void Start()
    {
        
    }

    // to check whether play prefab is spawned in both client and host
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        this.transform.position = Vector3.zero;
        // Get reference to the OVRCameraRig's CenterEyeAnchor
        Transform cameraAnchor = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor").transform;

        if( cameraAnchor != null)
        {
            this.transform.localScale *= 2;
        }

        //this.transform.SetParent(cameraAnchor, false);
        //this.transform.localPosition = Vector3.zero;
        //this.transform.localRotation = Quaternion.identity;
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
    }

 
    // the client need to call server to instantiate object
    [ServerRpc]
    public void InstantiatePrefabServerRpc(int index, Vector3 position, Quaternion rotation)
    {

        if (index < 0 || index >= prefabList.Count)
        {
            Debug.LogError("Invalid prefab index");
            return;
        }

       // GameObject spawnedPrefab = prefabList[index];
        myPrefabTransform = Instantiate(prefabList[index], position, rotation);

        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        } else
        {
            return;
        }
    }
        

    [ServerRpc]
    public void DespawnPrefabServerRpc()
    {
        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Despawn(true);
        }
        else
        {
            return;
        }
    }
}

