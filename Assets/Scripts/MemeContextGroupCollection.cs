using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MemeContextGroupCollection", menuName = "Memes/MemeContextGroupCollection", order = 1)]
public class MemeContextGroupCollection : ScriptableObject
{
    public enum MemeType
    {
        TwoD,
        ThreeD,
        Face,
        Video
    }

    [System.Serializable]
    public class Meme 
    {
        public MemeType type;
        public GameObject prefab;
    }

    [System.Serializable]
    public class MemeContextGroup
    {
        public string groupName;
        public List<Meme> groupItems = new();
    }

    public List<MemeContextGroup> memeGroups = new();
}