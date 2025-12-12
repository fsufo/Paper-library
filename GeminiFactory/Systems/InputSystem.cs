using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using GeminFactory.Systems.InputStates;

namespace GeminFactory
{
    /// <summary>
    /// 输入系统 (状态机管理器)
    /// <para>负责管理当前的输入状态，并将输入事件委托给当前状态处理。</para>
    /// </summary>
    [System.Serializable]
    public class InputSystem
    {
        #region Fields & State
        private GameContext context;
        private InputState currentState;
        private Dictionary<BuildMode, InputState> states = new Dictionary<BuildMode, InputState>();

        public BuildMode currentMode { get; private set; } = BuildMode.None;
        
        // --- Input Actions ---
        private InputActionAsset inputAsset;
        public InputAction ClickAction { get; private set; }
        public InputAction RightClickAction { get; private set; }
        public InputAction PointAction { get; private set; }
        public InputAction ScrollWheelAction { get; private set; }
        public InputAction RadialMenuAction { get; private set; } // 新增：菜单键 (Space)
        public InputAction RotateAction { get; private set; } // 新增：旋转键 (R)

        // --- Mouse State ---
        public Vector2Int CurrentMouseGridPos { get; private set; }
        public Vector3 CurrentMouseWorldPos { get; private set; }
        public bool IsMouseValid { get; private set; }
        #endregion

        #region Initialization
        /// <summary>
        /// 初始化输入系统
        /// </summary>
        public void Initialize(GameContext context)
        {
            this.context = context;
            var config = context.GameConfig;

            // Initialize Input Actions
            if (config.inputActions != null)
            {
                inputAsset = config.inputActions;
                inputAsset.Enable();

                var uiMap = inputAsset.FindActionMap("UI");
                if (uiMap != null)
                {
                    ClickAction = uiMap.FindAction("Click");
                    RightClickAction = uiMap.FindAction("RightClick");
                    PointAction = uiMap.FindAction("Point");
                    ScrollWheelAction = uiMap.FindAction("ScrollWheel");
                    RadialMenuAction = uiMap.FindAction("RadialMenu"); // 尝试查找 Menu Action
                    
                    // 如果没有找到 Menu，尝试查找 Space 或者 Submit，或者提示用户
                    if (RadialMenuAction == null) RadialMenuAction = uiMap.FindAction("Space");

                    RotateAction = uiMap.FindAction("Rotate"); // 查找 Rotate Action
                    if (RotateAction == null) RotateAction = uiMap.FindAction("R"); // 备用查找
                }
                else
                {
                    Debug.LogError("Input Action Map 'UI' not found in the assigned Input Asset!");
                }
            }
            else
            {
                Debug.LogError("Input Actions Asset is missing in GameConfig!");
            }

            // Initialize States
            states[BuildMode.None] = new InspectInputState(this, context);
            states[BuildMode.Belt] = new BeltInputState(this, context);
            states[BuildMode.Delete] = new DeleteInputState(this, context, config.deleteRadius);
            
            // Set initial state
            ChangeState(BuildMode.None);

            GameEventBus.OnMenuItemSelected += OnMenuSelection;
            GameEventBus.OnInputUpdate += HandleInput;
        }

        public void Dispose()
        {
            inputAsset?.Disable();
            // inputAsset?.Dispose(); // Don't dispose SO asset

            GameEventBus.OnMenuItemSelected -= OnMenuSelection;
            GameEventBus.OnInputUpdate -= HandleInput;
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// 处理每帧输入 (在 Update 中调用)
        /// </summary>
        public void HandleInput()
        {
            if (context.MainCamera == null) return;

            // 1. Update Mouse State
            UpdateMouseState();

            // Right click to cancel/reset to Inspect mode
            if (RightClickAction != null && RightClickAction.WasPressedThisFrame())
            {
                if (currentMode != BuildMode.None)
                {
                    OnMenuSelection(null);
                    return;
                }
            }

            currentState?.HandleInput();
            currentState?.UpdatePreview();
        }
        #endregion

        #region State Management
        void OnMenuSelection(GeminFactory.UI.RadialMenuItem item)
        {
            BuildMode newMode = item != null ? item.buildMode : BuildMode.None;
            
            // Special handling for Factory/Shop to inject data
            if (newMode == BuildMode.Factory || newMode == BuildMode.Shop)
            {
                if (item != null && item.buildingData != null)
                {
                    currentState?.Exit();
                    currentMode = newMode;
                    currentState = new BuildingInputState(this, context, item.buildingData);
                    currentState.Enter();
                }
            }
            else
            {
                ChangeState(newMode);
            }
        }

        void ChangeState(BuildMode mode)
        {
            if (states.TryGetValue(mode, out InputState newState))
            {
                currentState?.Exit();
                currentMode = mode;
                currentState = newState;
                currentState.Enter();
            }
        }
        #endregion

        #region Helpers
        private void UpdateMouseState()
        {
            if (PointAction == null) return;

            Vector2 mousePos = PointAction.ReadValue<Vector2>();
            Ray ray = context.MainCamera.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            
            if (groundPlane.Raycast(ray, out float enter))
            {
                CurrentMouseWorldPos = ray.GetPoint(enter);
                CurrentMouseGridPos = context.MapManager.WorldToGrid(CurrentMouseWorldPos);
                IsMouseValid = context.MapManager.IsValidGridPosition(CurrentMouseGridPos);
            }
            else
            {
                IsMouseValid = false;
            }
        }

        /// <summary>
        /// 获取鼠标指向的网格坐标 (Helper for states)
        /// </summary>
        public bool GetMouseGridPosition(out Vector2Int gridPos)
        {
            gridPos = CurrentMouseGridPos;
            return IsMouseValid;
        }

        /// <summary>
        /// 获取鼠标屏幕坐标 (UI 使用)
        /// </summary>
        public Vector2 GetMouseScreenPosition()
        {
            if (PointAction != null)
            {
                return PointAction.ReadValue<Vector2>();
            }
            return Vector2.zero;
        }

        // --- Menu Action Helpers ---
        public bool IsRadialMenuPressed => RadialMenuAction != null && RadialMenuAction.IsPressed();
        public bool WasRadialMenuPressed => RadialMenuAction != null && RadialMenuAction.WasPressedThisFrame();
        public bool WasRadialMenuReleased => RadialMenuAction != null && RadialMenuAction.WasReleasedThisFrame();

        // --- Click Action Helpers ---
        public bool IsClickPressed => ClickAction != null && ClickAction.IsPressed();
        public bool WasClickPressed => ClickAction != null && ClickAction.WasPressedThisFrame();
        public bool WasClickReleased => ClickAction != null && ClickAction.WasReleasedThisFrame();

        // --- Right Click Action Helpers ---
        public bool WasRightClickPressed => RightClickAction != null && RightClickAction.WasPressedThisFrame();

        // --- Scroll Wheel Helpers ---
        public float ScrollValue => ScrollWheelAction != null ? ScrollWheelAction.ReadValue<Vector2>().y : 0f;

        // --- Rotate Action Helpers ---
        public bool WasRotatePressed => RotateAction != null && RotateAction.WasPressedThisFrame();

        #endregion
    }
}