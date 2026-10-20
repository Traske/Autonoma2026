
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Camera")]
    public Transform camera;
    public Vector3 offset = new Vector3(0f, 3f, -7f);
    public bool followYRotation = true;

    [Header("Smooth Follow")]
    public float positionSmoothness = 5f;
    public float rotationSmoothness = 5f;

    [Header("Camera Angle")]
    public float lookHeight = 1f;

    [Header("First Person")]
    public bool firstPerson = false;
    public Vector3 firstPersonOffset = new Vector3(0f, 0.3f, 0.5f);

    [Header("MeetingPoint Lock")]
    public bool lockOntoMeetingPoint = false;
    public Transform meetingPoint;

    // Följer objektet i första eller tredje person.
    private void LateUpdate()
    {
        if (camera == null)
            return;

        if (firstPerson)
        {
            // Kameran följer drönarens riktning.
            Quaternion yawRotation = Quaternion.Euler(
                0f, transform.eulerAngles.y, 0f
            );

            camera.position =
                transform.position +
                yawRotation * firstPersonOffset;

            camera.rotation = yawRotation;

            return;
        }

        Quaternion direction;

        if (lockOntoMeetingPoint && meetingPoint != null)
        {
            Vector3 toTarget =
                meetingPoint.position - transform.position;

            toTarget.y = 0f;

            direction = toTarget.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toTarget)
                : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
        else
        {
            direction = followYRotation
                ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f)
                : Quaternion.identity;
        }

        Vector3 targetPosition =
            transform.position + direction * offset;

        camera.position = Vector3.Lerp(
            camera.position,
            targetPosition,
            Mathf.Clamp01(positionSmoothness * Time.deltaTime)
        );

        Vector3 lookTarget =
            transform.position + Vector3.up * lookHeight;

        Quaternion targetRotation = Quaternion.LookRotation(
            lookTarget - camera.position
        );

        camera.rotation = Quaternion.Slerp(
            camera.rotation,
            targetRotation,
            Mathf.Clamp01(rotationSmoothness * Time.deltaTime)
        );
    }
}
