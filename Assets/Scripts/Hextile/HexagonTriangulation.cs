using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class HexagonTriangulation : MonoBehaviour {

    // A Point represents a location on the hexagon
    public struct Point {
        public float[] location;  // The physical transform of this Point on the game map
        public int age;  // In which iteration was this Point created
        public List<Point> opp_correspondance;  // The opposite Point or Points of this point
        public Point[] neighbors;  // The two Points this Point cut their edge in half
        public string neighb_hextile_dir;  // The direction for the Hextile that this Point is neighboring to -- only for outer edge Points

        public override bool Equals(object obj)
        {
            if (!(obj is Point))
            {
                return false;
            }

            var point = (Point)obj;
            return EqualityComparer<float[]>.Default.Equals(location, point.location) &&
                   age == point.age &&
                   EqualityComparer<List<Point>>.Default.Equals(opp_correspondance, point.opp_correspondance) &&
                   EqualityComparer<Point[]>.Default.Equals(neighbors, point.neighbors) &&
                   neighb_hextile_dir == point.neighb_hextile_dir;
        }

        public override int GetHashCode()
        {
            var hashCode = -1146155129;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<float[]>.Default.GetHashCode(location);
            hashCode = hashCode * -1521134295 + age.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<List<Point>>.Default.GetHashCode(opp_correspondance);
            hashCode = hashCode * -1521134295 + EqualityComparer<Point[]>.Default.GetHashCode(neighbors);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(neighb_hextile_dir);
            return hashCode;
        }

        public static bool operator ==(Point point1, Point point2)
        {
            if (point1.location.SequenceEqual(point2.location))
                return true;
            return false;
        }

        public static bool operator !=(Point point1, Point point2)
        {
            if (point1.location.SequenceEqual(point2.location))
                return false;
            return true;
        }
    };

    // An array containing the initial Points' data of the hexagon (physical locations)
    private float[,] initial_points_data = new float[,]
    {
        { 4.3f, 5.0f },   // Center Point
        { 4.3f, 10.0f },  // Top Point
        { 8.6f, 7.5f },   // Top right Point
        { 8.6f, 2.5f },   // Bottom right Point
        { 4.3f, 0.0f },   // Bottom Point
        { 0.0f, 2.5f },   // Bottom left Point
        { 0.0f, 7.5f }    // Top left Point
    };

    // Dictionary that categorizes Points based on their age
    public Dictionary<int, List<Point>> unique_points_dict = new Dictionary<int,List<Point>>();

    // The list that contains all Triangles of the hextile
    public List<Point[]> triangles = new List<Point[]>();

    public void TriangulateHexagon(int iterations)
    {
        // Step 1: Create the initial Triangles, creating new Points when needed
        for (int i = 1; i < initial_points_data.GetLength(0); i++)
        {
            Point[] triangle_points = new Point[3];
            // Fetch/Create the three Points that will make up the Triangle and add them to an array
            for (int j = 0; j < 3; j++)
            {
                int index = 0;
                switch (j)
                {
                    case 0:
                        index = 0;
                        break;
                    case 1:
                        index = i;
                        break;
                    case 2:
                        index = (i == initial_points_data.GetLength(0) - 1) ? 1 : i + 1;
                        break;
                }

                // If the point that corresponds to the location pointed by the initial_points_data does exists, fetch it from the dictionary
                // If it doesn't exist, create a new Point and add it to the dictionary
                Point? point = FetchFromDictionary(0, initial_points_data[index, 0], initial_points_data[index, 1]);
                if (point == null)
                {
                    // Create the new Point and add it to the dictionary
                    point = new Point()
                    {
                        location = new float[3] { initial_points_data[index, 0], -1.0f, initial_points_data[index, 1] },
                        age = 0,
                        opp_correspondance = new List<Point>()
                    };
                    if (unique_points_dict.ContainsKey(0))
                        unique_points_dict[0].Add((Point)point);
                    else
                        unique_points_dict.Add(0, new List<Point>() { (Point)point } );
                }

                // Add the Point to the Triangle's array
                triangle_points[j] = (Point)point;
            }

            // Add the triangle created to the list
            triangles.Add(triangle_points);
        }

        // Step 2: Instantiate the correspondance array, which is an array of three arrays of two lists of Points
        // Basically linking the edges (1,2)-(5,4), (2,3)-(6,5) and (3,4)-(1-6)
        List<Point>[,] correspondance_array =
        {
            { new List<Point>() { unique_points_dict[0][1], unique_points_dict[0][2] }, new List<Point>() { unique_points_dict[0][5], unique_points_dict[0][4] } },
            { new List<Point>() { unique_points_dict[0][2], unique_points_dict[0][3] }, new List<Point>() { unique_points_dict[0][6], unique_points_dict[0][5] } },
            { new List<Point>() { unique_points_dict[0][3], unique_points_dict[0][4] }, new List<Point>() { unique_points_dict[0][1], unique_points_dict[0][6] } }
        };

        // Step 3: Divide existing triangles into four new ones (repeat for each iteration)
        for (int it = 0; it < iterations; it++)
        {
            // Create a new list of triangles that will replace the triangles list at the end of each iteration
            List<Point[]> new_triangles = new List<Point[]>();

            // Traverse every triangle and break it down to four new ones
            foreach (Point[] triangle in triangles)
            {
                // Cut the three edges in half and get three new Points
                Point[] midpoints = CreateMidpoints(triangle, it);

                // Search the Point dictionary and replace any midpoint that is already stored in the dictionary
                if (unique_points_dict.ContainsKey(it + 1))
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < unique_points_dict[it + 1].Count; j++)
                        if (unique_points_dict[it + 1][j] == midpoints[i])
                            midpoints[i] = unique_points_dict[it + 1][j];

                // If a midpoint is located in one of the outer edges of the hexagon update the correspondance_array
                for (int i = 0; i < 3; i++)
                    UpdateCorrespondanceArray(ref midpoints[i], ref correspondance_array);

                // Add the new Points to the Points dictionary
                for (int i = 0; i < 3; i++)
                    if (unique_points_dict.ContainsKey(it + 1))
                    {
                        bool is_stored = false;
                        foreach (Point point in unique_points_dict[it + 1])
                            if (point == midpoints[i])
                                is_stored = true;
                        if (!is_stored)
                            unique_points_dict[it + 1].Add(midpoints[i]);
                    }
                    else
                        unique_points_dict.Add(it + 1, new List<Point>() { midpoints[i] });

                // Create four new Triangles with the three new Points and the three old Points
                for (int i = 0; i < 3; i++)
                {
                    // Get sets of two midpoints
                    Point midpoint1 = midpoints[i];
                    Point midpoint2 = midpoints[(i + 1) % 3];

                    // Get the Point that those two midpoints both connect to
                    for (int j = 0; j < 3; j++)
                        if 
                        (
                            (midpoint1.neighbors[0] == triangle[j] || midpoint1.neighbors[1] == triangle[j])
                            &&
                            (midpoint2.neighbors[0] == triangle[j] || midpoint2.neighbors[1] == triangle[j])
                        )
                        {
                            Point[] new_triangle = new Point[3] { triangle[j], midpoint1, midpoint2 };
                            System.Array.Reverse(new_triangle);
                            new_triangles.Add(new_triangle);
                            break;
                        }
                }
                new_triangles.Add(midpoints);
            }

            // Replace the old triangles list with the new one
            triangles = new List<Point[]>(new_triangles);
        }

        // Step 4: Update each Point's correspondance value
        for (int i = 0; i < 3; i++)
        {
            // For every set of two edges, traverse the Points at the same time and connect those two Points
            for (int j = 0; j < correspondance_array[i, 0].Count; j++)
            {
                correspondance_array[i, 0][j].opp_correspondance.Add(correspondance_array[i, 1][j]);
                correspondance_array[i, 1][j].opp_correspondance.Add(correspondance_array[i, 0][j]);
            }
        }
    }

    private Point? FetchFromDictionary(int age, float loc_x, float loc_z)
    {
        if (!unique_points_dict.ContainsKey(age))
            return null;
        foreach (Point point in unique_points_dict[age])
            if (point.location[0] == loc_x && point.location[2] == loc_z)
                return point;
        return null;
    }

    private Point[] CreateMidpoints(Point[] triangle, int iteration)
    {
        Point[] midpoints = new Point[3];

        for (int i = 0; i < 3; i++)
        {
            int index1 = i;
            int index2 = (i + 1) % 3;

            Point point = new Point()
            {
                location = new float[3] { (triangle[index1].location[0] + triangle[index2].location[0]) / 2, -1.0f, (triangle[index1].location[2] + triangle[index2].location[2]) / 2 },
                age = iteration + 1,
                opp_correspondance = new List<Point>(),
                neighbors = new Point[2] { triangle[index1], triangle[index2] }
            };

            midpoints[i] = point;
        }

        return midpoints;
    }

    private void UpdateCorrespondanceArray(ref Point point, ref List<Point>[,] correspondance_array)
    {
        string[] opposite_edge_tags = new string[6] { "top_right", "bottom_left", "right", "left", "bottom_right", "top_left" };
        // Traverse the three entries of the correspondance array -- these entries store two lists of Points
        for (int i = 0; i < 3; i++)
        {
            // Traverse each list of Points
            for (int j = 0; j < 2; j++)
            {
                // Traverse each Point on the list
                for (int k = 0; k < correspondance_array[i, j].Count - 1; k++)
                {
                    Point left_point = correspondance_array[i, j][k];
                    Point right_point = correspondance_array[i, j][k + 1];
                    if (point.neighbors[0] == left_point && point.neighbors[1] == right_point || point.neighbors[1] == left_point && point.neighbors[0] == right_point)
                    {
                        // Tag the Point's edge
                        point.neighb_hextile_dir = opposite_edge_tags[i * 2 + j];
                        // Add the Point to the correspondance array
                        correspondance_array[i, j].Insert(k + 1, point);
                    }
                }
            }
        }
    }
}
