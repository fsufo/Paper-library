using UnityEngine;

namespace GeminFactory.Systems.InputStates
{
    public class BuildingInputState : InputState
    {
        private BuildingDataSO buildingData;

        public BuildingInputState(InputSystem system, GameContext ctx, BuildingDataSO data) : base(system, ctx) 
        {
            this.buildingData = data;
        }

        public override void HandleInput()
        {
            if (!system.IsMouseValid) return;
            Vector2Int currentPos = system.CurrentMouseGridPos;

            if (system.WasClickPressed)
            {
                // 计算修正后的原点 (使其中心对齐鼠标)
                // 必须与 PreviewSystem.GetBuildingOrigin 逻辑保持一致
                Vector2Int origin = currentPos - new Vector2Int(buildingData.width / 2, buildingData.height / 2);
                
                GameEventBus.RequestBuild(origin, buildingData);
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
            context.PreviewSystem.UpdateBuildingPreview(buildingData, currentPos);
        }
    }
}