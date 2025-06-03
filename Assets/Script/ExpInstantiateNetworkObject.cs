using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class ExpInstantiateNetworkObjects : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject myPrefab;
    [SerializeField] public List<GameObject> prefabList;
    private int currentIndex = 0;

    void Start()
    {

    }

    // to check whether play prefab is spawned in both client and host
    public override void OnNetworkSpawn()
    {
        transform.position = new Vector3(Random.Range(1, -1), 0, Random.Range(1, -1));
    }

    // Update is called once per frame
    void Update()
    {
        // only the owner can call function, other then host will control client
        if (!IsOwner) return;

        if (OVRInput.GetDown(OVRInput.Button.Two))
        {

            currentIndex = 0;
            // get the position of right cotroller
            Vector3 _position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            Quaternion _rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

            // instantiate network gameobject
            InstantiatePrefabServerRpc(currentIndex, _position, _rotation);
        }

      

        if (OVRInput.GetDown(OVRInput.Button.Three))
        {

            currentIndex = 1;
            // get the position of right cotroller
            Vector3 _position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            Quaternion _rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);

            // instantiate network gameobject
            InstantiatePrefabServerRpc(currentIndex, _position, _rotation);
        }

        if (OVRInput.GetDown(OVRInput.Button.Four))
        {

            currentIndex = 2;
            // get the position of right cotroller
            Vector3 _position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            Quaternion _rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);

            // instantiate network gameobject
            InstantiatePrefabServerRpc(currentIndex, _position, _rotation);
        }

        if (OVRInput.GetDown(OVRInput.Button.Start))
        {

            currentIndex = 3;
            // get the position of right cotroller
            Vector3 _position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            Quaternion _rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);

            // instantiate network gameobject
            InstantiatePrefabServerRpc(currentIndex, _position, _rotation);
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

        GameObject spawnedPrefab = prefabList[index];
        var instance = Instantiate(spawnedPrefab, position, rotation);
        var instanceNetworkObject = instance.GetComponent<NetworkObject>();
        instanceNetworkObject.Spawn();
    }
}