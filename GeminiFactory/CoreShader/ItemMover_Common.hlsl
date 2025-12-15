// --- Constants ---
#define DIR_NONE 0
#define DIR_UP 1
#define DIR_DOWN 2
#define DIR_LEFT 3
#define DIR_RIGHT 4

// Input Ports (11-14)
#define ID_IN_UP 11
#define ID_IN_DOWN 12
#define ID_IN_LEFT 13
#define ID_IN_RIGHT 14

// Output Ports (15-18)
#define ID_OUT_UP 15
#define ID_OUT_DOWN 16
#define ID_OUT_LEFT 17
#define ID_OUT_RIGHT 18

#define ID_SPLITTER 600

// [New] Layer Constants
#define MAX_LAYERS 4

#define THREAD_GROUP_X 64
#define THREAD_GROUP_GRID 256

// --- Simulation Constants ---
#define CELL_SIZE 1.0
#define CELL_HALF 0.5
// 边界阈值：当物品距离中心超过此值时，尝试跨格
// 留出 0.01 的余量防止浮点误差导致的边界闪烁
#define BOUNDARY_THRESHOLD 0.49 
#define SNAP_DIST 0.001
#define ALIGN_THRESHOLD 0.01
#define SPLITTER_REPICK_DIST_SQ 0.0025

// --- State Bitmasks ---
// State (int) layout:
// Bits 0-3:  Splitter Direction (0-4)
// Bit  4:    HasReservedNext (1 = Reserved)
// Bits 5-31: Splitter Grid Index (Validation)

#define MASK_DIR 0xF
#define FLAG_RESERVED (1 << 4)
#define MASK_INDEX 0xFFFFFFE0

// --- Structs ---
struct ItemData {
    float2 pos;      // Visual Position
    float2 logicPos; // Logical Position
    float4 color;
    int active;
    int price;
    int id;
    int state;
    float height;       // [New] Visual Height
    float targetHeight; // [New] Logic Height (Layer)
};

struct MapCell {
    int type;
    int filter;
    int data;
    int padding;
};

// --- Buffers ---
RWStructuredBuffer<ItemData> Items;
RWStructuredBuffer<MapCell> Map;
RWStructuredBuffer<int> Grid;
RWStructuredBuffer<int> Stats;

// --- Uniforms ---
int Width;
int Height;
float DeltaTime;
float MoveSpeed;
int MaxItems;

float2 DelCenter;
float DelRadius;

// --- Helpers ---
int GetIdx(int x, int y) { return y * Width + x; } // Legacy
int GetIdx(int x, int y, int layer) { return layer * (Width * Height) + y * Width + x; } // 3D Index

bool IsValid(int x, int y) { return x >= 0 && x < Width && y >= 0 && y < Height; }

// [优化] 更紧凑的 GetDir 实现
float2 GetDir(int type) {
    // 归一化 type 到 0-3 (Up, Down, Left, Right)
    // Up: 1, 11, 15 -> 0
    // Down: 2, 12, 16 -> 1
    // Left: 3, 13, 17 -> 2
    // Right: 4, 14, 18 -> 3
    
    int d = -1;
    if (type >= DIR_UP && type <= DIR_RIGHT) d = type - 1;
    else if (type >= ID_IN_UP && type <= ID_IN_RIGHT) d = type - 11;
    else if (type >= ID_OUT_UP && type <= ID_OUT_RIGHT) d = type - 15;
    
    if (d == 0) return float2(0, 1);
    if (d == 1) return float2(0, -1);
    if (d == 2) return float2(-1, 0);
    if (d == 3) return float2(1, 0);
    return float2(0, 0);
}

bool IsBelt(int type) {
    return (type >= DIR_UP && type <= DIR_RIGHT) ||
           (type >= ID_IN_UP && type <= ID_IN_RIGHT) ||
           (type >= ID_OUT_UP && type <= ID_OUT_RIGHT) ||
           (type == ID_SPLITTER);
}