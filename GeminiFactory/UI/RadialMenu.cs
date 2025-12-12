using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using GeminFactory.UI;
using GeminFactory;
using GeminFactory.Data;

public class RadialMenu : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private RadialMenuDataSO menuData; // 仅用于样式配置

    // 动态注入的数据库
    private BuildingDatabaseSO buildingDatabase;

    public void Initialize(BuildingDatabaseSO db)
    {
        buildingDatabase = db;
        // 强制重新生成菜单
        isGenerated = false;
    }

    [Header("Settings")]
    [SerializeField] private float activationDelay = 0.0f;
    [SerializeField] private float minMouseDistance = 20f;
    [SerializeField] private Sprite nodeBackgroundSprite; // 需要在 Inspector 中赋值一个圆形 Sprite
    [SerializeField] private Sprite lineSprite;           // 需要在 Inspector 中赋值一个细长条 Sprite

    // 内部引用
    private GameObject menuPanel;
    private Text labelText;
    private GeminFactory.InputSystem inputSystem;

    // 内部状态
    private bool isMenuOpen = false;
    private Vector2 startMousePos;
    private float pressTimer = 0;
    
    private List<MenuNode> allNodes = new List<MenuNode>();
    private MenuNode currentHoveredNode = null;
    private bool isGenerated = false;

    private class MenuNode
    {
        public RadialMenuItem item;
        public GameObject gameObject;
        public Image bgImage;
        public Image iconImage;
        public GameObject lineObject;
        public Image lineImage;
        public Vector2 position; // 相对于中心的坐标
        public MenuNode parent;
        public float angle; // 角度 (度)
        public float radius; // 半径
    }

    void Awake()
    {
        InitializeUI();
        GameEventBus.OnGameInitialized += OnGameInitialized;
    }

    void OnDestroy()
    {
        GameEventBus.OnGameInitialized -= OnGameInitialized;
    }

    void OnGameInitialized(GameContext context)
    {
        if (context != null)
        {
            inputSystem = context.InputSystem; // 获取 InputSystem 引用
            if (context.GameConfig != null && context.GameConfig.buildingDatabase != null)
            {
                Initialize(context.GameConfig.buildingDatabase);
            }
        }
    }

    void Start()
    {
        if(menuPanel) menuPanel.SetActive(false);
    }

    void InitializeUI()
    {
        // 1. 查找或创建 Panel
        // 优先查找名为 "Panel" 的子物体
        Transform panelTrans = transform.Find("Panel");
        if (panelTrans != null)
        {
            menuPanel = panelTrans.gameObject;
        }
        else
        {
            // 动态创建
            menuPanel = new GameObject("Panel");
            menuPanel.transform.SetParent(transform, false);
            RectTransform rect = menuPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 2. 查找或创建 Label
        // 优先查找名为 "Label" 的子物体 (在 Panel 内或外)
        Transform labelTrans = transform.Find("Label");
        if (labelTrans == null) labelTrans = menuPanel.transform.Find("Label");

        if (labelTrans != null)
        {
            labelText = labelTrans.GetComponent<Text>();
        }
        else
        {
            // 动态创建
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(menuPanel.transform, false);
            labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 10;
            labelText.resizeTextMaxSize = 30;
            labelText.color = Color.white;
            
            RectTransform rect = labelText.rectTransform;
            rect.sizeDelta = new Vector2(200, 50);
            rect.anchoredPosition = Vector2.zero;
            
            // 添加 Outline 增加可读性
            labelObj.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
        }
    }

    void Update()
    {
        if (inputSystem == null) return;

        // 使用 InputSystem 获取鼠标屏幕坐标
        Vector2 mousePos = inputSystem.GetMouseScreenPosition();

        if (inputSystem.WasRadialMenuPressed)
        {
            pressTimer = 0;
            startMousePos = mousePos;
        }

        if (inputSystem.IsRadialMenuPressed)
        {
            pressTimer += Time.deltaTime;
            if (!isMenuOpen && pressTimer > activationDelay)
            {
                OpenMenu();
            }

            if (isMenuOpen)
            {
                UpdateSelection();
            }
        }

        if (inputSystem.WasRadialMenuReleased)
        {
            if (isMenuOpen)
            {
                ConfirmSelection();
                CloseMenu();
            }
            pressTimer = 0;
        }
    }

    void OpenMenu()
    {
        isMenuOpen = true;
        menuPanel.SetActive(true);
        menuPanel.transform.position = startMousePos;
        
        if (!isGenerated && menuData != null)
        {
            GenerateMenu();
            isGenerated = true;
        }
        
        ResetVisuals();
        if (labelText) labelText.text = "";
    }

    void CloseMenu()
    {
        isMenuOpen = false;
        menuPanel.SetActive(false);
    }

    void GenerateMenu()
    {
        // 清理旧对象 (如果有)
        foreach(Transform child in menuPanel.transform)
        {
            if (child.name != "Label" && child.name != "Background") // 保留 Label 和可能的背景
                Destroy(child.gameObject);
        }
        allNodes.Clear();

        // 如果没有数据库，无法生成菜单
        if (buildingDatabase == null || buildingDatabase.rootNodes == null) return;

        // --- 从数据库构建菜单树 ---
        List<RadialMenuItem> runtimeItems = new List<RadialMenuItem>();
        
        foreach (var nodeDef in buildingDatabase.rootNodes)
        {
            runtimeItems.Add(ConvertNode(nodeDef));
        }

        int count = runtimeItems.Count;
        float angleStep = 360f / count;

        // 生成第一层
        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep; // 0度在右边
            CreateNode(runtimeItems[i], null, angle, menuData.innerRadius);
        }
    }

    /// <summary>
    /// 将数据库节点定义转换为运行时菜单项
    /// </summary>
    RadialMenuItem ConvertNode(MenuNodeDefinition def)
    {
        RadialMenuItem item = new RadialMenuItem
        {
            name = def.name,
            icon = def.icon,
            children = new List<RadialMenuItem>()
        };

        // 设置行为模式
        switch (def.type)
        {
            case MenuItemType.Category:
                item.buildMode = BuildMode.None;
                // 递归处理子节点
                foreach (var childDef in def.children)
                {
                    item.children.Add(ConvertNode(childDef));
                }
                break;

            case MenuItemType.Building:
                if (def.buildingData != null)
                {
                    item.buildingData = def.buildingData;
                    // 根据建筑类型自动判断模式
                    item.buildMode = (def.buildingData.buildingType == BuildingType.Shop) ? BuildMode.Shop : BuildMode.Factory;
                }
                break;

            case MenuItemType.Tool:
                item.buildMode = def.toolMode;
                // 确保工具模式下 buildingData 为空，避免误判
                item.buildingData = null;
                break;
        }

        return item;
    }

    void CreateNode(RadialMenuItem item, MenuNode parent, float angle, float radius)
    {
        // 1. 计算位置
        float rad = angle * Mathf.Deg2Rad;
        Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

        // 2. 创建节点对象
        GameObject nodeObj = new GameObject(item.name);
        nodeObj.transform.SetParent(menuPanel.transform, false);
        RectTransform rect = nodeObj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(menuData.iconSize, menuData.iconSize);

        // 背景
        Image bg = nodeObj.AddComponent<Image>();
        bg.sprite = nodeBackgroundSprite;
        bg.color = menuData.normalColor;

        // 图标
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(nodeObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5, 5);
        iconRect.offsetMax = new Vector2(-5, -5);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = item.icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // 3. 创建连线 (连接到父节点或中心)
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(menuPanel.transform, false);
        lineObj.transform.SetAsFirstSibling(); // 放在底层
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.sprite = lineSprite;
        lineImg.color = menuData.normalColor;
        
        Vector2 startPos = parent != null ? parent.position : Vector2.zero;
        UpdateLine(lineObj.GetComponent<RectTransform>(), startPos, pos, menuData.lineWidth);

        MenuNode node = new MenuNode
        {
            item = item,
            gameObject = nodeObj,
            bgImage = bg,
            iconImage = iconImg,
            lineObject = lineObj,
            lineImage = lineImg,
            position = pos,
            parent = parent,
            angle = angle,
            radius = radius
        };
        allNodes.Add(node);

        // 4. 递归生成子节点
        if (item.children != null && item.children.Count > 0)
        {
            int childCount = item.children.Count;
            // 子节点分布在父节点角度附近的扇区内
            // 例如：父节点在 90度，子节点分布在 60~120度
            float sectorAngle = 60f; 
            float startAngle = angle - sectorAngle / 2f;
            float step = sectorAngle / (childCount + 1); // +1 为了两边留空

            for (int j = 0; j < childCount; j++)
            {
                float childAngle = startAngle + step * (j + 1);
                CreateNode(item.children[j], node, childAngle, menuData.outerRadius);
            }
        }
    }

    void UpdateLine(RectTransform lineRect, Vector2 start, Vector2 end, float width)
    {
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.sizeDelta = new Vector2(dist, width);
        lineRect.anchoredPosition = start + dir * 0.5f;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void UpdateSelection()
    {
        Vector2 currentMouse = inputSystem != null ? inputSystem.GetMouseScreenPosition() : Vector2.zero;
        Vector2 dir = currentMouse - startMousePos;
        float dist = dir.magnitude;

        if (dist < minMouseDistance)
        {
            currentHoveredNode = null;
            if (labelText) labelText.text = "Inspect";
            ResetVisuals();
            return;
        }

        // 寻找最近的节点
        // 优化：根据距离判断层级，再根据角度判断
        // 这里简单遍历所有节点找最近的，对于少量节点（<50）性能足够
        MenuNode nearest = null;
        float minD = float.MaxValue;

        // 将鼠标位置转换为相对于中心的坐标
        // 注意：menuPanel 的中心就是 startMousePos
        // 所以 dir 就是相对于中心的坐标
        
        foreach (var node in allNodes)
        {
            float d = Vector2.Distance(dir, node.position);
            // 增加一个判定半径，只有在节点附近才算选中，或者使用扇区判定
            // 为了更好的体验，我们使用“最近节点”逻辑，但限制最大距离
            if (d < minD)
            {
                minD = d;
                nearest = node;
            }
        }

        // 如果距离节点太远（比如超过 iconSize * 1.5），则不选中
        if (minD > menuData.iconSize * 1.5f)
        {
            // 尝试使用角度判定（对于第一层）
            // 如果鼠标在第一层半径附近
            if (Mathf.Abs(dist - menuData.innerRadius) < menuData.iconSize)
            {
                // ... 可以在这里添加角度吸附逻辑
            }
        }

        if (nearest != currentHoveredNode)
        {
            currentHoveredNode = nearest;
            UpdateVisuals();
        }

        if (currentHoveredNode != null)
        {
            if (labelText) labelText.text = currentHoveredNode.item.name;
        }
        else
        {
            if (labelText) labelText.text = "";
        }
    }

    void ResetVisuals()
    {
        foreach (var node in allNodes)
        {
            node.bgImage.color = menuData.normalColor;
            node.lineImage.color = menuData.normalColor;
            node.lineImage.rectTransform.sizeDelta = new Vector2(node.lineImage.rectTransform.sizeDelta.x, menuData.lineWidth);
            node.gameObject.transform.localScale = Vector3.one;
        }
    }

    void UpdateVisuals()
    {
        ResetVisuals();
        if (currentHoveredNode == null) return;

        // 高亮当前节点及其父节点链
        MenuNode current = currentHoveredNode;
        while (current != null)
        {
            current.bgImage.color = menuData.highlightColor;
            current.lineImage.color = menuData.highlightColor;
            current.lineImage.rectTransform.sizeDelta = new Vector2(current.lineImage.rectTransform.sizeDelta.x, menuData.lineWidth * 2f); // 加粗
            current.gameObject.transform.localScale = Vector3.one * 1.2f; // 放大

            current = current.parent;
        }
    }

    void ConfirmSelection()
    {
        if (currentHoveredNode != null)
        {
            GameEventBus.PublishMenuItemSelected(currentHoveredNode.item);
        }
        else
        {
            GameEventBus.PublishMenuItemSelected(null); // 空模式
        }
    }
}