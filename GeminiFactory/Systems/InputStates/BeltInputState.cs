using UnityEngine;

namespace GeminFactory.Systems.InputStates
{
    public class BeltInputState : InputState
    {
        private Vector2Int? dragStartPos = null; // 这个似乎没用，下面用了 startPos
        private bool isAlternatePath = false;
        
        // State
        private bool isBuilding = false; // Changed from isDragging
        private Vector2Int startPos;

        public BeltInputState(InputSystem system, GameContext ctx) : base(system, ctx) { }

        public override void HandleInput()
        {
            if (!system.IsMouseValid) return;
            Vector2Int currentPos = system.CurrentMouseGridPos;

            if (system.WasClickPressed)
            {
                if (!isBuilding)
                {
                    // 第一次点击：确定起点
                    isBuilding = true;
                    startPos = currentPos;
                }
                else
                {
                    // 第二次点击：确定终点并建造
                    if (currentPos != startPos)
                    {
                        GameEventBus.RequestBeltBuild(startPos, currentPos, isAlternatePath);
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
                context.PreviewSystem.UpdateBeltPreview(startPos, currentPos, isAlternatePath);
            }
            else
            {
                context.PreviewSystem.UpdateBeltPreview(currentPos, currentPos, false);
            }
        }
        
        public override void Exit()
        {
            base.Exit();
            isBuilding = false;
        }
    }
}