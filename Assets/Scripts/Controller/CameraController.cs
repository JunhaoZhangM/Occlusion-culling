using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float xSensitivity = 50f;
    public float ySensitivity = 50f;

    private float moveX = 0f;
    private float moveY = 0f;

    private float mouseX = 0f;
    private float mouseY = 0f;

    private float currentXRotation = 0f;  // 当前的上下旋转角度
    private float currentYRotation = 0f;  // 当前的左右旋转角度

    public float upperLimit = 80f;   // 最大俯仰角度
    public float lowerLimit = -80f;  // 最小俯仰角度

    // Start is called before the first frame update
    void Start()
    {
        // 初始设置：确保鼠标锁定并且不显示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 initialRotation = transform.rotation.eulerAngles;

        currentXRotation = initialRotation.x;
        currentYRotation = initialRotation.y;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 移动输入
        if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01)
        {
            moveX = Input.GetAxis("Horizontal");
        }
        else
        {
            moveX = 0;
        }

        if (Mathf.Abs(Input.GetAxis("Vertical")) > 0.01)
        {
            moveY = Input.GetAxis("Vertical");
        }
        else
        {
            moveY = 0;
        }

        // 计算移动方向
        Vector3 moveDirection = transform.forward * moveY + transform.right * moveX;

        // 按照速度和时间更新物体位置
        Vector3 movement = moveDirection.normalized * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // 鼠标输入
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        // 更新旋转角度（左右旋转：y轴，上下旋转：x轴）
        currentYRotation += mouseX * xSensitivity * Time.deltaTime; // 左右旋转
        currentXRotation -= mouseY * ySensitivity * Time.deltaTime; // 上下旋转

        // 限制上下旋转角度，防止超过80度
        currentXRotation = Mathf.Clamp(currentXRotation, lowerLimit, upperLimit);

        // 应用旋转
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);
    }
}
