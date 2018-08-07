using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;


/*
 * A Point object represents a point on the hexagon
 */
struct Point
{
    // Unique identifier for the point
    public int point_id;

    // Physical location of the point and uv correspondence
    public float mesh_x;
    public float mesh_y;
    public float uv_x;
    public float uv_y;
    
    // A dictionary that contains points neighboring to this point in the format: {k: point_id, v: point_age}
    public Dictionary<int, int> neighbor_ids;

    // The age of the point corresponds to the iteration of which it was created
    public int age;
};

public static class HexagonTriangulation {

    // An array containing the initial points' data of the hexagon (physical locations)
    private static Tuple<float, float>[] initial_points_data = new Tuple<float, float>[]
    {
        new Tuple<float, float>(4.3f, 10.0f),
        new Tuple<float, float>(8.6f, 7.5f),
        new Tuple<float, float>(4.3f, 5.0f),
        new Tuple<float, float>(8.6f, 2.5f),
        new Tuple<float, float>(4.3f, 0.0f),
        new Tuple<float, float>(0.0f, 2.5f),
        new Tuple<float, float>(0.0f, 7.5f)
    };

    // An array containing the initial edges' data of the hexagon (tuples of point_ids that are connected to each other)
    private static Tuple<int, int>[] initial_edges_data = new Tuple<int, int>[]
    {
        new Tuple<int, int>(0, 1),
        new Tuple<int, int>(1, 2),
        new Tuple<int, int>(2, 0),
        new Tuple<int, int>(2, 3),
        new Tuple<int, int>(1, 3),
        new Tuple<int, int>(3, 4),
        new Tuple<int, int>(4, 2),
        new Tuple<int, int>(4, 5),
        new Tuple<int, int>(5, 2),
        new Tuple<int, int>(5, 6),
        new Tuple<int, int>(6, 2),
        new Tuple<int, int>(6, 0),
    };

    // An array containing the initial triangles of the hexagon
    private static int[][] initial_triangles_data = new int[][]
    {
        new int[] { 0, 1, 2 },
        new int[] { 1, 3, 2 },
        new int[] { 2, 3, 4 },
        new int[] { 2, 4, 5 },
        new int[] { 2, 5, 6 },
        new int[] { 2, 6, 0 }
    };

    // A dictionary that contains all points of the hexagon
    private static Dictionary<int, Point> points_dict = new Dictionary<int, Point>();

    // An array that contains all triangles in the hexagon (only the smallest ones, not the ones that are created by others)
    private static List<int[]> triangles = new List<int[]>();

    // The maximum values for mesh_x and mesh_y of a Point object - helps map to UV values by dividing with that
    private static float max_x = 8.6f;
    private static float max_y = 10.0f;

    // How many dividing iteration to have (resolution of the hexagon goes up, the more iterations we have)
    private static int iterations = 4;


    public static void TriangulateHexagon()
    {
        // Populate the points_dict with the initial points of the hexagon and the triangles list
        Setup();

        for (int it = 0; it < iterations; it++)
        {
            // First step is to visit every current edge on the hexagon and create a point at the middle of each edge
            Dictionary<int, Point> new_points_dict = CutEdges(it);

            // Create edges between the new points created
            new_points_dict = ConnectNewPoints(new_points_dict, it);

            // Dump the new_points_dict to the points_dict
            foreach (KeyValuePair<int, Point> point_entry in new_points_dict)
                points_dict[point_entry.Key] = point_entry.Value;
        }
    }

    /*
     * Takes in the initial_points_data array and creates Point objects populating the points_data dictionary.
     * Takes in the initial_triangles_data array and populates the triangles list.
     */
    private static void Setup()
    {
        // Create the initial Point objects
        for (int index = 0; index < initial_points_data.Length; index++)
        {
            Point point = new Point
            {
                point_id = index,
                mesh_x = initial_points_data[index].Item1,
                mesh_y = initial_points_data[index].Item2,
                uv_x = initial_points_data[index].Item1 / max_x,
                uv_y = initial_points_data[index].Item2 / max_y,
                neighbor_ids = new Dictionary<int, int>(),
                age = 0
            };
            points_dict[index] = point;
        }

        // Update the Point objects with their neighbors
        for (int index = 0; index < initial_edges_data.Length; index++)
        {
            int id_1 = initial_edges_data[index].Item1;
            int id_2 = initial_edges_data[index].Item2;
            points_dict[id_1].neighbor_ids[id_2] = 0;
            points_dict[id_2].neighbor_ids[id_1] = 0;
        }
        
        // Populate the triangles list
        for (int index = 0; index < initial_triangles_data.GetLength(0); index++)
            triangles.Add(initial_triangles_data[index]);
        
    }

    /*
     * This function visits every edge on the hexagon and creates a point at the middle of it.
     * Update points_dict for the new neighbors accordingly.
     * Return all the new points for further computation.
     */
    private static Dictionary<int, Point> CutEdges(int it)
    {
        // We dont want the newly created points stored in the points_dict yet, so store them here
        Dictionary<int, Point> new_points_dict = new Dictionary<int, Point>();

        // Visit all the current points on the hexagon
        foreach (int point_id in new List<int>(points_dict.Keys))
        {
            Point point = points_dict[point_id];

            // For each neighbor of this point
            foreach (int neighbor_id in new List<int>(point.neighbor_ids.Keys))
            {
                // If the edge has not been yet cut (this is true when the age of the neighboring point is equal to the iteration)
                if (point.neighbor_ids[neighbor_id] == it)
                {
                    // Create a new Point object and add it to the new_points_dict
                    Point midpoint = CreateMidpoint(point_id, neighbor_id, points_dict.Count + new_points_dict.Count, it + 1);
                    new_points_dict[points_dict.Count + new_points_dict.Count] = midpoint;

                    // Update the two old points' neighbors
                    points_dict[point_id].neighbor_ids.Remove(neighbor_id);
                    points_dict[point_id].neighbor_ids[midpoint.point_id] = it + 1;

                    points_dict[neighbor_id].neighbor_ids.Remove(point_id);
                    points_dict[neighbor_id].neighbor_ids[midpoint.point_id] = it + 1;
                }
            }
        }

        return new_points_dict;
    }

    /*
     * Creates a new Point object at the middle of an edge, with the correct properties
     */
    private static Point CreateMidpoint(int id_1, int id_2, int new_point_id, int new_point_age)
    {
        // Get the location of the old points
        float x_1 = points_dict[id_1].mesh_x;
        float y_1 = points_dict[id_1].mesh_y;
        float x_2 = points_dict[id_2].mesh_x;
        float y_2 = points_dict[id_2].mesh_y;

        // Get the age of the old points
        int age_1 = points_dict[id_1].age;
        int age_2 = points_dict[id_2].age;

        // Calculate the location of the midpoint
        float new_x = (x_1 + x_2) / 2;
        float new_y = (y_1 + y_2) / 2;

        Dictionary<int, int> neighbs = new Dictionary<int, int>();
        neighbs[id_1] = age_1;
        neighbs[id_2] = age_2;

        Point point = new Point
        {
            point_id = new_point_id,
            mesh_x = new_x,
            mesh_y = new_y,
            uv_x = new_x / max_x,
            uv_y = new_y / max_y,
            neighbor_ids = neighbs,
            age = new_point_age
        };

        return point;
    }

    /*
     * Connects the new points created with edges. A perfect hexagon can be created by 6 equilateral triangles.
     * With every iteration these triangles are split into 4 new ones. We call the old triangle of an iteration
     * the "outer triangle" and the middle of the four new ones the "inner triangle". The inner triangle is created
     * by three of the new points created, while the other three small triangles are created by two of the same three
     * points created along with one old point.
     * We search combinations of three points. For each combination, if their neighbors are the same three old points,
     * that means that this combination of three new points are inside the outer triangle.
     */
    private static Dictionary<int, Point> ConnectNewPoints(Dictionary<int, Point> new_points_dict, int it)
    {
        // Keeps track of the unique  neighbors of a combination of three new points
        Dictionary<int, int> unique_neighbors_count = new Dictionary<int, int>();

        // Get a snapshot of the new_points_dict because we are gonna work on that live
        List<Point> initial_new_points = new List<Point>(new_points_dict.Values);

        // For every combination of three new points, check if they are inside an outer triangle, and if yes, connect them
        for (int i = 0; i < initial_new_points.Count - 2; i++)
        {
            // Get the Point data
            int id_1 = initial_new_points.ElementAt(i).point_id;
            Point point_1 = initial_new_points.ElementAt(i);

            // Update the neighbors_count
            unique_neighbors_count = AddUniqueNeighbors(new Dictionary<int, int>(), point_1, it);

            // Back up the neighbors_count
            Dictionary<int, int> backup_dict_1 = new Dictionary<int, int>(unique_neighbors_count);

            for (int j = i + 1; j < initial_new_points.Count - 1; j++)
            {
                // Get the Point data
                int id_2 = initial_new_points.ElementAt(j).point_id;
                Point point_2 = initial_new_points.ElementAt(j);

                // Make sure to restore the dictionary every time we start a new J iteration
                unique_neighbors_count = new Dictionary<int, int>(backup_dict_1);

                // Update the neighbors_count
                unique_neighbors_count = AddUniqueNeighbors(unique_neighbors_count, point_2, it);

                // Back up the neightbors_count
                Dictionary<int, int> backup_dict_2 = new Dictionary<int, int>(unique_neighbors_count);

                // Count the unique neighbors that appear 2 times as neighbors to the points 1 and 2 in question
                if (CountValidNeighbors(unique_neighbors_count) == 1)
                {
                    for (int k = j + 1; k < initial_new_points.Count; k++)
                    {
                        // Get the Point data
                        int id_3 = initial_new_points.ElementAt(k).point_id;
                        Point point_3 = initial_new_points.ElementAt(k);

                        // Restore the dictionary
                        unique_neighbors_count = new Dictionary<int, int>(backup_dict_2);

                        // Update the neighbors_count
                        unique_neighbors_count = AddUniqueNeighbors(unique_neighbors_count, point_3, it);

                        // Check inner triangle conditions
                        if (CountValidNeighbors(unique_neighbors_count) == 3)
                        {
                            // Update the connections
                            new_points_dict[id_1].neighbor_ids[id_2] = it + 1;
                            new_points_dict[id_1].neighbor_ids[id_3] = it + 1;
                            new_points_dict[id_2].neighbor_ids[id_1] = it + 1;
                            new_points_dict[id_2].neighbor_ids[id_3] = it + 1;
                            new_points_dict[id_3].neighbor_ids[id_1] = it + 1;
                            new_points_dict[id_3].neighbor_ids[id_2] = it + 1;

                            // Update the triangles array
                            // Get the three points that form the outer triangle
                            int[] outer_triangle_points = new int[] { -1, -1, -1 };
                            int index = 0;
                            foreach (KeyValuePair<int, int> neighbor_entry in unique_neighbors_count)
                            {
                                if (neighbor_entry.Value == 2)
                                {
                                    outer_triangle_points[index] = neighbor_entry.Key;
                                    index++;
                                }
                            }
                            UpdateTriangles(new_points_dict, id_1, id_2, id_3, outer_triangle_points);
                        }
                    }
                }

            }
        }

        return new_points_dict;
    }

    /*
     * Takes an existing dictionary of unique_neighbors_count and updates it with the neighbors of the Point argument given.
     */
    private static Dictionary<int, int> AddUniqueNeighbors(Dictionary<int, int> existing_dict, Point point, int it)
    {
        // Go through all neighbors of the Point object
        foreach (KeyValuePair<int, int> neighbor_entry in point.neighbor_ids)
        {
            // Neighbors should be older age so make sure that the key is in the points_dict
            if (points_dict.ContainsKey(neighbor_entry.Key))
            {
                if (points_dict[neighbor_entry.Key].age <= it)
                {
                    if (existing_dict.ContainsKey(neighbor_entry.Key))
                        existing_dict[neighbor_entry.Key] += 1;
                    else
                        existing_dict[neighbor_entry.Key] = 1;
                }
            }
        }

        return existing_dict;
    }

    /*
     * Given the unique_neighbors_count dictionary as an argument, returns how many neighbors appear 2 times.
     */
    private static int CountValidNeighbors(Dictionary<int, int> unique_neighbors_count)
    {
        int count = 0;
        foreach (KeyValuePair<int, int> neighbor_entry in unique_neighbors_count)
            if (neighbor_entry.Value == 2)
                count++;
        return count;
    }

    /*
     * stuff
     */
    private static void UpdateTriangles(Dictionary<int, Point> new_points_dict, int id_1, int id_2, int id_3, int[] outer_points)
    {
        //Debug.Log("Point 1 (" + id_1 + "): " + new_points_dict[id_1].mesh_x + " " + new_points_dict[id_1].mesh_y);
        //Debug.Log("Point 2 (" + id_2 + "): " + new_points_dict[id_2].mesh_x + " " + new_points_dict[id_2].mesh_y);
        //Debug.Log("Point 3 (" + id_3 + "): " + new_points_dict[id_3].mesh_x + " " + new_points_dict[id_3].mesh_y);
        //Debug.Log("");

        // Find and remove the outer triangle from the triangles list
        foreach (int[] triangle in triangles)
        {
            // All the points from the outer_points array are located in the triangle array
            bool all_here = true;

            // For every point in the outer_points array
            for (int i = 0; i < 3; i++)
            {
                // Initially it doesnt exist in the triangles array
                bool exists = false;

                // Check all 3 items in the triangles array
                for (int j = 0; j < 3; j++)
                    if (outer_points[i] == triangle[j])
                        exists = true;

                // If this point doesnt exist, not all points are the same
                if (!exists)
                    all_here = false;
            }

            if (!all_here)
            {
                triangles.Remove(triangle);
                break;
            }
        }

        // Create the three (not the center one) new triangles and add them to the list
        Point[] inner_points = new Point[] { new_points_dict[id_1], new_points_dict[id_2], new_points_dict[id_3] };

        // Create the center triangle and add it to the list
        Point[] new_triangle_points = new Point[] { new_points_dict[id_1], new_points_dict[id_2], new_points_dict[id_3] };
        new_triangle_points = ClockwiseSort(new_triangle_points);
        triangles.Add(new int[] { new_triangle_points[0].point_id, new_triangle_points[1].point_id, new_triangle_points[2].point_id });

        foreach (int outer_point in outer_points)
        {
            // Get the three points that will make up the triangle
            new_triangle_points = new Point[] { points_dict[outer_point], new Point(), new Point() };
            int index = 1;
            foreach (Point inner_point in inner_points)
            {
                bool found = false;
                foreach (KeyValuePair<int, int> neighbor_entry in inner_point.neighbor_ids)
                    if (neighbor_entry.Key == outer_point)
                        found = true;
                if (found)
                {
                    new_triangle_points[index] = inner_point;
                    index++;
                }
            }

            // Sort the points to be in a clockwise order (used for the triangle meshes)
            new_triangle_points = ClockwiseSort(new_triangle_points);
            triangles.Add(new int[] { new_triangle_points[0].point_id, new_triangle_points[1].point_id, new_triangle_points[2].point_id });
        }
    }

    /*
     * stuff
     */
    private static Point[] ClockwiseSort(Point[] point_list)
    {
        // First goes the point with the highest Y value
        int highest_y = 0;
        for (int i = 0; i < point_list.Length; i++)
            if (point_list[i].mesh_y > point_list[highest_y].mesh_y)
                highest_y = i;

        // Then goes the point with the highest X value
        int highest_x = -1;
        for (int i = 0; i < point_list.Length; i++)
            if (i != highest_y && (highest_x == -1 || point_list[i].mesh_x > point_list[highest_x].mesh_x))
                highest_x = i;

        // Then the last one
        int last_one = -1;
        for (int i = 0; i < 3; i++)
            if (i != highest_y && i != highest_x)
                last_one = i;

        point_list = new Point[] { point_list[highest_y], point_list[highest_x], point_list[last_one] };
        return point_list;
    }

    public static Vector3[] GetVerticesArray()
    {
        Vector3[] local_vertices = new Vector3[points_dict.Count];
        for (int i = 0; i < local_vertices.Length; i++)
            local_vertices[i] = new Vector3(points_dict[i].mesh_x, 0.0f, points_dict[i].mesh_y);
        return local_vertices;
    }

    public static int[] GetTrianglesArray()
    {
        int[] local_triangles = new int[triangles.Count * 3];
        int index = 0;
        foreach (int[] triangle in triangles)
        {
            local_triangles[index] = triangle[0];
            local_triangles[index + 1] = triangle[1];
            local_triangles[index + 2] = triangle[2];
            index += 3;
        }
        return local_triangles;
    }

    public static Vector2[] GetUVArray()
    {
        Vector2[] local_uvs = new Vector2[points_dict.Count];
        for (int i = 0; i < local_uvs.Length; i++)
            local_uvs[i] = new Vector2(points_dict[i].uv_x, points_dict[i].uv_y);
        return local_uvs;
    }

}
