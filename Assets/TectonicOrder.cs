﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TectonicOrder : MonoBehaviour
{    
    Hextile[,] hextiles;
    Plate[] plates;

    // Generation options - Hidden from the player
    public int initial_plates = 10; // Supports up to 20 plates
    public float plate_min_center_height = 76;
    public float plate_max_center_height = 125;
    public float plate_height_deviation = 10; // If a plate has 100 center height, its tiles can take values from 90 to 110

    // On-Screen information
    public static string view_mode = "plates"; // plates, geography, height
    private bool showing_vectors = false;

    // Materials
    public static Material[] plate_mats;
    public static Dictionary<Geography, Material> geogr_mats;
    public static Material[] height_mats;


    private void Start()
    {
        // Plate materials
        plate_mats = new Material[initial_plates];
        for (int i = 0; i < initial_plates; i++)
        {
            plate_mats[i] = Resources.Load("Materials/Plates/Plate" + i) as Material;
        }

        // Geography materials
        geogr_mats = new Dictionary<Geography, Material>();
        for (int i = 0; i < Geography.GetNames(typeof(Geography)).Length; i++)
        {
            geogr_mats.Add((Geography)i, Resources.Load("Materials/Geography/" + ((Geography)i).ToString()) as Material);
        }

        // Height materials
        height_mats = new Material[8]; // 1-25, 26-50, 51-75, 76-100, 101-125, 126-150, 151-175, 176-200 . 1 -> mariana trench, 200 -> everest
        for (int i = 0; i < 8; i++)
        {
            height_mats[i] = Resources.Load("Materials/Height/Height" + i) as Material;
        }
    }
    
    public void InitializeMap(bool wipe_data)
    {
        // Search hextile and vector objects and delete them
        if (wipe_data)
        {
            showing_vectors = false;
            for (int row = 0; row < hextiles.GetLength(0); row++)
            {
                for (int col = 0; col < hextiles.GetLength(1); col++)
                {
                    Destroy(hextiles[row, col].hextile_object);
                    Destroy(hextiles[row, col].vector_object);
                }
            }
        }

        // Set the seed for the random number generation
        Random.InitState(Options.seed);

        // Initialize the plates
        plates = new Plate[initial_plates];
        for (int i = 0; i < initial_plates; i++)
            plates[i] = new Plate(i);

        /*
         * Emerging centers. Each plate is formed from a center, which is picked randomly. In the beggining,
         * each plate consists of only one hextile, its emerging center. From there, add all the adjacent hextiles
         * to the plate the emerging center belongs to. Keep doing that for every plate, increasing the radius of
         * the plates each loop by 1. If while adding new hextiles to the plate, there is a hextile that already
         * belongs to another plate, ignore that hextile. In the end, all hextiles on the map will belong in a single
         * plate.
         */
        int hextile_rows;
        int hextile_cols;
        switch (Options.map_size)
        {
            case 0:
                hextile_rows = 50;
                hextile_cols = 50;
                break;
            case 1:
                hextile_rows = 150;
                hextile_cols = 150;
                break;
            case 2:
                hextile_rows = 250;
                hextile_cols = 250;
                break;
            default:
                Debug.Log("Map size has value " + Options.map_size + ". Reseting to 0 (50x50)");
                Options.map_size = 0;
                hextile_rows = 50;
                hextile_cols = 50;
                break;
        }
        Tuple<int, int>[] emerging_centers = new Tuple<int, int>[initial_plates];
        for (int plate = 0; plate < initial_plates; plate++)
        {
            // Make sure that the emerging center location is unique for each plate
            start:
            Tuple<int, int> center_loc = new Tuple<int, int>(Random.Range(0, hextile_rows - 1), Random.Range(0, hextile_cols - 1));
            for (int j = 0; j < plate; j++)
                if (center_loc == emerging_centers[j])
                    goto start;
            emerging_centers[plate] = center_loc;
        }

        // Create an empty 2D array of ints, initialized with -1, meaning that the hextile has not been yet assigned to a plate
        int[,] plate_tags = new int[hextile_rows, hextile_cols];
        for (int row = 0; row < plate_tags.GetLength(0); row++)
            for (int col = 0; col < plate_tags.GetLength(1); col++)
                plate_tags[row, col] = -1;

        // Assign plates to hextiles, circling hextiles around their emerging centers
        int diameter = 3;
        while (diameter <= Mathf.Max(hextile_rows, hextile_cols))
        {
            for (int plate = 0; plate < initial_plates; plate++)
            {
                int center_row = emerging_centers[plate].Item1;
                int center_col = emerging_centers[plate].Item2;

                // Calculate the circle path. Note that rows are clamped while columns loop
                int starting_row = Mathf.Clamp(center_row - (int)diameter / 2, 0, hextile_rows - 1);
                int ending_row = Mathf.Clamp(center_row + (int)diameter / 2, 0, hextile_rows - 1);
                int starting_col = center_col - (int)diameter / 2;
                int ending_col = center_col + (int)diameter / 2;

                for (int row = starting_row; row <= ending_row; row++)
                    for (int col = starting_col; col <= ending_col; col++)
                        if (plate_tags[row, (col < 0) ? hextile_cols + col : (col >= hextile_cols) ? col - hextile_cols : col] == -1)
                            plate_tags[row, (col < 0) ? hextile_cols + col : (col >= hextile_cols) ? col - hextile_cols : col] = plate;
            }
            diameter += 2;
        }

        /*
         * Plate heights. Each plate has a slightly different elevation compared to the others. Each plate has a
         * medium center. All hextiles of this plate will have heights around this medium center. The deviation of
         * heights is the same for all plates.
         */
        float[] center_heights = new float[initial_plates];
        for (int plate = 0; plate < initial_plates; plate++)
            center_heights[plate] = Random.Range(plate_min_center_height, plate_max_center_height);

        /*
         * Hextiles array. This is the array that holds all information about the hextiles. Each cell is a Hextile object.
         * Instantiate and fill this array with information about each hextile.
         */
        hextiles = new Hextile[hextile_rows, hextile_cols];
        for (int row = 0; row < hextiles.GetLength(0); row++)
        {
            for (int col = 0; col < hextiles.GetLength(1); col++)
            {
                Plate plate = plates[plate_tags[row, col]];
                float height = Random.Range(center_heights[plate_tags[row, col]] - plate_height_deviation, center_heights[plate_tags[row, col]] + plate_height_deviation);
                Hextile hextile = gameObject.AddComponent<Hextile>();
                hextile.Initialize(row, col, plate, height);

                hextiles[row, col] = hextile;
            }
        }
    }

    public void ChangeViewMode(string mode)
    {
        view_mode = mode;
        for (int row = 0; row < hextiles.GetLength(0); row++)
            for (int col = 0; col < hextiles.GetLength(1); col++)
                hextiles[row, col].TexturizeHextileObject();
    }

    public void UpdateDirectionVectors(bool change_visibility)
    {
        if (change_visibility)
            showing_vectors = !showing_vectors;
        
        for (int row = 0; row < hextiles.GetLength(0); row++)
            for (int col = 0; col < hextiles.GetLength(1); col++)
                hextiles[row, col].SetVectorObjectActivity(showing_vectors);
    }

    public void DoSteps(int step_number)
    {
        /*
         * Every hextile interacts actively with an adjacent hextile (that is the hextile it's moving towards).
         * In order to calculate the interaction of the hextile correctly, we must know how the adjacent hextile will behave during the
         * current time frame. Thus, for each hextile we want to move, we must first move the hextile it is going to interact with, and
         * consequently first move the hextile this hextile is going to interact with. It is convenient to use a recursive algorith for 
         * this approach. In order to avoid endless loops we must have a list of hextiles that we have already moved in this time frame.
         */

        for (int curr_step = 0; curr_step < step_number; curr_step++)
        {
            // This array remembers the hextiles that have been already made their interaction
            // If a hextile has not been moved, the corresponding cell takes the value '0', contrary to the value '1' has it been moved
            // If a hextile has been deleted (in case of convergence) it takes the value '2'
            int[,] moved_flags = new int[hextiles.GetLength(0), hextiles.GetLength(1)];
            for (int row = 0; row < hextiles.GetLength(0); row++)
                for (int col = 0; col < hextiles.GetLength(1); col++)
                    moved_flags[row, col] = 0;

            // For each hextile run the move algorithm, if it has not already been moved
            for (int row = 0; row < hextiles.GetLength(0); row++)
                for (int col = 0; col < hextiles.GetLength(1); col++)
                    RecursiveInteraction(row, col, ref moved_flags);

            // Manage empty and rogue hextiles
            CleanUp();
            
            // At the end of each step rotate the direction vector of each plate by a random amount of degrees
            for (int i = 0; i < initial_plates; i++)
            {
                plates[i].ModifyDirVector(Random.Range(-120, 120), 1);
            }
        }

        // At the end, update the textures and vectors of each hextileq
        ChangeViewMode(view_mode);
        UpdateDirectionVectors(false);        
    }

    private void RecursiveInteraction(int row, int col, ref int[,] moved_flags)
    {
        if (moved_flags[row, col] == 0)
        {
            moved_flags[row, col] = 1;
            int new_row = -1;
            int new_col = -1;
            switch (hextiles[row, col].plate.dir_code)
            {
                case "r":
                    new_row = row;
                    new_col = col + 1;
                    if (new_col >= hextiles.GetLength(1))
                    {
                        new_col = 0;
                    }
                    break;
                case "ru":
                    if (row % 2 == 0)
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextiles.GetLength(0))
                        {
                            new_row = row;
                        }
                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col + 1;
                        if (new_row >= hextiles.GetLength(0))
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col >= hextiles.GetLength(1))
                        {
                            new_col = 0;
                        }
                    }
                    break;
                case "lu":
                    if (row % 2 == 0)
                    {
                        new_row = row + 1;
                        new_col = col - 1;
                        if (new_row >= hextiles.GetLength(0))
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col < 0)
                        {
                            new_col = hextiles.GetLength(1) - 1;
                        }

                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextiles.GetLength(0))
                        {
                            new_row = row;
                        }
                    }
                    break;
                case "l":
                    new_row = row;
                    new_col = col - 1;
                    if (new_col < 0)
                    {
                        new_col = hextiles.GetLength(1) - 1;
                    }
                    break;
                case "ld":
                    if (row % 2 == 0)
                    {
                        new_row = row - 1;
                        new_col = col - 1;
                        if (new_row < 0)
                        {
                            new_row = row;
                        }
                        if (new_col < 0)
                        {
                            new_col = hextiles.GetLength(1) - 1;
                        }
                    }
                    else
                    {
                        new_row = row - 1;
                        new_col = col;
                        if (new_row < 0)
                        {
                            new_row = row;
                        }
                    }
                    break;
                case "rd":
                    if (row % 2 == 0)
                    {
                        new_row = row - 1;
                        new_col = col;
                        if (new_row < 0)
                        {
                            new_row = row;
                        }
                    }
                    else
                    {
                        new_row = row - 1;
                        new_col = col + 1;
                        if (new_row < 0)
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col >= hextiles.GetLength(1))
                        {
                            new_col = 0;
                        }
                    }
                    break;
                default:
                    Debug.Log("Invalid direction code: " + hextiles[row, col].plate.dir_code + ". You done fucked up.");
                    break;
            }
            //Debug.Log(row + "," + col + " points to " + new_row + "," + new_col + " with " + plate_dirs[row, col]);
            RecursiveInteraction(new_row, new_col, ref moved_flags);
            if (moved_flags[row, col] != 2)
            {
                MoveHextile(row, col, new_row, new_col, ref moved_flags);
            }
        }
    }

    private void MoveHextile(int row, int col, int new_row, int new_col, ref int[,] moved_flags)
    {
        // If the hextile at the destination has moved from there, it's free real estate.
        if (hextiles[new_row, new_col].exposed_asthenosphere == true)
        {
            // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
            hextiles[new_row, new_col].plate = hextiles[row, col].plate;
            hextiles[new_row, new_col].height = hextiles[row, col].height;
            hextiles[new_row, new_col].exposed_asthenosphere = false;

            // Update the old hextile
            hextiles[row, col].exposed_asthenosphere = true;

            // Signal that the destination hextile has been eaten alive
            moved_flags[new_row, new_col] = 2;
        }
        else
        {
            // In order to find out which type of interaction to have, get the movement destination of both hextiles
            string[] move_codes = { "r", "ru", "lu", "l", "ld", "rd" };
            // Consider the move codes in a circle. If the move codes of both hextiles belong in the same hemisphere
            // we have thrust, else we have convergence
            int curr_code_pos = 0;
            while (hextiles[row, col].plate.dir_code != move_codes[curr_code_pos])
            {
                curr_code_pos++;
            }
            int dest_code_pos = 0;
            while (hextiles[new_row, new_col].plate.dir_code != move_codes[dest_code_pos])
            {
                dest_code_pos++;
            }

            int diff = Mathf.Abs(dest_code_pos - curr_code_pos);

            if (diff == 0 || diff == 1)
            {
                //// Fairly same direction -> Thrust: increase the magnitude of the destination vector and the height of both hextiles
                //hextiles[row, col].height += 5;
                //hextiles[new_row, new_col].height += 5;
            }
            else
            {
                /*
                 * Fairly opposide direction -> Convergence: 
                 * If the current hextile is higher than the destination, overlap it, increasing its height.
                 * If the destination hextile is higher, slide the current hextile under the destination, increasing the destination's height.
                 */
                if (hextiles[row, col].height >= hextiles[new_row, new_col].height)
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    hextiles[new_row, new_col].plate = hextiles[row, col].plate;
                    hextiles[new_row, new_col].height = Mathf.Clamp(hextiles[new_row, new_col].height + 5, 1, 200);
                    hextiles[new_row, new_col].exposed_asthenosphere = false;

                    // Update the old hextile
                    hextiles[row, col].exposed_asthenosphere = true;

                    // Signal that the destination hextile has been eaten alive
                    moved_flags[new_row, new_col] = 2;
                }
                else
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    hextiles[new_row, new_col].height = Mathf.Clamp(hextiles[new_row, new_col].height + 5, 1, 200);

                    // Update the old hextile
                    hextiles[row, col].exposed_asthenosphere = true;
                }

            }
        }
    }

    private void CleanUp()
    {
        /*
         * Empty hextiles. Hextiles that have their asthenosphere exposed must be assigned to a plate. Some of the adjacent hextiles will have
         * a direction vector that is moving towards the empty hextile. Pick one of them in a random fashion and assign the empty hextile to the
         * plate of the hextile we picked. If no hextiles are moving towards the empty hextile, assign the old plate of the hextile as the current
         * plate.
         * 
         * Rogue hextiles. Hextiles that have foud themselves surrounded (or almost surrounded) by another plate will be assimilated to said plate.
         * If 4 (except when at the top/bottom edges, where the number is calculated differently) or more of a hextile's adjacent hextiles belong to
         * a single plate, that hextile will now belong to that plate, and its direction vector will be replaced with one of its neighbour hextiles.
         */
        for (int row = 0; row < hextiles.GetLength(0); row++)
        {
            for (int col = 0; col < hextiles.GetLength(1); col++)
            {
                // List of adjacent hextiles moving towards the current hextile
                List<Tuple<int, int>> candidates = new List<Tuple<int, int>>();

                // Array that keeps count of the plates that the adjacent hextiles belong to
                List<Tuple<int, int>>[] adj_plates = new List<Tuple<int, int>>[initial_plates];
                for (int plate = 0; plate < initial_plates; plate++)
                    adj_plates[plate] = new List<Tuple<int, int>>();

                int adj_row, adj_col;

                // Left hextile
                adj_row = row;
                adj_col = (col == 0) ? hextiles.GetLength(1) - 1 : col - 1;
                if (hextiles[adj_row, adj_col].plate.dir_code == "r")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Right hextile
                adj_row = row;
                adj_col = (col == hextiles.GetLength(1) - 1) ? 0 : col + 1;
                if (hextiles[adj_row, adj_col].plate.dir_code == "l")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Top left hextile
                if (row < hextiles.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? hextiles.GetLength(1) - 1 : adj_col;
                    if (hextiles[adj_row, adj_col].plate.dir_code == "rb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Top right hextile
                if (row < hextiles.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == hextiles.GetLength(1)) ? 0 : adj_col;
                    if (hextiles[adj_row, adj_col].plate.dir_code == "lb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom left hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? hextiles.GetLength(1) - 1 : adj_col;
                    if (hextiles[adj_row, adj_col].plate.dir_code == "ru")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom right hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == hextiles.GetLength(1)) ? 0 : adj_col;
                    if (hextiles[adj_row, adj_col].plate.dir_code == "lu")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextiles[adj_row, adj_col].plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                if (hextiles[row, col].exposed_asthenosphere)
                {
                    // Pick one in random
                    Tuple<int, int> adj_hextile;
                    if (candidates.Count > 0)
                        adj_hextile = candidates[Random.Range(0, candidates.Count - 1)];
                    else
                        adj_hextile = new Tuple<int, int>(row, col);
                    Hextile data = hextiles[adj_hextile.Item1, adj_hextile.Item2];
                    hextiles[row, col].plate = data.plate;
                    hextiles[row, col].exposed_asthenosphere = false;
                }

                // Determine if the current hextile is rogue
                int adj_plates_num = 0;
                int highest_plate_index = 0;
                for (int plate = 0; plate < initial_plates; plate++)
                {
                    adj_plates_num += adj_plates[plate].Count;
                    if (adj_plates[plate].Count > adj_plates[highest_plate_index].Count)
                        highest_plate_index = plate;
                }

                if (adj_plates[hextiles[row, col].plate.id].Count <= adj_plates_num - 4)
                {
                    // Find a hextile from the adjacent hextiles with plate_id of the highest_plate_num, and assimilate the current hextile to this plate
                    Tuple<int, int> adj_hextile_loc = adj_plates[highest_plate_index][0];
                    Hextile hextile_data = hextiles[adj_hextile_loc.Item1, adj_hextile_loc.Item2];
                    hextiles[row, col].plate = hextile_data.plate;
                }
            }
        }
    }

}