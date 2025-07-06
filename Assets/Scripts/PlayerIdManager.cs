using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerIdManager : MonoBehaviour
{
    public static PlayerIdManager Instance { get; private set; }
    public int PlayerId { get; private set; } = -1;

    [Header("UI Elements")]
    [SerializeField] private Canvas idInputCanvas;
    [SerializeField] private TMP_InputField idInputField;
    [SerializeField] private Button confirmButton;

    [SerializeField] private Logger logger;

    [Tooltip("Reference to the MemeMenu canvas, which will be activated after setting the PlayerId")]
    [SerializeField] private Canvas MemeMenuCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetupUI();
    }

    private void SetupUI()
    {
        if (PlayerId > 0)
        {
            idInputCanvas.gameObject.SetActive(false);
            return;
        }
        idInputCanvas.gameObject.SetActive(true);
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirmId);
    }

    private void OnConfirmId()
    {
        if (int.TryParse(idInputField.text, out int id) && id >= 0 && id <= 100)
        {
            PlayerId = id;
            idInputCanvas.gameObject.SetActive(false);

            if (id == 0)
            {
                logger.LogInfo("Player ID is set to 0, player will be recognized as a spectator.", nameof(PlayerIdManager));
                return;
            }

            logger.LogInfo($"Player ID set to: {PlayerId}", nameof(PlayerIdManager));
            if (MemeMenuCanvas != null)
            {
                MemeMenuCanvas.gameObject.SetActive(true);
            }
            else
            {
                logger.LogError("MemeMenuCanvas is not assigned in PlayerIdManager.", nameof(PlayerIdManager));
            }
        }
        else
        {
            idInputField.text = "";
            logger.LogWarning("Invalid Player ID. Please enter a number between 1 and 100.", nameof(PlayerIdManager));
        }
    }


    // Only for Debugging in the Unity Editor
    private void Update()
    {
#if UNITY_EDITOR
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick) && idInputCanvas.gameObject.activeSelf)
        {
            idInputField.text = "1";
            OnConfirmId();
        }
#endif
    }
}
