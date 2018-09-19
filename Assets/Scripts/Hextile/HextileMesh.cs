using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HextileMesh : MonoBehaviour {

    // Mesh Information
    public Vector3[] renderer_vertices;
    public int[] renderer_triangles;
    public static Dictionary<int, List<HexagonTriangulation.Point>> unique_points_dict;
    public static List<HexagonTriangulation.Point[]> triangles;
    public Dictionary<int, List<float>> heights_dict;

    // Fetch the data about the hexagon's points from the HexagonTriangulation script
    void Start () {
        HexagonTriangulation hexagon_triangulation = GameObject.Find("__GameManager").GetComponent<HexagonTriangulation>();

        unique_points_dict = new Dictionary<int, List<HexagonTriangulation.Point>>(hexagon_triangulation.unique_points_dict);
        heights_dict = new Dictionary<int, List<float>>();
        foreach (var kvp in unique_points_dict)
        {
            heights_dict.Add(kvp.Key, new List<float>());
            foreach (HexagonTriangulation.Point point in kvp.Value)
                heights_dict[kvp.Key].Add(-1.0f);
        }
        triangles = new List<HexagonTriangulation.Point[]>(hexagon_triangulation.triangles);

        // Prepare the mesh
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshCollider>();
        ResetRendererData();
        gameObject.GetComponent<MeshFilter>().mesh.vertices = renderer_vertices;
        gameObject.GetComponent<MeshFilter>().mesh.triangles = renderer_triangles;
        gameObject.GetComponent<Renderer>().material.shader = Shader.Find("Custom/StandardVertex");

        // Set the color of the mesh
        SetMapmode(TectonicOrder.view_mode);
    }

    private void ResetRendererData()
    {
        // Reset the renderer data
        renderer_vertices = new Vector3[triangles.Count * 3];
        renderer_triangles = new int[triangles.Count * 3];

        // Set the renderer data
        int vertices_counter = 0;
        foreach (HexagonTriangulation.Point[] triangle in triangles)
        {
            for (int i = 0; i < 3; i++)
            {
                // Fetch the Point
                HexagonTriangulation.Point point = triangle[i];

                // Locate the Point in the points_dictionary
                float height = GetHeight(point);
                renderer_vertices[vertices_counter] = new Vector3(point.location[0], height, point.location[2]);
                renderer_triangles[vertices_counter] = vertices_counter;
                vertices_counter += 1;
            }
        }
    }

    public void CalculateMeshHeights()
    {
        HextileManager hextile_manager = gameObject.GetComponent<HextileManager>();

        // Step 1: Set heights for the initial points of the hexagon - the ones with age 0
        heights_dict[0][0] = CalculateHeightValue(gameObject.GetComponent<HextileGeography>().GetBaseHeight(), gameObject.GetComponent<HextileGeography>().GetDeviation(), 0);
        for (int i = 1; i < 7; i++)
        {
            // Get the hextiles that neighbor the current point
            string[,] direction_codes =
            {
                { "top_left", "top_right" },  // Top Point
                { "right", "top_right" },  // Top right Point
                { "right", "bottom_right" },  // Bottom right Point
                { "bottom_left", "bottom_right" },  // Bottom Point
                { "bottom_left", "left" },  // Bottom left Point
                { "left", "top_left" } // Top left Point
            };
            GameObject hextile_1 = TectonicOrder.GetRelativeHextile(hextile_manager.hextile_row, hextile_manager.hextile_col, direction_codes[i - 1, 0]);
            GameObject hextile_2 = TectonicOrder.GetRelativeHextile(hextile_manager.hextile_row, hextile_manager.hextile_col, direction_codes[i - 1, 1]);

            // Check if those hextiles have had their heights assigned
            int[,] opposite_origin_points = { { 3, 5 }, { 6, 4 }, { 5, 1 }, { 2, 6 }, { 1, 3 }, { 2, 4 } };
            float height = -1.0f;
            if (hextile_1 != null)
                height = hextile_1.GetComponent<HextileMesh>().heights_dict[0][opposite_origin_points[i - 1, 0]];
            if (hextile_2 != null && height == -1.0f)
                height = hextile_2.GetComponent<HextileMesh>().heights_dict[0][opposite_origin_points[i - 1, 1]];

            // Set the height for this Point
            if (height == -1.0f)
            {
                float base_height = gameObject.GetComponent<HextileGeography>().GetBaseHeight();
                float deviation = gameObject.GetComponent<HextileGeography>().GetDeviation();
                
                if (hextile_1 != null)
                {
                    if (hextile_1.GetComponent<HextileGeography>().GetBaseHeight() < base_height)
                        base_height = hextile_1.GetComponent<HextileGeography>().GetBaseHeight();
                    if (hextile_1.GetComponent<HextileGeography>().GetDeviation() > deviation)
                        deviation = hextile_1.GetComponent<HextileGeography>().GetDeviation();
                }
                if (hextile_2 != null)
                {
                    if (hextile_2.GetComponent<HextileGeography>().GetBaseHeight() < base_height)
                        base_height = hextile_1.GetComponent<HextileGeography>().GetBaseHeight();
                    if (hextile_2.GetComponent<HextileGeography>().GetDeviation() > deviation)
                        deviation = hextile_1.GetComponent<HextileGeography>().GetDeviation();
                }

                // Fluctuate the height
                heights_dict[0][i] = CalculateHeightValue(base_height, deviation, 0);
            }
            else
            {
                heights_dict[0][i] = height;
            }
        }

        // Step 2: Set heights for all Point of a given age, increasing the age with every step
        for (int age = 1; age < unique_points_dict.Count; age++)
        {
            // Go through every Point of that age and assign a height to it
            for (int i = 0; i < unique_points_dict[age].Count; i++)
            {
                // Get the value of the point - dont modify this, this is not the reference
                HexagonTriangulation.Point point = unique_points_dict[age][i];

                // If the Point is an inner point, calculate the height based on its two neighbors
                if (unique_points_dict[age][i].opp_correspondance.Count == 0)
                {
                    float base_height = Mathf.Clamp(
                        (GetHeight(point.neighbors[0]) + GetHeight(point.neighbors[1])) / 2,
                        0.0f,
                        gameObject.GetComponent<HextileGeography>().GetBaseHeight() + gameObject.GetComponent<HextileGeography>().GetDeviation()
                    );
                    float deviation = gameObject.GetComponent<HextileGeography>().GetDeviation();
                    heights_dict[age][i] = CalculateHeightValue(base_height, deviation, i);
                }
                else
                {
                    // Get the relative Hextile
                    GameObject hextile = TectonicOrder.GetRelativeHextile(hextile_manager.hextile_row, hextile_manager.hextile_col, point.neighb_hextile_dir);

                    // Grab the height of the relative Hextile's Point that touches the current Point
                    float height = -1.0f;
                    if (hextile != null)
                        foreach (HexagonTriangulation.Point a_point in unique_points_dict[point.age])
                            if (a_point == point.opp_correspondance[0])
                                height = hextile.GetComponent<HextileMesh>().GetHeight(a_point);

                    // If that Point has had its height assigned, assign the same value to this Point, else calculate the height based on the Point's neighbors
                    if (height != -1.0f)
                        heights_dict[age][i] = height;
                    else
                    {
                        float base_height = (GetHeight(point.neighbors[0]) + GetHeight(point.neighbors[1])) / 2;
                        float deviation = gameObject.GetComponent<HextileGeography>().GetDeviation();
                        if (hextile != null)
                            if (hextile.GetComponent<HextileGeography>().GetDeviation() > deviation)
                                deviation = hextile.GetComponent<HextileGeography>().GetDeviation();
                        heights_dict[age][i] = CalculateHeightValue(base_height, deviation, i);
                    }
                }
            }
        }

        // Step 3: Recalculate and apply the renderer data
        ResetRendererData();
        gameObject.GetComponent<MeshFilter>().mesh.vertices = renderer_vertices;
        gameObject.GetComponent<MeshFilter>().mesh.triangles = renderer_triangles;
    }

    private float CalculateHeightValue(float base_height, float deviation, int point_age)
    {
        // Fluctuation modifier based on the point's age
        float modifier = 1.0f / Mathf.Pow(Mathf.Max(1, point_age), 2.0f);

        // Calculate the final fluctuation and return
        float fluctuation = Random.Range(-deviation * modifier, deviation * modifier);
        return Mathf.Max(0.0f, base_height + fluctuation);
    }

    public void SetMapmode(string mapmode)
    {
        Color[] colors = new Color[renderer_vertices.Length];
        for (int triangle_index = 0; triangle_index < triangles.Count; triangle_index++)
        {
            Color color;
            switch (TectonicOrder.view_mode)
            {
                case "plates":
                    float plate_num = TectonicOrder.initial_plates;
                    float plate_id = gameObject.GetComponent<HextileGeography>().plate.id;
                    float r = plate_id / plate_num;
                    float g = Mathf.Pow(plate_id, plate_num) % 1.0f;
                    float b = Mathf.Pow(plate_num, plate_id) % 1.0f;
                    color = new Color(r, g, b);
                    break;
                case "geography":
                    // If all three Points are at water level then paint the triangle blue
                    if (renderer_vertices[triangle_index * 3][1] == 0 && renderer_vertices[triangle_index * 3 + 1][1] == 0 && renderer_vertices[triangle_index * 3 + 2][1] == 0)
                        color = new Color(0.1f, 0.4f, 0.8f);
                    else if (renderer_vertices[triangle_index * 3][1] <= 1.2f && renderer_vertices[triangle_index * 3 + 1][1] < 1.2f && renderer_vertices[triangle_index * 3 + 2][1] < 1.2f)
                        color = new Color(0.1f, 0.6f, 0.1f);
                    else if (renderer_vertices[triangle_index * 3][1] <= 3.5f && renderer_vertices[triangle_index * 3 + 1][1] < 3.5f && renderer_vertices[triangle_index * 3 + 2][1] < 3.5f)
                        color = new Color(0.3f, 0.5f, 0.2f);
                    else
                        color = new Color(0.3f, 0.3f, 0.1f);
                    break;
                case "height":
                    float height = (float)gameObject.GetComponent<HextileGeography>().height_01 / 100;
                    color = new Color(height, height, height);
                    break;
                default:
                    color = Color.magenta;
                    break;
            }

            // Set the triangle's color
            colors[triangle_index * 3] = color;
            colors[triangle_index * 3 + 1] = color;
            colors[triangle_index * 3 + 2] = color;
        }
        gameObject.GetComponent<MeshFilter>().mesh.colors = colors;
    }

    public float GetHeight(HexagonTriangulation.Point point)
    {
        for (int index = 0; index < unique_points_dict[point.age].Count; index++)
            if (unique_points_dict[point.age][index] == point)
                return heights_dict[point.age][index];
        return -1;
    }
}
