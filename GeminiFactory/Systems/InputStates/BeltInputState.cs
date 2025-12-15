using UnityEngine;
using GeminFactory; // [Fix] Add missing namespace

namespace GeminFactory.Systems.InputStates
{
    public class BeltInputState : InputState
    {
        private Vector2Int? dragStartPos = null; // 这个似乎没用，下面用了 startPos
        private bool isAlternatePath = false;
        
        // State
        private bool isBuilding = false; // Changed from isDragging
        private Vector2Int startPos;
        private int startHeight; // [New] Record start height
        
        // [New] Height Control
        private int currentHeight = 0; // 当前建造高度 (0-based)
        private int MAX_HEIGHT => FactoryConstants.MAX_LAYERS - 1;

        public BeltInputState(InputSystem system, GameContext ctx) : base(system, ctx) { }

        public override void HandleInput()
        {
            if (!system.IsMouseValid) return;
            Vector2Int currentPos = system.CurrentMouseGridPos;

            // [New] 滚轮调整高度
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                currentHeight = Mathf.Clamp(currentHeight + (scroll > 0 ? 1 : -1), 0, MAX_HEIGHT);
                // TODO: 更新 UI 显示当前高度
                Debug.Log($"Current Layer: {currentHeight}");
            }

            if (system.WasClickPressed)
            {
                if (!isBuilding)
                {
                    // 第一次点击：确定起点
                    isBuilding = true;
                    startPos = currentPos;
                    startHeight = currentHeight; // [Fix] Record start height
                }
                else
                {
                    // 第二次点击：确定终点并建造
                    if (currentPos != startPos || currentHeight != startHeight) // Allow vertical build in place
                    {
                        // [Fix] Use startHeight for start position
                        GameEventBus.RequestBeltBuild(new Vector3Int(startPos.x, startPos.y, startHeight), new Vector3Int(currentPos.x, currentPos.y, currentHeight), isAlternatePath);
                        
                        // 非连续建造：重置状态
                        isBuilding = false;
                        context.PreviewSystem.ClearPreview();
                    }
                }
            }
            
            // R 键切换路径模式
            if (system.WasRotatePressed) 
            {
                isAlternatePath = !isAlternatePath;
            }
        }

        public override void UpdatePreview()
        {
            if (!system.IsMouseValid)
            {
                context.PreviewSystem.ClearPreview();
                return;
            }

            Vector2Int currentPos = system.CurrentMouseGridPos;

            if (isBuilding)
            {
                // [Fix] Use startHeight for preview
                context.PreviewSystem.UpdateBeltPreview(new Vector3Int(startPos.x, startPos.y, startHeight), new Vector3Int(currentPos.x, currentPos.y, currentHeight), isAlternatePath);
            }
            else
            {
                context.PreviewSystem.UpdateBeltPreview(new Vector3Int(currentPos.x, currentPos.y, currentHeight), new Vector3Int(currentPos.x, currentPos.y, currentHeight), false);
            }
        }
        
        public override void Exit()
        {
            base.Exit();
            isBuilding = false;
        }
    }
}