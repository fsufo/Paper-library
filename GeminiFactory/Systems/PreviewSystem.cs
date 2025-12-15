using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory
{
    /// <summary>
    /// 预览系统
    /// 负责在建造模式下显示跟随鼠鼠标松开的预览模型（建筑或传送带路径）。
    /// </summary>
    [System.Serializable]
    public class PreviewSystem
    {
        #region Fields & Dependencies
        private Transform previewParent;        // 预览物体的父变换
        private BuildingSystem buildingSystem;  // 建筑系统引用
        private BeltThemeSO beltTheme;          // 传送带主题SO
        private MapManager mapManager;        // 地图管理器

        private List<GameObject> beltPreviewObjects = new List<GameObject>();  // 存储所有生成的传送带预览物体
        private GameObject buildingPreviewObject;  // 当前的建筑预览物体
        private GameObject deleteRangeIndicator;  // 删除范围指示器 (红色圆柱体)
        #endregion

        #region Initialization
        /// <summary>
        /// 初始化预览系统
        /// </summary>
        public void Initialize(GameContext context)
        {
            previewParent = context.PreviewParent;
            buildingSystem = context.BuildingSystem;
            mapManager = context.MapManager;
            
            var config = context.GameConfig;
            beltTheme = config.beltTheme;

            SetupDeleteIndicator(config.deleteRadius, config.deletePreviewMat);
        }

        /// <summary>
        /// 设置删除范围指示器 (红色圆柱体)
        /// </summary>
        private void SetupDeleteIndicator(float radius, Material mat)
        {
            deleteRangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(deleteRangeIndicator.GetComponent<Collider>()); 
            deleteRangeIndicator.name = "DeleteIndicator";
            deleteRangeIndicator.transform.SetParent(previewParent);
            deleteRangeIndicator.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2);
            deleteRangeIndicator.SetActive(false);

            if (mat != null && deleteRangeIndicator.GetComponent<Renderer>())
            {
                deleteRangeIndicator.GetComponent<Renderer>().material = mat;
            }
        }
        #endregion

        #region Preview Updates
        /// <summary>
        /// 更新删除指示器的位置和显示状态
        /// </summary>
        public void UpdateDeleteIndicator(bool isDeleting, Vector3 pos, float radius = 1.0f)
        {
            if (deleteRangeIndicator)
            {
                deleteRangeIndicator.SetActive(isDeleting);
                if (isDeleting) 
                {
                    deleteRangeIndicator.transform.position = pos;
                    deleteRangeIndicator.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2);
                }
            }
        }

        /// <summary>
        /// 更新传送带路径预览
        /// </summary>
        public void UpdateBeltPreview(Vector3Int start, Vector3Int end, bool isAlternatePath)
        {
            // 清理建筑预览
            if (buildingPreviewObject != null) Object.Destroy(buildingPreviewObject);

            // 清理旧的传送带预览
            foreach (var obj in beltPreviewObjects) Object.Destroy(obj);
            beltPreviewObjects.Clear();

            List<Vector3Int> points = buildingSystem.CalculatePathPoints(start, end, isAlternatePath);
            GameObject prefab = beltTheme != null ? beltTheme.beltPrefab : null;
            if (prefab == null) return;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3Int current = points[i];
                
                // Preview Multi-layer Elevator
                if (i < points.Count - 1)
                {
                    Vector3Int next = points[i + 1];
                    if (next.z != current.z)
                    {
                        // 填充中间层预览
                        int step = next.z > current.z ? 1 : -1;
                        int elevatorBase = next.z > current.z ? FactoryConstants.ID_ELEVATOR_UP_BASE : FactoryConstants.ID_ELEVATOR_DOWN_BASE;
                        int dir = buildingSystem.CalculateDirectionForPathIndex(points, i);
                        int type = elevatorBase + dir; // 这里的 type 仅用于决定旋转，预览不区分 ID

                        for (int h = current.z + step; h != next.z; h += step)
                        {
                            Vector3Int midPos = new Vector3Int(current.x, current.y, h);
                            CreatePreviewObject(midPos, dir, true); // true = isElevator
                        }
                        
                        // 也要在当前层生成电梯预览 (替代普通传送带)
                        CreatePreviewObject(current, dir, true);
                        continue; // 跳过默认生成
                    }
                }

                // 检查是否是建筑区域
                if (mapManager != null)
                {
                    int idx = mapManager.GetIndex(current.x, current.y, current.z);
                    if (mapManager.mapCells[idx].type >= FactoryConstants.MIN_BUILDING_ID) continue;
                }

                int direction = buildingSystem.CalculateDirectionForPathIndex(points, i);
                CreatePreviewObject(current, direction, false);
            }
        }
        
        private void CreatePreviewObject(Vector3Int pos, int dir, bool isElevator)
        {
            GameObject prefab = beltTheme != null ? beltTheme.beltPrefab : null;
            if (prefab == null) return;

            Quaternion rot = GetBeltRotation(dir);
            float heightOffset = pos.z * 1.0f;
            Vector3 spawnPos = new Vector3(pos.x, 0.05f + heightOffset, pos.y);
            Vector3 scale = prefab.transform.localScale;

            if (isElevator)
            {
                // 应用电梯视觉逻辑
                // 假设是垂直电梯
                // Up: -90, Down: 90. 但这里我们只知道它是电梯，不知道具体是 Up 还是 Down (除非传入)
                // 简化：预览时统一显示为垂直柱子
                // 为了区分 Up/Down，我们需要更多信息。但通常垂直柱子是对称的。
                // 让我们假设是 Up (或者根据 dir 旋转)
                
                // 注意：PreviewSystem 不知道它是 Up 还是 Down，因为我们只传了 isElevator。
                // 但我们可以推断：如果是在填充循环里，它肯定是垂直连接的一部分。
                
                rot = Quaternion.Euler(-90, 0, 0); // Vertical
                scale = new Vector3(scale.x, 1.0f, scale.z); // Stretch to 1 layer height
                spawnPos.y += 0.5f; // Center alignment
            }

            GameObject p = Object.Instantiate(prefab, spawnPos, rot, previewParent);
            p.transform.localScale = scale;
            beltPreviewObjects.Add(p);
        }

        /// <summary>
        /// 更新建筑预览
        /// </summary>
        public void UpdateBuildingPreview(BuildingDataSO data, Vector2Int mousePos)
        {
            // 清理传送带预览
            foreach (var obj in beltPreviewObjects) Object.Destroy(obj);
            beltPreviewObjects.Clear();

            if (data == null || data.prefab == null) return;

            Vector2Int origin = GetBuildingOrigin(mousePos, data);

            // 如果预览对象不存在，创建它
            if (buildingPreviewObject == null)
            {
                buildingPreviewObject = Object.Instantiate(data.prefab, previewParent);
                // 移除 Collider 以免干扰射线检测
                foreach (var c in buildingPreviewObject.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }

            // 更新位置 (居中对齐)
            float centerX = origin.x + (data.width - 1) * 0.5f;
            float centerZ = origin.y + (data.height - 1) * 0.5f;
            buildingPreviewObject.transform.position = new Vector3(centerX, 0.1f, centerZ);
            buildingPreviewObject.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 清理所有预览对象
        /// </summary>
        public void ClearPreview()
        {
            foreach (var obj in beltPreviewObjects) Object.Destroy(obj);
            beltPreviewObjects.Clear();
            
            if (buildingPreviewObject != null)
            {
                Object.Destroy(buildingPreviewObject);
                buildingPreviewObject = null;
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// 计算建筑的原点坐标 (左下角)，使其中心对齐鼠标位置
        /// </summary>
        public Vector2Int GetBuildingOrigin(Vector2Int mousePos, BuildingDataSO data)
        {
            if (data == null) return mousePos;
            return mousePos - new Vector2Int(data.width / 2, data.height / 2);
        }

        Quaternion GetBeltRotation(int direction)
        {
            float yRot = 0;
            switch (direction)
            {
                case 1: yRot = 0; break;
                case 2: yRot = 180; break;
                case 3: yRot = -90; break;
                case 4: yRot = 90; break;
            }
            return Quaternion.Euler(90, yRot, 0);
        }
        #endregion
    }
}