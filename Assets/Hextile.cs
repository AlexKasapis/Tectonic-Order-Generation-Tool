﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Enumeration of geographical terrain types. Each hextile has one geographical type
public enum Geography
{
    Ocean, Inland_Sea, Grassland, Hills, Forest, Jungle, Snow, Desert, Mountains
}

public class Hextile : MonoBehaviour
{
    // Mesh Information
    Vector3[] local_vertices = new Vector3[]
    {
            new Vector3(4.3f, 0, 5),    // Center
            new Vector3(4.3f, 0, 0),    // Bottom
            new Vector3(0, 0, 2.5f),    // Left bottom
            new Vector3(0, 0, 7.5f),    // Left top
            new Vector3(4.3f, 0, 10),   // Top
            new Vector3(8.6f, 0, 7.5f), // Right top
            new Vector3(8.6f, 0, 2.5f)  // Right bottom
    };
    int[] local_triangles = new int[]
    {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 6,
            0, 6, 1
    };
    Vector2[] local_uvs = new Vector2[]
    {
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0),
            new Vector2(0, 0.25f),
            new Vector2(0, 0.75f),
            new Vector2(0.5f, 1),
            new Vector2(1, 0.75f),
            new Vector2(1, 0.25f)
    };

    //Location on the map
    public int row;
    public int col;

    // World dimensions
    private float hextile_eff_width = 8.6f;
    private float hextile_eff_height = 10f * 0.75f;
    private float odd_hextile_offset = 8.6f / 2;

    // Morphological details of the hextile
    public Plate plate;
    public Geography geo_type;
    public int height;

    // Objects associated with this hextile
    public GameObject hextile_object;
    public GameObject vector_object;

    public bool exposed_asthenosphere = false;


    public void Initialize(int row, int col, Plate plate, int height)
    {
        this.row = row;
        this.col = col;
        this.plate = plate;
        this.height = height;
        CreateHextileObject();
        TexturizeHextileObject();
    }

    private void CreateHextileObject()
    {
        // Create a primitive GameObject with a name
        hextile_object = new GameObject();
        hextile_object.name = "Hextile:" + row + "," + col;

        // Place the hextile_object at the correct location
        float x = (row % 2 == 0) ? col * hextile_eff_width : col * hextile_eff_width + odd_hextile_offset;
        float z = row * hextile_eff_height;
        hextile_object.transform.position = new Vector3(x, 0, z);

        // Handle the Mesh and Rendering components
        hextile_object.AddComponent<MeshFilter>();
        hextile_object.AddComponent<MeshRenderer>();
        hextile_object.AddComponent<MeshCollider>();
        Mesh tile_mesh = hextile_object.GetComponent<MeshFilter>().mesh;
        tile_mesh.vertices = local_vertices;
        tile_mesh.triangles = local_triangles;
        tile_mesh.uv = local_uvs;
        hextile_object.GetComponent<MeshCollider>().sharedMesh = tile_mesh;
    }

    public void TexturizeHextileObject()
    {
        // Add the textured Material, depending on the view mode
        switch (TectonicOrder.view_mode)
        {
            case "plates":
                hextile_object.GetComponent<MeshRenderer>().material = TectonicOrder.plate_mats[plate.id];
                break;
            case "geography":
                hextile_object.GetComponent<MeshRenderer>().material = TectonicOrder.geogr_mats[geo_type];
                break;
            case "height":
                hextile_object.GetComponent<MeshRenderer>().material = TectonicOrder.height_mats[height / 10];
                break;
            default:
                Debug.Log("Invalid mode: " + TectonicOrder.view_mode + ". Defaulting to plates");
                hextile_object.GetComponent<MeshRenderer>().material = TectonicOrder.plate_mats[plate.id];
                break;
        }
    }

    public void SetVectorObjectActivity(bool active)
    {
        if (active == false)
        {
            if (vector_object)
                vector_object.SetActive(false);
        }
        else
        {
            if (!vector_object)
            {
                float x = (row % 2 == 0) ? col * hextile_eff_width + 4.3f : col * hextile_eff_width + odd_hextile_offset + 4.3f;
                float z = row * hextile_eff_height + 5;
                vector_object = Instantiate(Resources.Load("Prefabs/Vector"), new Vector3(x, 0.1f, z), Quaternion.identity) as GameObject;
                vector_object.name = "Vector:" + row + "," + col;
            }
            else
            {
                vector_object.SetActive(true);
            }

            GameObject line_object = vector_object.transform.Find("Line").gameObject;
            line_object.transform.localScale = new Vector3(1, plate.dir_vector.sqrMagnitude * 1.5f, 1);
            line_object.transform.localPosition = new Vector3(plate.dir_vector.sqrMagnitude * 1.5f, 0, 0);

            GameObject tip_object = vector_object.transform.Find("Tip").gameObject;
            tip_object.transform.localPosition = new Vector3(plate.dir_vector.sqrMagnitude * 3, 0, 0);

            // For some reason the positive rotation is reversed
            vector_object.transform.eulerAngles = new Vector3(0, -Plate.CalculateVectorAngle(plate.dir_vector), 0);
        }
    }
}
