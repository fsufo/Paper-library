using UnityEngine;
using UnityEngine.UI;
using GeminFactory;

namespace GeminFactory.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD References")]
        [SerializeField] private Text moneyText;
        [SerializeField] private Text currentToolText;
        [SerializeField] private Text inspectionText;
        
        private InspectionSystem inspectionSystem; // 只依赖 InspectionSystem

        void Awake()
        {
            GameEventBus.OnGameInitialized += OnGameInitialized;
        }

        void OnDestroy()
        {
            GameEventBus.OnGameInitialized -= OnGameInitialized;
            
            GameEventBus.OnMenuItemSelected -= OnRadialMenuItemSelected;
            GameEventBus.OnMoneyUpdated -= UpdateMoney;
            GameEventBus.OnInspectRequest -= ShowInspection;
            GameEventBus.OnInspectCancel -= HideInspection;
        }

        void OnGameInitialized(GameContext context)
        {
            inspectionSystem = context.InspectionSystem;
            
            // 初始状态
            if (inspectionText) inspectionText.gameObject.SetActive(false);

            // 订阅其他事件
            GameEventBus.OnMenuItemSelected += OnRadialMenuItemSelected;
            GameEventBus.OnMoneyUpdated += UpdateMoney;
            GameEventBus.OnInspectRequest += ShowInspection;
            GameEventBus.OnInspectCancel += HideInspection;
        }

        // --- Event Handlers ---

        void UpdateMoney(int amount)
        {
            if (moneyText) moneyText.text = $"Money: ${amount}";
        }

        void ShowInspection(Vector2Int pos)
        {
            if (!inspectionText || inspectionSystem == null) return;

            string info = inspectionSystem.GetInspectionInfo(pos);
            inspectionText.text = info;
            inspectionText.gameObject.SetActive(true);
        }

        void HideInspection()
        {
            if (inspectionText) inspectionText.gameObject.SetActive(false);
        }

        void OnRadialMenuItemSelected(RadialMenuItem item)
        {
            // 这里我们只负责更新 UI 显示，InputSystem 会自己处理逻辑
            // 但 InputSystem 也订阅了 radialMenu.onItemSelected
            
            string toolName = item != null ? item.name : "None";
            // 如果 item.buildMode 是 None，我们显示 "Inspect" 或 "None"
            if (item == null || item.buildMode == BuildMode.None) toolName = "Inspect";
            
            if (currentToolText) currentToolText.text = $"Tool: {toolName}";
        }

        // --- Helper Methods ---
        // GetInspectionInfo 方法已移除，逻辑移至 InspectionSystem
    }
}