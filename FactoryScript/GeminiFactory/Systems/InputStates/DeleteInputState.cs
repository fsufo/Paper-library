using UnityEngine;

namespace GeminFactory.Systems.InputStates
{
    public class DeleteInputState : InputState
    {
        private float deleteRadius;

        public DeleteInputState(InputSystem system, GameContext ctx, float radius) : base(system, ctx) 
        {
            this.deleteRadius = radius;
        }

        public override void HandleInput()
        {
            if (Input.GetMouseButton(0))
            {
                HandleDeleteOperation();
            }
        }

        public override void UpdatePreview()
        {
            // Delete 需要 WorldPos 来显示圆形指示器
            if (system.IsMouseValid)
            {
                context.PreviewSystem.UpdateDeleteIndicator(true, system.CurrentMouseWorldPos);
            }
            else
            {
                context.PreviewSystem.UpdateDeleteIndicator(false, Vector3.zero);
            }
        }
        
        public override void Exit()
        {
            base.Exit();
            context.PreviewSystem.UpdateDeleteIndicator(false, Vector3.zero);
        }

        private void HandleDeleteOperation()
        {
            if (!system.IsMouseValid) return;
            Vector3 center = system.CurrentMouseWorldPos;
            
            // 1. 删除物品 (GPU)
            context.TransportSystem.DeleteItemsInArea(center, deleteRadius);
            
            // 2. 删除建筑/传送带 (CPU)
            int r = Mathf.CeilToInt(deleteRadius);
            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.z);

            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= deleteRadius)
                    {
                        if (context.MapManager.IsValidGridPosition(new Vector2Int(x, y)))
                        {
                            GameEventBus.RequestDelete(new Vector2Int(x, y));
                        }
                    }
                }
            }
        }
    }
}