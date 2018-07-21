using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * A class that contains helper functions for the generation algorithm
 */
public class TectonicHelper : MonoBehaviour {

    /*
     * Maps cells corresponding to hextiles to plate IDs.
     * tl;dr of the algorithm:
     *  - Generate small plates at random locations
     *  - Generate medium plates at random locations
     *  - Generate large plates at random locations
     *  
     * Each plate is formed from a center, which is picked randomly. In the beggining, each plate consists of 
     * only one tile, its center. From there, add all the adjacent hextiles to the plate the center belongs to.
     * Keep doing that for every plate, increasing the radius of the plates each loop by 1 (until the plate reaches
     * its maximum diameter, or the map has been fully assigned). If while adding new hextiles to the plate, there
     * is a hextile that already belongs to another plate, ignore that hextile. In the end, all hextiles on the map
     * will belong in a single plate.
     */
    public static int[,] GenerateInitialPlates(int width, int height, int plates)
    {
        // Initialize the 2d array with -1 in every cell
        int[,] plate_ids = new int[height, width];
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                plate_ids[row, col] = -1;

        // Percentage of small and medium plates
        float s_plate_perc = 2f / 10f;
        float m_plate_perc = 4f / 10f;

        // Actual number of small and medium plates (the rest are considered large)
        int s_plate_num = (int)(s_plate_perc * plates);
        int m_plate_num = (int)(m_plate_perc * plates);

        // Pick random locations for the plates' centers
        Tuple<int, int>[] plate_centers = PickRandomPositions(width, height, plates);

        // Small and medium plates have diameter limits while large can potentially cover the whole map
        int s_plate_diameter = (int)(Mathf.Min(width, height) * 1f / 10f);
        int m_plate_diameter = (int)(Mathf.Min(width, height) * 3f / 10f);

        // Lay down the plates
        int diameter = 1;
        bool keep_going = true;
        while (keep_going)
        {
            for (int plate = 0; plate < plates; plate++)
            {
                // These variable are used as a simplification on the code, but also as a flag
                int center_row = -1;
                int center_col = -1;
                
                if (plate < s_plate_num && diameter <= s_plate_diameter)
                {
                    center_row = plate_centers[plate].Item1;
                    center_col = plate_centers[plate].Item2;
                }
                else if (plate >= s_plate_num && plate < s_plate_num + m_plate_num && diameter <= m_plate_diameter)
                {
                    center_row = plate_centers[plate].Item1;
                    center_col = plate_centers[plate].Item2;
                }
                else if (plate >= s_plate_num + m_plate_num)
                {
                    center_row = plate_centers[plate].Item1;
                    center_col = plate_centers[plate].Item2;
                }

                // If the diameter is valid with the plate number the variables will have values different of -1
                if (center_row != -1 && center_col != -1)
                {
                    // Find the two edges of the rectangle (rows are clamped while columns loop)
                    int s_row = Mathf.Clamp(center_row - (int)diameter / 2, 0, height - 1);
                    int e_row = Mathf.Clamp(center_row + (int)diameter / 2, 0, height - 1);
                    int s_col = center_col - (int)diameter / 2;
                    int e_col = center_col + (int)diameter / 2;

                    for (int row = s_row; row <= e_row; row++)
                        for (int col = s_col; col <= e_col; col++)
                            if (plate_ids[row, (col < 0) ? width + col : (col >= width) ? col - width : col] == -1)
                                plate_ids[row, (col < 0) ? width + col : (col >= width) ? col - width : col] = plate;
                }
            }

            // Increase the diameter by 2
            diameter += 2;

            // Check if all the tiles in the map have been assigned to a plate
            keep_going = false;
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                    if (plate_ids[row, col] == -1)
                        keep_going = true;
        }

        // Return the 2d array
        return plate_ids;
    }

    /*
     * Elevate areas of the world without taking consideration of the plates. The elevation algorithm follows
     * the same fashion of the plate distribution of the hextiles. This time, we have the height emerging centers
     * and circles around them. These areas have an elevated height compared to the rest of the world.
     */
    public static int[,] GenerateHeightMap(int width, int height, int elevation_centers)
    {
        // Initialize the 2d array with 0 at every cell
        int[,] height_map = new int[height, width];
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                height_map[row, col] = 0;

        Tuple<int, int>[] height_centers = PickRandomPositions(width, height, elevation_centers);

        int diameter = 1;
        while (diameter <= Mathf.Min(height_map.GetLength(0), height_map.GetLength(1)) / 3)
        {
            for (int center = 0; center < height_centers.Length; center++)
            {
                int center_row = height_centers[center].Item1;
                int center_col = height_centers[center].Item2;

                // Calculate the circle path. Note that rows are clamped while columns loop
                int s_row = Mathf.Clamp(center_row - (int)diameter / 2, 0, height - 1);
                int e_row = Mathf.Clamp(center_row + (int)diameter / 2, 0, height - 1);
                int s_col = center_col - (int)diameter / 2;
                int e_col = center_col + (int)diameter / 2;

                for (int row = s_row; row <= e_row; row++)
                    for (int col = s_col; col <= e_col; col++)
                        height_map[row, (col < 0) ? width + col : (col >= width) ? col - width : col] += 1;
            }

            diameter += 2;
        }

        return height_map;
    }

    /*
     * Generates a number of random locations inside a grid with the only requirement that two different
     * location cannot have the same position. The location is a Tuple with values <row, col>.
     */
    public static Tuple<int, int>[] PickRandomPositions(int width, int height, int position_num)
    {
        // Initialize the array that will contain the position values
        Tuple<int, int>[] positions_array = new Tuple<int, int>[position_num];
        for (int index = 0; index < position_num; index++)
        {
            // Make sure that the emerging center location is unique for each plate
            start:
            Tuple<int, int> position = new Tuple<int, int>(Random.Range(0, height - 1), Random.Range(0, width - 1));
            for (int j = 0; j < index; j++)
                if (position == positions_array[j])
                    goto start;
            positions_array[index] = position;
        }

        // Return the array
        return positions_array;
    }

}
