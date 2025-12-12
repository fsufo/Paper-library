using UnityEngine;
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
            if (Input.GetMouseButtonDown(1))
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
            Ray ray = context.MainCamera.ScreenPointToRay(Input.mousePosition);
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
        #endregion
    }
}