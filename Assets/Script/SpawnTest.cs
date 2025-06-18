using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpawnTest : NetworkBehaviour
{
    public GameObject testPrefab;
    public GameObject clientTestPrefab;
    public Button spawnButton;
    //timestamp variable
    private float lastMemeSpawn;
    private float clientLastMemeSpawn;

    // Example position/rotation for spawning
    private Vector3 spawnPosition = new Vector3(0, 1, 0);
    private Quaternion spawnRotation = Quaternion.identity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        spawnButton.onClick.RemoveAllListeners();

        if (NetworkManager.Singleton.IsServer)
        {
            //transform.position = new Vector3(0, 0, 0.5f);
            spawnButton.onClick.AddListener(OnSpawnButtonClicked);
        } else {
            //transform.position = new Vector3(0, 0, -0.5f);
            spawnButton.onClick.AddListener(OnClientSpawnButtonClicked);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }


    void OnSpawnButtonClicked()
    {
        if (!IsOwner) return;

        //check if last timestamp is e.g. less than 0.5 seconds ago. If yes, then only update timestamp and return without spawning
        if ((Time.time - lastMemeSpawn) < 0.5f)
        {
            lastMemeSpawn = Time.time;
            return;
        }
 
        Vector3 _position = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.position;
        Quaternion _rotation = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.rotation;
        TestSpawnServerRpc(_position, _rotation);

        //update timestamp
        lastMemeSpawn = Time.time;
    }

    void OnClientSpawnButtonClicked()
    {
        if (!IsOwner) return;

        //check if last timestamp is e.g. less than 0.5 seconds ago. If yes, then only update timestamp and return without spawning
        if ((Time.time - clientLastMemeSpawn) < 0.5f)
        {
            clientLastMemeSpawn = Time.time;
            return;
        }
        Vector3 _position = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.position;
        Quaternion _rotation = GameObject.Find("CameraRig/TrackingSpace/RightHandAnchor").transform.rotation;
        ClientTestSpawnServerRpc(_position, _rotation);

        //update timestamp
        clientLastMemeSpawn = Time.time;
    }



    [ServerRpc]
    public void TestSpawnServerRpc(Vector3 position, Quaternion rotation)
    {
        var myPrefabTransform = Instantiate(testPrefab, position, rotation);

        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        }
    }


    [ServerRpc]
    public void ClientTestSpawnServerRpc(Vector3 position, Quaternion rotation)
    {
        var myPrefabTransform = Instantiate(clientTestPrefab, position, rotation);

        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}

