using Unity.Netcode;
using UnityEngine;

public class FollowInterface : NetworkBehaviour
{
    private Transform ui;

    private void Start()
    {
        ui = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Interface").transform;
    }
    void Update()
    {
        if (!IsOwner) return;

        this.transform.position = ui.position;
        this.transform.rotation = ui.rotation;
    }
}
