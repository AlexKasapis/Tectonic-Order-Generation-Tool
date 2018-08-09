using System.Collections;
using System.Collections.Generic;
using UnityEngine;




public class Hextile : MonoBehaviour
{
    // Mesh Information. This should be dynamic but for now it is what it is.
    public Vector3[] local_vertices;
    public int[] local_triangles;
    public Vector2[] local_uvs;

    //Location on the map
    public int row;
    public int col;

    // World dimensions
    private float hextile_eff_width = 8.6f;
    private float hextile_eff_height = 10f * 0.75f;
    private float odd_hextile_offset = 8.6f / 2;

    // Morphological details of the hextile
    public Plate plate;
    public GeoType geo_type;
    public int height_01;

    // Objects associated with this hextile
    public GameObject hextile_object;
    public GameObject vector_object;

    public bool exposed_asthenosphere = false;

    public void InitializeHextile(int row, int col, Plate plate, int height_01)
    {
        local_vertices = HexagonTriangulation.GetVerticesArray();
        local_triangles = HexagonTriangulation.GetTrianglesArray();
        local_uvs = HexagonTriangulation.GetUVArray();

        this.row = row;
        this.col = col;
        this.plate = plate;
        this.height_01 = height_01;
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
                hextile_object.GetComponent<MeshRenderer>().material = GeoType.elevation_mats[geo_type.geo_elevation];
                break;
            case "height":
                hextile_object.GetComponent<MeshRenderer>().material = TectonicOrder.height_mats[height_01 / 10];
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

    public void CalculateFinalMesh()
    {
        // Set the center vertex height according to the geo_type of the hextile
        local_vertices[2].y = FluctuateHeight(geo_type.base_height, 0, geo_type);

        // Set the outer point vertices
        for (int i = 0; i < 7; i++)
        {
            if (i != 2)
            {
                string[][] direction_codes = new string[][]
                {
                    new string[] { "left_up", "right_up" },
                    new string[] { "right_up", "right" },
                    new string[] { "", "" },
                    new string[] { "right", "right_down" },
                    new string[] { "right_down", "left_down" },
                    new string[] { "left_down", "left" },
                    new string[] { "left", "left_up" }
                };

                // Check if the corresponding vertex on the neighboring hextile has already a value
                Hextile hextile_1 = TectonicOrder.GetRelativeHextile(row, col, direction_codes[i][0]);
                Hextile hextile_2 = TectonicOrder.GetRelativeHextile(row, col, direction_codes[i][1]);

                float height = -1.0f;
                if (hextile_1 != null)
                    height = hextile_1.GetHeightOnOppositeVertex(i, direction_codes[i][0]);
                if (hextile_2 != null && height == -1.0f)
                    height = hextile_2.GetHeightOnOppositeVertex(i, direction_codes[i][1]);

                if (height == -1.0f)
                {
                    float combined_height = geo_type.base_height;
                    int count = 1;
                    if (hextile_1 != null)
                    {
                        combined_height += hextile_1.geo_type.base_height;
                        count++;
                    }
                    if (hextile_2 != null)
                    {
                        combined_height += hextile_2.geo_type.base_height;
                        count++;
                    }

                    // Fluctuate the height
                    local_vertices[i].y = Mathf.Max(0.0f, FluctuateHeight(combined_height / count, 0, geo_type));
                }
                else
                {
                    local_vertices[i].y = height;
                }
            }
        }

        // Give values to the rest of the vertices
        for (int i = 1; i < HexagonTriangulation.age_dict.Count; i++)
        {
            // Get the list containing all the points of that specific age
            List<int> age_points = HexagonTriangulation.age_dict[i];

            // Visit all these points and calculate their height value
            foreach (int point_id in age_points)
            {
                // Check if the corresponding vertex on the neighboring hextile has already a value
                Hextile hextile_1 = TectonicOrder.GetRelativeHextile(row, col, HexagonTriangulation.GetEdgeCode(point_id));

                float height = -1.0f;
                if (hextile_1 != null)
                    height = hextile_1.GetHeightOnOppositeVertex(point_id, "");

                if (height == -1.0f)
                {
                    Point point = HexagonTriangulation.points_dict[point_id];
                    float combined_height = local_vertices[point.initial_neighbors.Item1].y + local_vertices[point.initial_neighbors.Item2].y;
                    local_vertices[point_id].y = Mathf.Max(0.0f, FluctuateHeight(combined_height / 2, i, geo_type));
                }
                else
                {
                    local_vertices[point_id].y = height;
                }
            }
        }

        // Apply the updated vertices array as the vertices of the mesh
        local_vertices[2].y = Mathf.Max(local_vertices[2].y, 0.0f);
        hextile_object.GetComponent<MeshFilter>().mesh.vertices = local_vertices;
    }

    private static float FluctuateHeight(float base_height, int point_age, GeoType geo_type)
    {
        // Fluctuation modifier based on the geography type
        float geo_deviation = geo_type.elev_deviation;

        // Fluctuation modifier based on the point's age
        float age_fluct_modifier = 1.0f / Mathf.Max(1, point_age);

        // Calculate the final fluctuation and return
        float fluctuation = Random.Range(-geo_deviation * age_fluct_modifier, geo_deviation * age_fluct_modifier);
        return base_height + fluctuation;
    }

    public float GetHeightOnOppositeVertex(int point_id, string relative_dir)
    {
        // If this hextile has had its vertices' Y values assigned
        if (local_vertices[2].y != -1.0f)
        {
            if (point_id >= 0 && point_id <= 6)
            {
                switch (point_id)
                {
                    case 0:
                        return (relative_dir == "right_up") ? local_vertices[5].y : local_vertices[3].y;
                    case 1:
                        return (relative_dir == "right") ? local_vertices[6].y : local_vertices[5].y;
                    case 3:
                        return (relative_dir == "right") ? local_vertices[5].y : local_vertices[0].y;
                    case 4:
                        return (relative_dir == "left_down") ? local_vertices[1].y : local_vertices[6].y;
                    case 5:
                        return (relative_dir == "left") ? local_vertices[3].y : local_vertices[0].y;
                    case 6:
                        return (relative_dir == "left") ? local_vertices[1].y : local_vertices[4].y;
                    default:
                        return -1.0f;
                }
            }
            else
            {
                int opposite_point = HexagonTriangulation.GetOppositePoint(point_id);
                if (opposite_point != -1)
                    return local_vertices[opposite_point].y;
            }
        }
        return -1.0f;
    }

    public Color GetColor(float height)
    {
        if (height == 0)
            return new Color(0, 150, 255);
        else if (height < 3)
            return new Color(0, 200, 0);
        else if (height < 6)
            return new Color(0, 100, 0);
        else
            return new Color(70, 50, 0);
    }
}
