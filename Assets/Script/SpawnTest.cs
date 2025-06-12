using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpawnTest : NetworkBehaviour
{
    public GameObject testPrefab;
    public Button spawnButton;

    // Example position/rotation for spawning
    private Vector3 spawnPosition = new Vector3(0, 1, 0);
    private Quaternion spawnRotation = Quaternion.identity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        //if (!IsOwner) return;
        //transform.position = new Vector3(Random.Range(0,1),0, Random.Range(0, 1));

        if (NetworkManager.Singleton.IsServer)
        {
            transform.position = new Vector3(0, 0, 0.5f);
        } else {transform.position = new Vector3(0, 0, -0.5f); }

        spawnButton.onClick.AddListener(OnSpawnButtonClicked);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void OnSpawnButtonClicked()
    {
        if (IsOwner)
        {
            // You can customize index if you have multiple prefabs
            TestSpawnServerRpc(spawnPosition, spawnRotation);
        }

        //TestSpawnServerRpc(spawnPosition, spawnRotation);
    }

    [ServerRpc]
    public void TestSpawnServerRpc(Vector3 position, Quaternion rotation)
    {
        // GameObject spawnedPrefab = prefabList[index];
        var myPrefabTransform = Instantiate(testPrefab, position, rotation);

        if (myPrefabTransform != null)
        {
            myPrefabTransform.GetComponent<NetworkObject>().Spawn(true);
        }
    }

}
