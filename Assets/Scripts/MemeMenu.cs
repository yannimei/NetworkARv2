using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class MemeMenu : MonoBehaviour
{
    [Header("Meme Menu Settings")]
    [SerializeField] private GridLayoutGroup memeButtonsContainer;
    [SerializeField] private GameObject memeContextGroupPrefab;
    [SerializeField] private GameObject memeButtonPrefab;
    [SerializeField] private Button memeCloseButton;
    [SerializeField] private string memePath = "Memes/";
    [SerializeField] private Logger logger;
    private string playerMemePath;
    private readonly List<Button> memeButtons = new();
    private MemeContextGroupCollection memeContextGroupCollection;

    private MemeNetworkManager MemeNetworkManager => MemeNetworkManager.Instance;

    private enum MenuMode { QuickSelection, GroupMode }
    private MenuMode currentMode = MenuMode.QuickSelection;
    private List<MemeContextGroupCollection.Meme> quickSelectionMemes = new();

    private void Start()
    {
        SetPlayerMemePath();
        InitQuickSelection();
        AssignMemeButtons();
        memeCloseButton.onClick.RemoveAllListeners();
        memeCloseButton.onClick.AddListener(CloseCurrentMeme);
    }

    private void SetPlayerMemePath()
    {
        int playerId = PlayerIdManager.Instance.PlayerId;
        logger.LogDebug($"Setting player meme path for player ID: {playerId}", nameof(MemeMenu));
        if (playerId > 0 && playerId < 100)
        {
            playerMemePath = memePath + $"{playerId}";
            logger.LogDebug($"Player meme path set to: {playerMemePath}", nameof(MemeMenu));
        }
        else
            playerMemePath = "";
    }

    private void InitQuickSelection()
    {
        memeContextGroupCollection = Resources.Load<MemeContextGroupCollection>(playerMemePath + "/MemeContextGroupCollection");
        quickSelectionMemes.Clear();
        if (memeContextGroupCollection != null)
        {
            foreach (var group in memeContextGroupCollection.memeGroups)
            {
                quickSelectionMemes.Add(group.groupItems.Count > 0 ? group.groupItems[0] : null);
            }
        }
        else
        {
            logger.LogError($"No MemeContextGroupCollection found at {playerMemePath}/MemeContextGroupCollection", nameof(MemeMenu));
        }
    }

    public void SetMenuMode(int mode)
    {
        currentMode = (MenuMode)mode;
        AssignMemeButtons();
    }

    private void Update()
    {
        // Update the meme buttons if the mode has changed
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            var newMode = (currentMode == MenuMode.QuickSelection) ? MenuMode.GroupMode : MenuMode.QuickSelection;
            SetMenuMode((int)newMode);
        }
    }

    private void AssignMemeButtons()
    {
        foreach (Transform child in memeButtonsContainer.transform)
            Destroy(child.gameObject);
        memeButtons.Clear();

        if (currentMode == MenuMode.QuickSelection)
        {
            memeButtonsContainer.constraintCount = 4;
            memeButtonsContainer.cellSize = new Vector2(25, 25);

            for (int i = 0; i < quickSelectionMemes.Count; i++)
            {
                var meme = quickSelectionMemes[i];
                var buttonObj = Instantiate(memeButtonPrefab, memeButtonsContainer.transform);
                var btn = buttonObj.GetComponent<Button>();
                var rawImage = buttonObj.GetComponentInChildren<RawImage>();
                if (rawImage != null && meme.prefab != null)
                {
                    var preview = Resources.Load<Texture2D>(playerMemePath + "/MenuPreview/" + meme.prefab.name);
                    if (preview != null)
                        rawImage.texture = preview;
                    else
                        logger.LogWarning($"Preview texture not found for meme {meme.prefab.name}", nameof(MemeMenu));
                }
                // Set meme type text if TMP_Text is present
                var tmpText = buttonObj.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    if (meme.type == MemeContextGroupCollection.MemeType.TwoD)
                        tmpText.text = "2D";
                    else if (meme.type == MemeContextGroupCollection.MemeType.ThreeD)
                        tmpText.text = "3D";
                    else if (meme.type == MemeContextGroupCollection.MemeType.Face)
                        tmpText.text = "Face";
                    else if (meme.type == MemeContextGroupCollection.MemeType.Video)
                        tmpText.text = "Video";
                    else
                        tmpText.text = "Unknown";
                }
                if (btn != null)
                    {
                        int index = i;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnMemeButtonClicked(index));
                        memeButtons.Add(btn);
                    }
            }
        }
        else if (currentMode == MenuMode.GroupMode)
        {
            memeButtonsContainer.constraintCount = 2;
            memeButtonsContainer.cellSize = new Vector2(50, 50);

            memeContextGroupCollection = Resources.Load<MemeContextGroupCollection>(playerMemePath + "/MemeContextGroupCollection");
            if (memeContextGroupCollection == null)
            {
                logger.LogError($"No MemeContextGroupCollection found at {playerMemePath}/MemeContextGroupCollection", nameof(MemeMenu));
                return;
            }
            int groupIndex = 0;
            foreach (var group in memeContextGroupCollection.memeGroups)
            {
                var groupObj = Instantiate(memeContextGroupPrefab, memeButtonsContainer.transform);
                groupObj.name = $"MemeGroup_{groupIndex}_{group.groupName}";
                int memeIndex = 0;
                foreach (var meme in group.groupItems)
                {
                    var buttonObj = Instantiate(memeButtonPrefab, groupObj.transform);
                    var btn = buttonObj.GetComponent<Button>();
                    var rawImage = buttonObj.GetComponentInChildren<RawImage>();
                    if (rawImage != null && meme.prefab != null)
                    {
                        var preview = Resources.Load<Texture2D>(playerMemePath + "/MenuPreview/" + meme.prefab.name);
                        if (preview != null)
                            rawImage.texture = preview;
                        else
                        {
                            logger.LogWarning($"Preview texture not found for meme {meme.prefab.name} in group {group.groupName}", nameof(MemeMenu));
                        }
                    }
                    // Set meme type text if TMP_Text is present
                    var tmpText = buttonObj.GetComponentInChildren<TMP_Text>();
                    if (tmpText != null)
                    {
                        if (meme.type == MemeContextGroupCollection.MemeType.TwoD)
                            tmpText.text = "2D";
                        else if (meme.type == MemeContextGroupCollection.MemeType.ThreeD)
                            tmpText.text = "3D";
                        else if (meme.type == MemeContextGroupCollection.MemeType.Face)
                            tmpText.text = "Face";
                        else if (meme.type == MemeContextGroupCollection.MemeType.Video)
                            tmpText.text = "Video";
                        else
                            tmpText.text = "Unknown";
                    }
                    if (btn != null)
                    {
                        int capturedGroup = groupIndex;
                        int capturedMeme = memeIndex;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnGroupMemeSelected(capturedGroup, capturedMeme));
                        memeButtons.Add(btn);
                    }
                    memeIndex++;
                }
                groupIndex++;
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    private void OnGroupMemeSelected(int groupIndex, int memeIndex)
    {
        if (memeContextGroupCollection != null &&
            groupIndex < memeContextGroupCollection.memeGroups.Count &&
            memeIndex < memeContextGroupCollection.memeGroups[groupIndex].groupItems.Count)
        {
            var meme = memeContextGroupCollection.memeGroups[groupIndex].groupItems[memeIndex];
            // Ensure quickSelectionMemes is always the same length/order as memeGroups
            while (quickSelectionMemes.Count < memeContextGroupCollection.memeGroups.Count)
                quickSelectionMemes.Add(null);
            quickSelectionMemes[groupIndex] = meme;
            logger.LogInfo($"Assigned meme {(meme.prefab != null ? meme.prefab.name : null)} to quick slot {groupIndex}", nameof(MemeMenu));
        }
    }

    private void OnMemeButtonClicked(int index)
    {
        int playerId = PlayerIdManager.Instance.PlayerId;
        var meme = quickSelectionMemes[index];
        string memeName = meme != null && meme.prefab != null ? meme.prefab.name : "None";
        logger.LogInfo($"Meme button {index} clicked by player: {playerId}. Meme: {memeName}", nameof(MemeMenu));
        try
        {
            if (meme != null && meme.prefab != null)
            {
                // Find the index of the meme prefab in the player's meme folder
                var allMemes = Resources.LoadAll<GameObject>(playerMemePath);
                int prefabIndex = -1;
                for (int i = 0; i < allMemes.Length; i++)
                {
                    if (allMemes[i] == meme.prefab)
                    {
                        prefabIndex = i;
                        break;
                    }
                }
                if (prefabIndex >= 0)
                {
                    MemeNetworkManager.RequestSpawnMeme(memePath, prefabIndex, playerId, transform.position, transform.rotation);
                    logger.LogDebug($"RequestSpawnMeme succeeded for prefab index {prefabIndex}", nameof(MemeMenu));
                }
                else
                {
                    logger.LogError($"Meme prefab not found in folder for quick slot {index}", nameof(MemeMenu));
                }
            }
            else
            {
                logger.LogError($"No meme assigned to quick slot {index}", nameof(MemeMenu));
            }
        }
        catch (System.Exception ex)
        {
            logger.LogError($"Exception in RequestSpawnMeme: {ex}", nameof(MemeMenu));
        }
    }

    private void CloseCurrentMeme()
    {
        try
        {
            MemeNetworkManager.RequestCloseCurrentMeme();
            logger.LogInfo("CloseCurrentMeme called: current meme saved and removed.", nameof(MemeMenu));
        }
        catch (System.Exception ex)
        {
            logger.LogError($"Exception in RequestCloseCurrentMeme: {ex}", nameof(MemeMenu));
        }
    }
}