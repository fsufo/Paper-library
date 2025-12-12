using UnityEngine;

namespace GeminFactory.Systems.InputStates
{
    public class InspectInputState : InputState
    {
        public InspectInputState(InputSystem system, GameContext ctx) : base(system, ctx) { }

        public override void HandleInput()
        {
            if (system.WasClickPressed)
            {
                // 使用 InputSystem 缓存的鼠标状态
                if (system.IsMouseValid)
                {
                    GameEventBus.RequestInspect(system.CurrentMouseGridPos);
                }
                else
                {
                    GameEventBus.CancelInspect();
                }
            }
        }

        public override void UpdatePreview()
        {
            // 检查模式不需要预览，或者可以显示一个高亮框
            context.PreviewSystem.ClearPreview();
        }
        
        public override void Exit()
        {
            base.Exit();
            GameEventBus.CancelInspect();
        }
    }
}