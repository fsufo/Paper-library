using UnityEngine;
using UnityEditor;
using GeminFactory;
using System.Collections.Generic;

namespace GeminFactory.Editor
{
    [CustomEditor(typeof(BuildingDataSO))]
    public class BuildingDataSOEditor : UnityEditor.Editor
    {
        #region Inspector GUI
        public override void OnInspectorGUI()
        {
            BuildingDataSO data = (BuildingDataSO)target;

            // 1. 绘制默认属性 (除了 inputs 和 outputs，因为我们要自定义绘制)
            serializedObject.Update();
            
            // 获取迭代器并排除特定属性
            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name != "inputs" && prop.name != "outputs" && prop.name != "m_Script")
                    {
                        // 1. Recipe & Inventory: 仅在 Production 类且不是 Shop 时显示
                        if (prop.name == "recipe" || prop.name == "maxInventorySize")
                        {
                            if (data.category == BuildingCategory.Production && data.buildingType != BuildingType.Shop)
                            {
                                EditorGUILayout.PropertyField(prop, true);
                            }
                            continue;
                        }

                        // 2. Auto Sell: 隐藏 (自动管理)
                        if (prop.name == "autoSellItems")
                        {
                            continue;
                        }

                        // 3. Power Props: 仅在 Power 类显示
                        bool isPowerProp = prop.name == "requiresPower" || 
                                           prop.name == "powerConsumption" || 
                                           prop.name == "generatesPower" || 
                                           prop.name == "powerGeneration" || 
                                           prop.name == "powerRadius";

                        if (isPowerProp)
                        {
                            if (data.category == BuildingCategory.Power)
                            {
                                EditorGUILayout.PropertyField(prop, true);
                            }
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(prop, true);
                        }
                    }
                }
                while (prop.NextVisible(false));
            }

            // [新增] 如果是 Splitter，强制设置宽高为 1，并跳过端口配置绘制
            if (data.buildingType == BuildingType.Splitter)
            {
                if (data.width != 1) data.width = 1;
                if (data.height != 1) data.height = 1;
                
                EditorGUILayout.HelpBox("Splitter configuration is handled automatically. No port setup required.", MessageType.Info);
                
                // 清理残留数据 (防止 BuildingSystem 误读)
                if (data.inputs.Count > 0) data.inputs.Clear();
                if (data.outputs.Count > 0) data.outputs.Clear();
                
                // 应用修改并返回，不绘制下面的网格
                if (GUI.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(data);
                }
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Port Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click cells to toggle: Empty -> Input (Green) -> Output (Red)", MessageType.Info);

            // 2. 绘制网格编辑器
            // 确保宽高至少为 1
            if (data.width < 1) data.width = 1;
            if (data.height < 1) data.height = 1;

            // 使用垂直布局绘制行
            // 为了符合直觉 (0,0 在左下角)，我们从上往下绘制时，Y 应该是递减的
            // 例如 3x3:
            // Row 0: (0,2) (1,2) (2,2)
            // Row 1: (0,1) (1,1) (2,1)
            // Row 2: (0,0) (1,0) (2,0)
            
            // 居中显示
            GUILayout.BeginVertical(GUI.skin.box);
            
            for (int y = data.height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // 左侧弹簧
                
                for (int x = 0; x < data.width; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    DrawCell(data, pos);
                }
                
                GUILayout.FlexibleSpace(); // 右侧弹簧
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();

            // 自动管理 Auto Sell 属性
            SerializedProperty autoSellProp = serializedObject.FindProperty("autoSellItems");
            SerializedProperty typeProp = serializedObject.FindProperty("buildingType");
            
            // BuildingType.Shop 的索引通常是 2 (Miner=0, Processor=1, Shop=2, Storage=3)
            // 最好通过名字判断，或者确保枚举顺序不变
            // 这里假设 Shop 是枚举中的一个值
            bool isShop = (BuildingType)typeProp.enumValueIndex == BuildingType.Shop;

            if (autoSellProp.boolValue != isShop)
            {
                autoSellProp.boolValue = isShop;
            }

            // 应用修改
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
            }
        }
        #endregion

        #region Grid Drawing
        void DrawCell(BuildingDataSO data, Vector2Int pos)
        {
            // 0. 检查是否是边缘格子
            bool isEdge = pos.x == 0 || pos.x == data.width - 1 || pos.y == 0 || pos.y == data.height - 1;
            
            // 如果不是边缘，绘制为禁用状态且不可交互
            if (!isEdge)
            {
                GUI.enabled = false;
                GUILayout.Button("", GUILayout.Width(40), GUILayout.Height(40));
                GUI.enabled = true;
                return;
            }

            // 查找当前格子是否在 inputs 或 outputs 中
            int inputIndex = data.inputs.FindIndex(p => p.position == pos);
            int outputIndex = data.outputs.FindIndex(p => p.position == pos);

            bool isInput = inputIndex != -1;
            bool isOutput = outputIndex != -1;
            int direction = 4; // Default

            if (isInput) direction = data.inputs[inputIndex].direction;
            else if (isOutput) direction = data.outputs[outputIndex].direction;

            Color originalColor = GUI.backgroundColor;
            string label = $"{pos.x},{pos.y}";
            string tooltip = $"Pos: {pos}\nL-Click: Toggle Type\nR-Click: Rotate";

            if (isInput)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f); // 浅绿
                label = "IN\n" + GetDirArrow(direction);
                tooltip = "Input Port";
            }
            else if (isOutput)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // 浅红
                label = "OUT\n" + GetDirArrow(direction);
                tooltip = "Output Port";
            }
            else
            {
                GUI.backgroundColor = Color.white;
            }

            // 绘制按钮
            Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(label, tooltip), GUI.skin.button, GUILayout.Width(40), GUILayout.Height(40));
            
            if (GUI.Button(btnRect, new GUIContent(label, tooltip)))
            {
                // 左键点击：切换类型
                if (Event.current.button == 0) 
                {
                    Undo.RecordObject(data, "Toggle Port Type");

                    if (!isInput && !isOutput)
                    {
                        // Empty -> Input
                        // 获取合法的输入方向
                        var validDirs = GetValidDirections(pos, data.width, data.height, true);
                        int defaultDir = validDirs.Count > 0 ? validDirs[0] : 4;
                        data.inputs.Add(new PortData { position = pos, direction = defaultDir });
                    }
                    else if (isInput)
                    {
                        // Input -> Output
                        data.inputs.RemoveAt(inputIndex);
                        // 获取合法的输出方向
                        var validDirs = GetValidDirections(pos, data.width, data.height, false);
                        int defaultDir = validDirs.Count > 0 ? validDirs[0] : 4;
                        data.outputs.Add(new PortData { position = pos, direction = defaultDir });
                    }
                    else if (isOutput)
                    {
                        // Output -> Empty
                        data.outputs.RemoveAt(outputIndex);
                    }
                }
                // 右键点击：旋转方向
                else if (Event.current.button == 1)
                {
                    Undo.RecordObject(data, "Rotate Port");
                    if (isInput)
                    {
                        var port = data.inputs[inputIndex];
                        var validDirs = GetValidDirections(pos, data.width, data.height, true);
                        port.direction = RotateValidDir(port.direction, validDirs);
                        data.inputs[inputIndex] = port;
                    }
                    else if (isOutput)
                    {
                        var port = data.outputs[outputIndex];
                        var validDirs = GetValidDirections(pos, data.width, data.height, false);
                        port.direction = RotateValidDir(port.direction, validDirs);
                        data.outputs[outputIndex] = port;
                    }
                }
            }

            GUI.backgroundColor = originalColor;
        }
        #endregion

        #region Helpers
        string GetDirArrow(int dir)
        {
            switch (dir)
            {
                case 1: return "▲"; // Up
                case 2: return "▼"; // Down
                case 3: return "◄"; // Left
                case 4: return "►"; // Right
                default: return "";
            }
        }

        // 获取该位置允许的方向列表
        List<int> GetValidDirections(Vector2Int pos, int width, int height, bool isInput)
        {
            List<int> validDirs = new List<int>();
            
            // 1=Up, 2=Down, 3=Left, 4=Right
            
            // 左边缘 (x=0)
            if (pos.x == 0) 
            {
                if (isInput) validDirs.Add(4); // 向右进
                else validDirs.Add(3);         // 向左出
            }
            // 右边缘 (x=w-1)
            if (pos.x == width - 1)
            {
                if (isInput) validDirs.Add(3); // 向左进
                else validDirs.Add(4);         // 向右出
            }
            // 下边缘 (y=0)
            if (pos.y == 0)
            {
                if (isInput) validDirs.Add(1); // 向上进
                else validDirs.Add(2);         // 向下出
            }
            // 上边缘 (y=h-1)
            if (pos.y == height - 1)
            {
                if (isInput) validDirs.Add(2); // 向下进
                else validDirs.Add(1);         // 向上出
            }
            
            return validDirs;
        }

        int RotateValidDir(int currentDir, List<int> validDirs)
        {
            if (validDirs == null || validDirs.Count == 0) return currentDir;
            
            int index = validDirs.IndexOf(currentDir);
            if (index == -1) return validDirs[0]; // 如果当前方向非法，重置为第一个合法方向
            
            // 循环切换
            index = (index + 1) % validDirs.Count;
            return validDirs[index];
        }
        #endregion
    }
}