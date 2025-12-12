using UnityEngine;

namespace GeminFactory
{
    /// <summary>
    /// 自由漫游相机脚本
    /// 控制方式：
    /// - WASD: 前后左右移动
    /// - Q/E: 下降/上升
    /// - 鼠标右键(按住): 旋转视角
    /// - Shift(按住): 加速移动
    /// - 鼠标滚轮: 调整基础移动速度
    /// </summary>
    public class FreeCamera : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("基础移动速度")]
        public float moveSpeed = 10f;
        
        [Tooltip("按住 Shift 时的加速倍率")]
        public float boostMultiplier = 3f;
        
        [Tooltip("垂直移动速度 (Q/E)")]
        public float climbSpeed = 5f;

        [Header("Rotation Settings")]
        [Tooltip("鼠标灵敏度")]
        public float mouseSensitivity = 2f;
        
        [Tooltip("平滑插值时间")]
        public float smoothTime = 0.1f;

        [Header("Limits")]
        [Tooltip("最小垂直角度")]
        public float minVerticalAngle = -90f;
        
        [Tooltip("最大垂直角度")]
        public float maxVerticalAngle = 90f;

        // 内部状态
        private float rotationX = 0f;
        private float rotationY = 0f;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private bool isRightMouseButtonDown = false;

        void Start()
        {
            // 初始化目标位置和旋转为当前状态
            targetPosition = transform.position;
            targetRotation = transform.rotation;

            Vector3 euler = transform.eulerAngles;
            rotationX = euler.y;
            rotationY = euler.x;
        }

        void Update()
        {
            HandleInput();
            HandleMovement();
            HandleRotation();
        }

        void HandleInput()
        {
            // 鼠标右键控制光标锁定
            if (Input.GetMouseButtonDown(1))
            {
                isRightMouseButtonDown = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isRightMouseButtonDown = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 鼠标滚轮调整速度
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                moveSpeed = Mathf.Clamp(moveSpeed * (1f + scroll * 0.1f), 1f, 100f);
            }
        }

        void HandleMovement()
        {
            float currentSpeed = moveSpeed;
            
            // Shift 加速
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                currentSpeed *= boostMultiplier;
            }

            // 获取输入
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S
            float up = 0f;

            if (Input.GetKey(KeyCode.E)) up = 1f;
            if (Input.GetKey(KeyCode.Q)) up = -1f;

            // 计算移动方向（相对于相机朝向）
            Vector3 direction = transform.forward * v + transform.right * h + Vector3.up * up;
            
            // 如果只想在水平面上移动 WASD，可以取消注释下面这行：
            // direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * v + transform.right * h + Vector3.up * up;

            // 应用移动
            transform.position += direction * currentSpeed * Time.deltaTime;
        }

        void HandleRotation()
        {
            // 只有按住右键时才旋转
            if (!isRightMouseButtonDown) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            rotationX += mouseX;
            rotationY -= mouseY;
            rotationY = Mathf.Clamp(rotationY, minVerticalAngle, maxVerticalAngle);

            Quaternion nextRotation = Quaternion.Euler(rotationY, rotationX, 0);
            
            // 使用 Slerp 进行平滑旋转
            transform.rotation = Quaternion.Slerp(transform.rotation, nextRotation, Time.deltaTime / smoothTime);
            
            // 如果不需要平滑，直接赋值：
            // transform.rotation = nextRotation;
        }
    }
}
