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
    private readonly List<MemeContextGroupCollection.Meme> quickSelectionMemes = new();

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
            // Sort memeGroups and groupItems using natural sort on prefab name
            memeContextGroupCollection.memeGroups.Sort((a, b) => NaturalCompare(a.groupName, b.groupName));
            foreach (var group in memeContextGroupCollection.memeGroups)
            {
                group.groupItems.Sort((a, b) => NaturalCompare(a?.prefab?.name, b?.prefab?.name));
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
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            var newMode = (currentMode == MenuMode.QuickSelection) ? MenuMode.GroupMode : MenuMode.QuickSelection;
            SetMenuMode((int)newMode);
        }
    }

    private void AssignMemeButtons()
    {
        // Remove all children from memeButtonsContainer
        foreach (Transform child in memeButtonsContainer.transform)
            Destroy(child.gameObject);
        memeButtons.Clear();

        // Add quick selection buttons (always 1 per group)
        for (int i = 0; i < quickSelectionMemes.Count; i++)
        {
            var meme = quickSelectionMemes[i];
            var buttonObj = Instantiate(memeButtonPrefab, memeButtonsContainer.transform);
            var btn = buttonObj.GetComponent<Button>();
            
            // Set preview image
            var rawImage = buttonObj.GetComponentInChildren<RawImage>();
            if (rawImage != null && meme?.prefab != null)
            {
                var preview = Resources.Load<Texture2D>(playerMemePath + "/MenuPreview/" + meme.prefab.name);
                if (preview != null)
                    rawImage.texture = preview;
                else
                    logger.LogWarning($"Preview texture not found for meme {meme.prefab.name}", nameof(MemeMenu));
            }
            
            // Set text using helper method
            var tmpText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
                tmpText.text = GetMemeTypeText(meme);
            
            // Set button click handler
            if (btn != null)
            {
                int index = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnMemeButtonClicked(index));
                memeButtons.Add(btn);
            }
        }

        // In GroupMode, add group meme buttons after quick selection
        if (currentMode == MenuMode.GroupMode && memeContextGroupCollection != null)
        {
            int groupIndex = 0;
            foreach (var group in memeContextGroupCollection.memeGroups)
            {
                var groupObj = Instantiate(memeContextGroupPrefab, memeButtonsContainer.transform);

                int memeIndex = 0;
                foreach (var meme in group.groupItems)
                {
                    var buttonObj = Instantiate(memeButtonPrefab, groupObj.transform);
                    var btn = buttonObj.GetComponent<Button>();
                    
                    // Set preview image
                    var rawImage = buttonObj.GetComponentInChildren<RawImage>();
                    if (rawImage != null && meme.prefab != null)
                    {
                        var preview = Resources.Load<Texture2D>(playerMemePath + "/MenuPreview/" + meme.prefab.name);
                        if (preview != null)
                            rawImage.texture = preview;
                        else
                            logger.LogWarning($"Preview texture not found for meme {meme.prefab.name} in group {group.groupName}", nameof(MemeMenu));
                    }
                    
                    // Set text using helper method
                    var tmpText = buttonObj.GetComponentInChildren<TMP_Text>();
                    if (tmpText != null)
                        tmpText.text = GetMemeTypeText(meme);
                    
                    // Set button click handler
                    if (btn != null)
                    {
                        int capturedGroup = groupIndex;
                        int capturedMeme = memeIndex;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnGroupMemeSelectedAndSpawn(capturedGroup, capturedMeme));
                        memeButtons.Add(btn);
                    }
                    memeIndex++;
                }
                groupIndex++;
            }
        }
        // Manually set the height of the memeButtonsContainer
        SetContainerHeight();

        LayoutRebuilder.ForceRebuildLayoutImmediate(memeButtonsContainer.GetComponent<RectTransform>());
    }

    private void SetContainerHeight()
    {
        if (!memeButtonsContainer.TryGetComponent<RectTransform>(out var containerRect)) return;

        float memeButtonHeight = memeButtonsContainer.cellSize.y;
        float spacing = memeButtonsContainer.spacing.y;
        float padding = memeButtonsContainer.padding.top + memeButtonsContainer.padding.bottom;
        int constraintCount = memeButtonsContainer.constraintCount > 0 ? memeButtonsContainer.constraintCount : 1;

        // Quick selection: always one row
        float quickSelectionHeight = memeButtonHeight + padding;
        int totalRows = 1;

        // In GroupMode, add rows for each group
        if (currentMode == MenuMode.GroupMode && memeContextGroupCollection != null)
        {
            foreach (var group in memeContextGroupCollection.memeGroups)
            {
                int memeCount = group.groupItems.Count;
                int groupRows = Mathf.CeilToInt((float)memeCount / constraintCount);
                if (groupRows == 0) groupRows = 1;
                totalRows += groupRows;
            }
        }

        float totalHeight = (memeButtonHeight * totalRows) + (spacing * (totalRows - 1)) + padding;
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        logger.LogDebug($"Container height set to: {totalHeight} (Rows: {totalRows}, ButtonHeight: {memeButtonHeight}, Spacing: {spacing}, Padding: {padding})", nameof(MemeMenu));
    }

    // Helper method to get display text for meme type
    private string GetMemeTypeText(MemeContextGroupCollection.Meme meme)
    {
        if (meme == null) return "Empty";
        
        return meme.type switch
        {
            MemeContextGroupCollection.MemeType.TwoD => "2D",
            MemeContextGroupCollection.MemeType.ThreeD => "3D",
            MemeContextGroupCollection.MemeType.Face => "Face",
            MemeContextGroupCollection.MemeType.Video => "Video",
            _ => "Unknown"
        };
    }

    // Helper method to find prefab index in resources
    private int FindPrefabIndex(GameObject prefab)
    {
        if (prefab == null) return -1;
        var allMemes = new List<GameObject>(Resources.LoadAll<GameObject>(playerMemePath));
        allMemes.Sort((a, b) => NaturalCompare(a?.name, b?.name));
        for (int i = 0; i < allMemes.Count; i++)
        {
            if (allMemes[i] == prefab)
                return i;
        }
        return -1;
    }

    // Consolidated method for spawning memes
    private void SpawnMeme(MemeContextGroupCollection.Meme meme, int slotIndex)
    {
        if (meme?.prefab == null)
        {
            logger.LogError($"No meme assigned to slot {slotIndex}", nameof(MemeMenu));
            return;
        }

        int prefabIndex = FindPrefabIndex(meme.prefab);
        if (prefabIndex >= 0)
        {
            int playerId = PlayerIdManager.Instance.PlayerId;
            MemeNetworkManager.RequestSpawnMeme(memePath, prefabIndex, playerId, transform.position, transform.rotation);
            logger.LogDebug($"RequestSpawnMeme succeeded for prefab index {prefabIndex}", nameof(MemeMenu));
        }
        else
        {
            logger.LogError($"Meme prefab not found in folder for slot {slotIndex}", nameof(MemeMenu));
        }
    }

    // Update quick selection button visuals for a specific slot
    private void UpdateQuickSelectionButton(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < memeButtons.Count)
        {
            var meme = quickSelectionMemes[slotIndex];
            var buttonObj = memeButtons[slotIndex].gameObject;
            
            // Update preview image
            var rawImage = buttonObj.GetComponentInChildren<RawImage>();
            if (rawImage != null && meme?.prefab != null)
            {
                var preview = Resources.Load<Texture2D>(playerMemePath + "/MenuPreview/" + meme.prefab.name);
                if (preview != null)
                    rawImage.texture = preview;
                else
                    logger.LogWarning($"Preview texture not found for meme {meme.prefab.name}", nameof(MemeMenu));
            }
            
            // Update text
            var tmpText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
                tmpText.text = GetMemeTypeText(meme);
        }
    }

    // When a group meme is selected, update quick selection and spawn meme
    private void OnGroupMemeSelectedAndSpawn(int groupIndex, int memeIndex)
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
            logger.LogInfo($"Assigned meme {meme?.prefab?.name ?? "null"} to quick slot {groupIndex}", nameof(MemeMenu));
            
            // Spawn the meme
            SpawnMeme(meme, groupIndex);
            
            // Only update the affected quick selection button instead of rebuilding everything
            if (currentMode == MenuMode.QuickSelection || groupIndex < memeButtons.Count)
                UpdateQuickSelectionButton(groupIndex);
        }
    }

    private void OnMemeButtonClicked(int index)
    {
        int playerId = PlayerIdManager.Instance.PlayerId;
        var meme = quickSelectionMemes[index];
        string memeName = meme?.prefab?.name ?? "None";
        logger.LogInfo($"Meme button {index} clicked by player: {playerId}. Meme: {memeName}", nameof(MemeMenu));
        
        try
        {
            SpawnMeme(meme, index);
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

    // Natural sort comparison for strings with numbers (e.g. meme2 < meme10)
    private int NaturalCompare(string a, string b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        string padNumbers(string input) => System.Text.RegularExpressions.Regex.Replace(input ?? "", "\\d+", m => m.Value.PadLeft(10, '0'));
        return string.Compare(padNumbers(a), padNumbers(b), System.StringComparison.Ordinal);
    }
}