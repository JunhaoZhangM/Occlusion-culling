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

    private float currentXRotation = 0f;  // ��ǰ��������ת�Ƕ�
    private float currentYRotation = 0f;  // ��ǰ��������ת�Ƕ�

    public float upperLimit = 80f;   // ������Ƕ�
    public float lowerLimit = -80f;  // ��С�����Ƕ�

    // Start is called before the first frame update
    void Start()
    {
        // ��ʼ���ã�ȷ������������Ҳ���ʾ
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

        // �ƶ�����
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

        // �����ƶ�����
        Vector3 moveDirection = transform.forward * moveY + transform.right * moveX;

        // �����ٶȺ�ʱ���������λ��
        Vector3 movement = moveDirection.normalized * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // �������
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        // ������ת�Ƕȣ�������ת��y�ᣬ������ת��x�ᣩ
        currentYRotation += mouseX * xSensitivity * Time.deltaTime; // ������ת
        currentXRotation -= mouseY * ySensitivity * Time.deltaTime; // ������ת

        // ����������ת�Ƕȣ���ֹ����80��
        currentXRotation = Mathf.Clamp(currentXRotation, lowerLimit, upperLimit);

        // Ӧ����ת
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);
    }
}
