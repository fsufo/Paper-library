using UnityEngine;

namespace GeminFactory.Systems.InputStates
{
    /// <summary>
    /// 输入状态基类
    /// </summary>
    public abstract class InputState
    {
        protected InputSystem system; // Renamed from inputSystem
        protected GameContext context;

        public InputState(InputSystem system, GameContext ctx)
        {
            this.system = system;
            this.context = ctx;
        }

        public virtual void Enter() { }
        public virtual void Exit() 
        {
            context.PreviewSystem.ClearPreview();
        }
        
        public abstract void HandleInput();
        public abstract void UpdatePreview();

        // Helper
        protected bool GetMouseGridPosition(out Vector2Int gridPos)
        {
            return system.GetMouseGridPosition(out gridPos);
        }
    }
}