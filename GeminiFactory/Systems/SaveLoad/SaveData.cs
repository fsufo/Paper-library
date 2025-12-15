using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory.Systems.SaveLoad
{
    [System.Serializable]
    public class SaveData
    {
        public int width;
        public int height;
        public int money;
        public List<SavedBelt> belts = new List<SavedBelt>();
        public List<SavedBuilding> buildings = new List<SavedBuilding>();
        public List<SavedItem> items = new List<SavedItem>();
    }

    [System.Serializable]
    public class SavedBelt
    {
        public int x;
        public int y;
        public int direction; // 1-4 or Elevator ID
        public int height;    // [New] Layer Height
    }

    [System.Serializable]
    public class SavedBuilding
    {
        public int x;
        public int y;
        public string buildingName; // Name of the SO asset
        
        // State
        public float progressTimer;
        public bool isProcessing;
        public List<SavedInventoryItem> inventory = new List<SavedInventoryItem>();
    }

    [System.Serializable]
    public class SavedInventoryItem
    {
        public int itemID;
        public int count;
    }

    [System.Serializable]
    public class SavedItem
    {
        public float x;
        public float y;
        public int itemID;
        public int price;
        public float r, g, b;
        public float height; // [New] Stores targetHeight (Logic Layer)
    }
}