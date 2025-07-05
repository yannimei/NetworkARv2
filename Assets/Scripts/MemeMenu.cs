using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class MemeMenu : MonoBehaviour
{
    [Header("Meme Menu Settings")]
    [SerializeField] private LayoutGroup memeButtonsContainer;
    [SerializeField] private GameObject memeButtonPrefab;
    [SerializeField] private Button memeCloseButton;
    [SerializeField] private string memePath = "Memes/";
    [SerializeField] private Logger logger;
    private string playerMemePath;
    private readonly List<Button> memeButtons = new();

    private MemeNetworkManager MemeNetworkManager => MemeNetworkManager.Instance;

    private void Start()
    {
        SetPlayerMemePath();
        AssignMemeButtons();
        memeCloseButton.onClick.RemoveAllListeners();
        memeCloseButton.onClick.AddListener(CloseCurrentMeme);
    }

    private void SetPlayerMemePath()
    {
        int playerId = PlayerIdManager.Instance.PlayerId;
        logger.Log($"Setting player meme path for player ID: {playerId}", true, nameof(MemeMenu));
        if (playerId > 0 && playerId < 100)
        {
            playerMemePath = memePath + $"{playerId}/MenuPreview";
            logger.Log($"Player meme path set to: {playerMemePath}", true, nameof(MemeMenu));
        }
        else
            playerMemePath = "";
    }

    private void AssignMemeButtons()
    {
        foreach (Transform child in memeButtonsContainer.transform)
            Destroy(child.gameObject);
        memeButtons.Clear();

        Texture2D[] textures = Resources.LoadAll<Texture2D>(playerMemePath);

        // sort textures by their meme number to fix the lexicographical order issue
        System.Array.Sort(textures, (a, b) =>
        {
          static int GetMemeNumber(Texture2D tex)
            {
                string name = tex.name;
                // Extract number after 'Meme'
                int idx = name.IndexOf("Meme");
                if (idx >= 0)
                {
                    string numPart = name[(idx + 4)..];
                    if (int.TryParse(numPart, out int num))
                        return num;
                }
                return 0;
            }
            return GetMemeNumber(a).CompareTo(GetMemeNumber(b));
        });
        
        for (int i = 0; i < textures.Length && textures[i].name.StartsWith("Meme"); i++)
        {
            logger.Log($"Creating meme button {i}: {textures[i].name}", true, nameof(MemeMenu));
            var buttonObj = Instantiate(memeButtonPrefab, memeButtonsContainer.transform);
            var btn = buttonObj.GetComponent<Button>();
            var rawImage = buttonObj.GetComponentInChildren<RawImage>();
            if (rawImage != null)
                rawImage.texture = textures[i];
            if (btn != null)
            {
                int index = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnMemeButtonClicked(index));
                memeButtons.Add(btn);
            }
        }

        // Force layout rebuild to ensure interaction surfaces are correctly sized after expanding the menu
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    private void OnMemeButtonClicked(int index)
    {
        int playerId = PlayerIdManager.Instance.PlayerId;
        logger.Log($"Meme button {index} clicked by player: {playerId}", true, nameof(MemeMenu));
        try
        {
            MemeNetworkManager.RequestSpawnMeme(memePath, index, playerId, transform.position, transform.rotation);
            logger.Log($"RequestSpawnMeme succeeded for index {index}", true, nameof(MemeMenu));
        }
        catch (System.Exception ex)
        {
            logger.Log($"Exception in RequestSpawnMeme: {ex}", true, nameof(MemeMenu));
        }
    }

    private void CloseCurrentMeme()
    {
        try
        {
            MemeNetworkManager.RequestCloseCurrentMeme();
            logger.Log("CloseCurrentMeme called: current meme saved and removed.", true, nameof(MemeMenu));
        }
        catch (System.Exception ex)
        {
            logger.Log($"Exception in RequestCloseCurrentMeme: {ex}", true, nameof(MemeMenu));
        }
    }
}