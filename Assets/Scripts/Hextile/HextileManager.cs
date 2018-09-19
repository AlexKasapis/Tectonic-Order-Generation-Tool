using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HextileManager : MonoBehaviour {
    
    // Location on the map
    public int hextile_row;
    public int hextile_col;

    // World dimensions
    private float hextile_eff_width = 8.6f;
    private float hextile_eff_height = 10f * 0.75f;
    private float odd_hextile_offset = 8.6f / 2;

    public void InitializeHextile(int row, int col, Plate plate, int height_01)
    {
        gameObject.AddComponent<HextileMesh>();
        gameObject.AddComponent<HextileGeography>();

        // Set the information about the Hextile
        hextile_row = row;
        hextile_col = col;

        gameObject.GetComponent<HextileGeography>().plate = plate;
        gameObject.GetComponent<HextileGeography>().height_01 = height_01;

        // Set a name and a position to the gameObject
        gameObject.name = "Hextile:" + row + "," + col;
        float x = (row % 2 == 0) ? col * hextile_eff_width : col * hextile_eff_width + odd_hextile_offset;
        float z = row * hextile_eff_height;
        gameObject.transform.position = new Vector3(x, 0, z);        
    }

    
}
