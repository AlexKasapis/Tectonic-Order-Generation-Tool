using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TectonicOrder : MonoBehaviour
{    
    Hextile[,] hextiles = new Hextile[0, 0];
    Plate[] plates;

    // Generation options - Hidden from the player
    private int initial_plates = 44;
    private int elevation_centers = 25;

    // Steps counter and maximum steps
    public int curr_step = 0;
    public int max_steps = 15;

    // On-Screen information
    public static string view_mode = "plates"; // plates, geography, height
    private bool showing_vectors = false;

    // Materials
    public static Material[] plate_mats;
    public static Dictionary<Geography, Material> geogr_mats;
    public static Material[] height_mats;


    private void Start()
    {
        // Initialize the info labels
        GameObject.Find("StepsLabel").GetComponent<TMPro.TextMeshProUGUI>().text = "Steps: " + curr_step + " / " + max_steps;

        // Make buttons not clickable
        GameObject.Find("Step1Button").GetComponent<UnityEngine.UI.Button>().interactable = false;
        GameObject.Find("FinishGenButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
        GameObject.Find("PlatesModeButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
        GameObject.Find("GeographyModeButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
        GameObject.Find("HeightModeButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
        GameObject.Find("DirectionVectorsButton").GetComponent<UnityEngine.UI.Button>().interactable = false;

        // Plate materials
        plate_mats = new Material[initial_plates];
        for (int i = 0; i < initial_plates; i++)
            plate_mats[i] = Resources.Load("Materials/Plates/Plate" + i) as Material;

        // Geography materials
        geogr_mats = new Dictionary<Geography, Material>();
        for (int i = 0; i < Geography.GetNames(typeof(Geography)).Length; i++)
        {
            geogr_mats.Add((Geography)i, Resources.Load("Materials/Geography/" + ((Geography)i).ToString()) as Material);
        }

        // Height materials
        height_mats = new Material[10];
        for (int i = 0; i < 10; i++)
        {
            height_mats[i] = Resources.Load("Materials/Height/Height" + i) as Material;
        }
    }
    
    public void InitializeMap()
    {
        curr_step = 0;
        GameObject.Find("StepsLabel").GetComponent<TMPro.TextMeshProUGUI>().text = "Steps: " + curr_step + " / " + max_steps;
        GameObject.Find("GeographyModeButton").GetComponent<UnityEngine.UI.Button>().interactable = false;

        // Search hextile and vector objects and delete them
        showing_vectors = false;
        if (hextiles.Length > 0)
            for (int row = 0; row < hextiles.GetLength(0); row++)
            {
                for (int col = 0; col < hextiles.GetLength(1); col++)
                {
                    Destroy(hextiles[row, col].hextile_object);
                    Destroy(hextiles[row, col].vector_object);
                }
            }

        // Set the seed for the random number generation
        Random.InitState(Options.seed);

        // Initialize the Plate objects
        plates = new Plate[initial_plates];
        for (int i = 0; i < initial_plates; i++)
            plates[i] = new Plate(i);

        // Assign plate IDs to each tile
        int hextile_rows = Options.GetMapInfo().Item1;
        int hextile_cols = Options.GetMapInfo().Item2;
        int[,] plate_ids = TectonicHelper.GenerateInitialPlates(hextile_cols, hextile_rows, initial_plates);

        // Calculate the initial height map for the world
        int[,] heightmap = TectonicHelper.GenerateHeightMap(hextile_cols, hextile_rows, elevation_centers);

        // Setup the hextiles array which will hold all information about the world map (along with the plates array)
        hextiles = new Hextile[hextile_rows, hextile_cols];
        for (int row = 0; row < hextiles.GetLength(0); row++)
        {
            for (int col = 0; col < hextiles.GetLength(1); col++)
            {
                Plate plate = plates[plate_ids[row, col]];
                Hextile hextile = gameObject.AddComponent<Hextile>();
                hextile.Initialize(row, col, plate, heightmap[row, col]);

                hextiles[row, col] = hextile;
            }
        }

        // Make buttons clickable
        GameObject.Find("Step1Button").GetComponent<UnityEngine.UI.Button>().interactable = true;
        GameObject.Find("FinishGenButton").GetComponent<UnityEngine.UI.Button>().interactable = true;
        GameObject.Find("PlatesModeButton").GetComponent<UnityEngine.UI.Button>().interactable = true;
        GameObject.Find("HeightModeButton").GetComponent<UnityEngine.UI.Button>().interactable = true;
        GameObject.Find("DirectionVectorsButton").GetComponent<UnityEngine.UI.Button>().interactable = true;

        // Reset the view
        view_mode = "plates";
        ChangeViewMode(view_mode);
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
        for (int step = 0; step < step_number && curr_step < max_steps; step++)
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
                plates[i].ModifyDirVector(Random.Range(-120, 120), 1);

            // Update the current step internally and on the UI
            curr_step++;
            GameObject.Find("StepsLabel").GetComponent<TMPro.TextMeshProUGUI>().text = "Steps: " + curr_step + " / " + max_steps;

            // If we reached the maximum steps, disable the Do1StepButton
            if (curr_step == max_steps)
                GameObject.Find("Step1Button").GetComponent<UnityEngine.UI.Button>().interactable = false;

        }

        // If the FinishGen button was clicked, finish the thing
        if (step_number == 30)
        {
            FinishGeneration();
            GameObject.Find("FinishGenButton").GetComponent<UnityEngine.UI.Button>().interactable = false;
            GameObject.Find("GeographyModeButton").GetComponent<UnityEngine.UI.Button>().interactable = true;
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
                    hextiles[new_row, new_col].height = Mathf.Clamp(hextiles[new_row, new_col].height + 5, 0, 99);
                    hextiles[new_row, new_col].exposed_asthenosphere = false;

                    // Update the old hextile
                    hextiles[row, col].exposed_asthenosphere = true;

                    // Signal that the destination hextile has been eaten alive
                    moved_flags[new_row, new_col] = 2;
                }
                else
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    hextiles[new_row, new_col].height = Mathf.Clamp(hextiles[new_row, new_col].height + 5, 0, 99);

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

    private void FinishGeneration()
    {
        /*
         * Part 1: Land, sea and elevation. Distinguish the sea level based on the water level option.
         * Sort the Hextiles by height and keep only the (100-X)% as land. Then map the height to create realistic hills and mountains.
         */

        // Create a 1D list of all hextiles
        List<Hextile> hextiles_list = new List<Hextile>();

        // Populate the list with all the hextiles
        for (int row = 0; row < hextiles.GetLength(0); row++)
            for (int col = 0; col < hextiles.GetLength(1); col++)
                hextiles_list.Add(hextiles[row, col]);
        
        // Sort the list based on the height of each hextile
        hextiles_list.Sort(SortByHeight);

        // Land is separated in three major categories: low, medium and high elevation
        float high_elevation = 5.0f / 100.0f;
        float medium_elevation = 25.0f / 100.0f;
        int land_hextiles = (int)((float)((100 - Options.GetWaterInfo()) * (hextiles.GetLength(0) * hextiles.GetLength(1))) / 100);
        int high_hextiles = (int)(land_hextiles * high_elevation);
        int medium_hextiles = (int)(land_hextiles * medium_elevation);
        int low_hextiles = land_hextiles - high_hextiles - medium_hextiles;

        // Apply geography
        int count = 0;
        for (int tile = hextiles_list.Count - 1; tile >= 0; tile--)
        {
            int row = hextiles_list[tile].row;
            int col = hextiles_list[tile].col;

            if (count < high_hextiles)
                hextiles[row, col].geo_type = Geography.Mountains;
            else if (count < high_hextiles + medium_hextiles)
                hextiles[row, col].geo_type = Geography.Hills;
            else if (count < high_hextiles + medium_hextiles + low_hextiles)
                hextiles[row, col].geo_type = Geography.Grassland;
            else
                hextiles[row, col].geo_type = Geography.Ocean;

            count++;
        }

        /*
         * Part 2: Eliminate isolated hextiles
         */
        for (int row = 0; row < hextiles.GetLength(0); row++)
        {
            for (int col = 0; col < hextiles.GetLength(1); col++)
            {
                // If the tile is water it will have the value true
                bool is_water = (hextiles[row, col].geo_type == Geography.Ocean || hextiles[row, col].geo_type == Geography.Inland_Sea) ? true : false;

                // Gather the surrounding hextiles (in array: left, right, up_left, up_right, down_left, down_right)
                Hextile[] surr_hextiles = new Hextile[6];

                // Left
                surr_hextiles[0] = (col == 0) ? hextiles[row, hextiles.GetLength(1) - 1] : hextiles[row, col - 1];

                // Right
                surr_hextiles[1] = (col == hextiles.GetLength(1) - 1) ? hextiles[row, 0] : hextiles[row, col + 1];

                // Up side
                if (row != hextiles.GetLength(0) - 1)
                {
                    if (row % 2 == 0)
                    {
                        // Up left
                        surr_hextiles[2] = (col == 0) ? hextiles[row + 1, hextiles.GetLength(1) - 1] : hextiles[row + 1, col - 1];

                        // Up right
                        surr_hextiles[3] = hextiles[row + 1, col];
                    }
                    else
                    {
                        // Up left
                        surr_hextiles[2] = hextiles[row + 1, col];

                        // Up right
                        surr_hextiles[3] = (col == hextiles.GetLength(1) - 1) ? hextiles[row + 1, 0] : hextiles[row + 1, col + 1];
                    }
                }

                // Down side
                if (row != 0)
                {
                    if (row % 2 == 0)
                    {
                        // Down left
                        surr_hextiles[4] = (col == 0) ? hextiles[row - 1, hextiles.GetLength(1) - 1] : hextiles[row - 1, col - 1];

                        // Down right
                        surr_hextiles[5] = hextiles[row - 1, col];
                    }
                    else
                    {
                        // Down left
                        surr_hextiles[4] = hextiles[row - 1, col];

                        // Down right
                        surr_hextiles[5] = (col == hextiles.GetLength(1) - 1) ? hextiles[row - 1, 0] : hextiles[row - 1, col + 1];
                    }
                }

                // If this remains true after the inspection
                bool is_isolated = true;

                for (int index = 0; index < surr_hextiles.Length; index++)
                {
                    if (surr_hextiles[index] != null)
                    {
                        if (is_water && (surr_hextiles[index].geo_type == Geography.Ocean || surr_hextiles[index].geo_type == Geography.Inland_Sea))
                            is_isolated = false;

                        if (!is_water && (surr_hextiles[index].geo_type != Geography.Ocean && surr_hextiles[index].geo_type != Geography.Inland_Sea))
                            is_isolated = false;
                    }
                }

                // Is the tile is isolated, find one non null hextile and copy the height and geo_type to the isolated tile
                if (is_isolated)
                {
                    for (int index = 0; index < surr_hextiles.Length; index++)
                    {
                        if (surr_hextiles[index] != null)
                        {
                            hextiles[row, col].geo_type = surr_hextiles[index].geo_type;
                            hextiles[row, col].height = surr_hextiles[index].height;
                            break;
                        }
                    }
                }
            }
        }
    }

    private int SortByHeight(Hextile h1, Hextile h2)
    {
        return h1.height.CompareTo(h2.height);
    }


}
