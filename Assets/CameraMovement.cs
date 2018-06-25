using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour {

    // Zoom
    private float min_zoom = 50;
    private float max_zoom = 1000;
    private float zoom_speed = 1000;

    // Scroll speed
    private float min_speed = 0.1f;
    private float max_speed = 2;

    // Position of cursor at any time
    private float cursor_x;
    private float cursor_y;

    
    void Start () {

        // Setup the camera
        transform.position = new Vector3(215, 400, 190);
        transform.eulerAngles = new Vector3(90, 0, 0);

        cursor_x = Input.mousePosition.x;
        cursor_y = Input.mousePosition.y;
    }
	
	void Update () {
        
        // Zoom
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float zoom_distance = Input.GetAxis("Mouse ScrollWheel") * zoom_speed;
            float new_y = Mathf.Clamp(transform.position.y - zoom_distance, min_zoom, max_zoom);
            transform.position = new Vector3(transform.position.x, new_y, transform.position.z);
        }

        // Scroll (hold middle mouse button)
        if (Input.GetMouseButton(2))
        {
            float perc_zoom = (transform.position.y - min_zoom) / (max_zoom - min_zoom);
            float speed_modifier = min_speed + perc_zoom * (max_speed - min_speed);
            float x_diff = cursor_x - Input.mousePosition.x;
            float z_diff = cursor_y - Input.mousePosition.y;
            Vector3 move_vector = new Vector3(x_diff * speed_modifier, 0, z_diff * speed_modifier);
            transform.position += move_vector;
        }

        // Update cursor position
        cursor_x = Input.mousePosition.x;
        cursor_y = Input.mousePosition.y;
    }
}
