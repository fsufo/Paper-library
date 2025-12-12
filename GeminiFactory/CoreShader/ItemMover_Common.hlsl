// --- Constants ---
#define BELT_NONE 0
#define BELT_UP 1
#define BELT_DOWN 2
#define BELT_LEFT 3
#define BELT_RIGHT 4

// 输入口 ID (11-14)
#define BELT_INPUT_UP 11
#define BELT_INPUT_DOWN 12
#define BELT_INPUT_LEFT 13
#define BELT_INPUT_RIGHT 14

// 输出口 ID (15-18)
#define BELT_OUTPUT_UP 15
#define BELT_OUTPUT_DOWN 16
#define BELT_OUTPUT_LEFT 17
#define BELT_OUTPUT_RIGHT 18

// 分流器 ID
#define ID_SPLITTER 600

#define THREAD_GROUP_SIZE_X 64
#define THREAD_GROUP_SIZE_GRID 256
#define FIXED_TIME_STEP 0.0166667

// --- Data Structures ---
struct ItemData
{
    float2 position;
    float2 velocity;
    float4 color;
    int isActive;
    int price;
    int itemID;
    int extraData; // [Splitter State] High 4 bits: Preferred Dir, Low 28 bits: GridIndex
};

struct MapCell
{
    int type;
    int filterID;
    int reserved1; // [Splitter Counter] Ticket Issuer
    int reserved2;
};

// --- Buffers ---
RWStructuredBuffer<ItemData> items;
RWStructuredBuffer<MapCell> MapGrid;
RWStructuredBuffer<int> GridOccupancy;
RWStructuredBuffer<int> GlobalStats; // [0] = Money

// --- Uniforms ---
int mapWidth;
int mapHeight;
float deltaTime;
float moveSpeed;
int maxItems;

// 删除相关参数
float2 deleteCenter;
float deleteRadius;

// --- Helper Functions ---
float2 GetBeltDirection(int beltValue)
{
    if (beltValue == BELT_UP || beltValue == BELT_INPUT_UP || beltValue == BELT_OUTPUT_UP) return float2(0, 1);
    if (beltValue == BELT_DOWN || beltValue == BELT_INPUT_DOWN || beltValue == BELT_OUTPUT_DOWN) return float2(0, -1);
    if (beltValue == BELT_LEFT || beltValue == BELT_INPUT_LEFT || beltValue == BELT_OUTPUT_LEFT) return float2(-1, 0);
    if (beltValue == BELT_RIGHT || beltValue == BELT_INPUT_RIGHT || beltValue == BELT_OUTPUT_RIGHT) return float2(1, 0);
    return float2(0, 0);
}

int GetGridIndex(int x, int y)
{
    return y * mapWidth + x;
}

bool IsValidGrid(int x, int y)
{
    return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
}
