using Unity.Netcode;
using UnityEngine;

public class FollowCamera : NetworkBehaviour
{
    public Transform cam;

    private void Start()
    {
        cam = GameObject.Find("CameraRig/TrackingSpace/CenterEyeAnchor/Face").transform;
    }
    void Update()
    {
        if (!IsOwner) return;

        this.transform.position = cam.position;
        this.transform.rotation = cam.rotation;
    }

    public void OverrideCamAnchor(Transform clientCam)
    {
        this.cam = clientCam;
    }
}
