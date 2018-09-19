using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TectonicOrder : MonoBehaviour {

    // Script references
    UIManager ui_manager;
    HexagonTriangulation hexagon_triangulation;
    CameraMovement camera_movement;

    public static GameObject[,] hextile_objects = new GameObject[0, 0];
    Plate[] plates;

    // Generation options - Hidden from the player
    public static int initial_plates;
    private int elevation_centers;

    // Steps counter and maximum steps
    public int curr_step = 0;
    public int max_steps = 15;

    // On-Screen information
    public static string view_mode = "plates"; // plates, geography, height


    private void Start()
    {
        // Get the script references
        ui_manager = GameObject.Find("__GameManager").GetComponent<UIManager>();
        hexagon_triangulation = GameObject.Find("__GameManager").GetComponent<HexagonTriangulation>();
        camera_movement = GameObject.Find("__Camera").GetComponent<CameraMovement>();

        // Initialize the Hextile mesh data
        hexagon_triangulation.TriangulateHexagon(1);
        HextileMesh.unique_points_dict = hexagon_triangulation.unique_points_dict;
        HextileMesh.triangles = hexagon_triangulation.triangles;
    }

    public void InitializeMap()
    {
        // Set values for the number of initial plates and elevation centers based on the size of the map
        // Each size has double the length of its edges so four times the area, so the values are getting multiplied by four
        switch (ui_manager.map_size_value)
        {
            case 0:
                initial_plates = 10;
                elevation_centers = 5;
                break;
            case 1:
                initial_plates = 40;
                elevation_centers = 20;
                break;
            case 2:
                initial_plates = 160;
                elevation_centers = 80;
                break;
        }
        // Set the seed for the random number generation
        Random.InitState(ui_manager.seed_value);

        // Initialize the Plate objects
        plates = new Plate[initial_plates];
        for (int i = 0; i < initial_plates; i++)
            plates[i] = new Plate(i);

        // Assign plate IDs to each tile
        int hextile_rows = ui_manager.GetMapSizeInfo()[1];
        int hextile_cols = ui_manager.GetMapSizeInfo()[0];
        int[,] plate_ids = TectonicHelper.GenerateInitialPlates(hextile_cols, hextile_rows, initial_plates);

        // Calculate the initial height map for the world
        int[,] heightmap = TectonicHelper.GenerateHeightMap(hextile_cols, hextile_rows, elevation_centers);

        // Setup the hextile_objects array which will hold all information about the world map (along with the plates array)
        hextile_objects = new GameObject[hextile_rows, hextile_cols];
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
        {
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
            {
                Plate plate = plates[plate_ids[row, col]];
                GameObject hextile_object = new GameObject();
                hextile_object.AddComponent<HextileManager>();
                hextile_object.GetComponent<HextileManager>().InitializeHextile(row, col, plate, heightmap[row, col]);
                hextile_objects[row, col] = hextile_object;
            }
        }

        // Make control buttons interactable
        ui_manager.EnableStepBtns();
        ui_manager.EnableMapmodeBtn("plates");
        ui_manager.EnableMapmodeBtn("height");

        // Make initialize button and option fields non interactable
        ui_manager.DisableOptions();
        ui_manager.DisableInitializeBtn();

        // Reset the view
        view_mode = "plates";

        // Set the position of the camera
        Vector3 cam_location = new Vector3(hextile_cols * 4.3f, 500 * (ui_manager.map_size_value + 1), hextile_rows * 3.9f);
        camera_movement.PlaceCameraAt(cam_location);
    }

    public void UpdateMapmode(string view)
    {
        view_mode = view;
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
                hextile_objects[row, col].GetComponent<HextileMesh>().SetMapmode(view_mode);
    }

    public void DoSteps(int step_number)
    {
        /*
         * Every hextile interacts actively with an adjacent hextile (that is the hextile it's moving towards).
         * In order to calculate the interaction of the hextile correctly, we must know how the adjacent hextile will behave during the
         * current time frame. Thus, for each hextile we want to move, we must first move the hextile it is going to interact with, and
         * consequently first move the hextile this hextile is going to interact with. It is convenient to use a recursive algorith for 
         * this approach. In order to avoid endless loops we must have a list of hextile_objects that we have already moved in this time frame.
         */
        for (int step = 0; step < step_number && curr_step < max_steps; step++)
        {
            // This array remembers the hextile_objects that have been already made their interaction
            // If a hextile has not been moved, the corresponding cell takes the value '0', contrary to the value '1' has it been moved
            // If a hextile has been deleted (in case of convergence) it takes the value '2'
            int[,] moved_flags = new int[hextile_objects.GetLength(0), hextile_objects.GetLength(1)];
            for (int row = 0; row < hextile_objects.GetLength(0); row++)
                for (int col = 0; col < hextile_objects.GetLength(1); col++)
                    moved_flags[row, col] = 0;

            // For each hextile run the move algorithm, if it has not already been moved
            for (int row = 0; row < hextile_objects.GetLength(0); row++)
                for (int col = 0; col < hextile_objects.GetLength(1); col++)
                    RecursiveInteraction(row, col, ref moved_flags);

            // Manage empty and rogue hextile_objects
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

        // At the end, update each Hextile's mapmode
        UpdateMapmode(view_mode);
    }

    private void RecursiveInteraction(int row, int col, ref int[,] moved_flags)
    {
        if (moved_flags[row, col] == 0)
        {
            moved_flags[row, col] = 1;
            int new_row = -1;
            int new_col = -1;
            switch (hextile_objects[row, col].GetComponent<HextileGeography>().plate.dir_code)
            {
                case "r":
                    new_row = row;
                    new_col = col + 1;
                    if (new_col >= hextile_objects.GetLength(1))
                    {
                        new_col = 0;
                    }
                    break;
                case "ru":
                    if (row % 2 == 0)
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextile_objects.GetLength(0))
                        {
                            new_row = row;
                        }
                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col + 1;
                        if (new_row >= hextile_objects.GetLength(0))
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col >= hextile_objects.GetLength(1))
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
                        if (new_row >= hextile_objects.GetLength(0))
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col < 0)
                        {
                            new_col = hextile_objects.GetLength(1) - 1;
                        }

                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextile_objects.GetLength(0))
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
                        new_col = hextile_objects.GetLength(1) - 1;
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
                            new_col = hextile_objects.GetLength(1) - 1;
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
                        if (new_col >= hextile_objects.GetLength(1))
                        {
                            new_col = 0;
                        }
                    }
                    break;
                default:
                    Debug.Log("Invalid direction code: " + hextile_objects[row, col].GetComponent<HextileGeography>().plate.dir_code + ". You done fucked up.");
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
        if (hextile_objects[new_row, new_col].GetComponent<HextileGeography>().exposed_asthenosphere == true)
        {
            // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
            hextile_objects[new_row, new_col].GetComponent<HextileGeography>().plate = hextile_objects[row, col].GetComponent<HextileGeography>().plate;
            hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01 = hextile_objects[row, col].GetComponent<HextileGeography>().height_01;
            hextile_objects[new_row, new_col].GetComponent<HextileGeography>().exposed_asthenosphere = false;

            // Update the old hextile
            hextile_objects[row, col].GetComponent<HextileGeography>().exposed_asthenosphere = true;

            // Signal that the destination hextile has been eaten alive
            moved_flags[new_row, new_col] = 2;
        }
        else
        {
            // In order to find out which type of interaction to have, get the movement destination of both hextile_objects
            string[] move_codes = { "r", "ru", "lu", "l", "ld", "rd" };
            // Consider the move codes in a circle. If the move codes of both hextile_objects belong in the same hemisphere
            // we have thrust, else we have convergence
            int curr_code_pos = 0;
            while (hextile_objects[row, col].GetComponent<HextileGeography>().plate.dir_code != move_codes[curr_code_pos])
            {
                curr_code_pos++;
            }
            int dest_code_pos = 0;
            while (hextile_objects[new_row, new_col].GetComponent<HextileGeography>().plate.dir_code != move_codes[dest_code_pos])
            {
                dest_code_pos++;
            }

            int diff = Mathf.Abs(dest_code_pos - curr_code_pos);

            if (diff == 0 || diff == 1)
            {
                //// Fairly same direction -> Thrust: increase the magnitude of the destination vector and the height of both hextile_objects
                //hextile_objects[row, col].height += 5;
                //hextile_objects[new_row, new_col].height += 5;
            }
            else
            {
                /*
                 * Fairly opposide direction -> Convergence: 
                 * If the current hextile is higher than the destination, overlap it, increasing its height.
                 * If the destination hextile is higher, slide the current hextile under the destination, increasing the destination's height.
                 */
                if (hextile_objects[row, col].GetComponent<HextileGeography>().height_01 >= hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01)
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    hextile_objects[new_row, new_col].GetComponent<HextileGeography>().plate = hextile_objects[row, col].GetComponent<HextileGeography>().plate;
                    hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01 = Mathf.Clamp(hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01 + 5, 0, 99);
                    hextile_objects[new_row, new_col].GetComponent<HextileGeography>().exposed_asthenosphere = false;

                    // Update the old hextile
                    hextile_objects[row, col].GetComponent<HextileGeography>().exposed_asthenosphere = true;

                    // Signal that the destination hextile has been eaten alive
                    moved_flags[new_row, new_col] = 2;
                }
                else
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01 = Mathf.Clamp(hextile_objects[new_row, new_col].GetComponent<HextileGeography>().height_01 + 5, 0, 99);

                    // Update the old hextile
                    hextile_objects[row, col].GetComponent<HextileGeography>().exposed_asthenosphere = true;
                }

            }
        }
    }

    private void CleanUp()
    {
        /*
         * Empty hextile_objects. hextile_objects that have their asthenosphere exposed must be assigned to a plate. Some of the adjacent hextile_objects will have
         * a direction vector that is moving towards the empty hextile. Pick one of them in a random fashion and assign the empty hextile to the
         * plate of the hextile we picked. If no hextile_objects are moving towards the empty hextile, assign the old plate of the hextile as the current
         * plate.
         * 
         * Rogue hextile_objects. hextile_objects that have foud themselves surrounded (or almost surrounded) by another plate will be assimilated to said plate.
         * If 4 (except when at the top/bottom edges, where the number is calculated differently) or more of a hextile's adjacent hextile_objects belong to
         * a single plate, that hextile will now belong to that plate, and its direction vector will be replaced with one of its neighbour hextile_objects.
         */
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
        {
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
            {
                // List of adjacent hextile_objects moving towards the current hextile
                List<Tuple<int, int>> candidates = new List<Tuple<int, int>>();

                // Array that keeps count of the plates that the adjacent hextile_objects belong to
                List<Tuple<int, int>>[] adj_plates = new List<Tuple<int, int>>[initial_plates];
                for (int plate = 0; plate < initial_plates; plate++)
                    adj_plates[plate] = new List<Tuple<int, int>>();

                int adj_row, adj_col;

                // Left hextile
                adj_row = row;
                adj_col = (col == 0) ? hextile_objects.GetLength(1) - 1 : col - 1;
                if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "r")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Right hextile
                adj_row = row;
                adj_col = (col == hextile_objects.GetLength(1) - 1) ? 0 : col + 1;
                if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "l")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Top left hextile
                if (row < hextile_objects.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? hextile_objects.GetLength(1) - 1 : adj_col;
                    if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "rb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Top right hextile
                if (row < hextile_objects.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == hextile_objects.GetLength(1)) ? 0 : adj_col;
                    if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "lb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom left hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? hextile_objects.GetLength(1) - 1 : adj_col;
                    if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "ru")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom right hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == hextile_objects.GetLength(1)) ? 0 : adj_col;
                    if (hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.dir_code == "lu")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[hextile_objects[adj_row, adj_col].GetComponent<HextileGeography>().plate.id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                if (hextile_objects[row, col].GetComponent<HextileGeography>().exposed_asthenosphere)
                {
                    // Pick one in random
                    Tuple<int, int> adj_hextile;
                    if (candidates.Count > 0)
                        adj_hextile = candidates[Random.Range(0, candidates.Count - 1)];
                    else
                        adj_hextile = new Tuple<int, int>(row, col);
                    GameObject data = hextile_objects[adj_hextile.Item1, adj_hextile.Item2];
                    hextile_objects[row, col].GetComponent<HextileGeography>().plate = data.GetComponent<HextileGeography>().plate;
                    hextile_objects[row, col].GetComponent<HextileGeography>().exposed_asthenosphere = false;
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

                if (adj_plates[hextile_objects[row, col].GetComponent<HextileGeography>().plate.id].Count <= adj_plates_num - 4)
                {
                    // Find a hextile from the adjacent hextile_objects with plate_id of the highest_plate_num, and assimilate the current hextile to this plate
                    Tuple<int, int> adj_hextile_loc = adj_plates[highest_plate_index][0];
                    GameObject hextile_data = hextile_objects[adj_hextile_loc.Item1, adj_hextile_loc.Item2];
                    hextile_objects[row, col].GetComponent<HextileGeography>().plate = hextile_data.GetComponent<HextileGeography>().plate;
                }
            }
        }
    }

    private void FinishGeneration()
    {
        /*
         * Part 1: Land, sea and elevation. Distinguish the sea level based on the water level option.
         * Sort the hextile_objects by height and keep only the (100-X)% as land. Then map the height to create realistic hills and mountains.
         */

        // Create a 1D list of all hextile_objects
        List<GameObject> hextile_objects_list = new List<GameObject>();

        // Populate the list with all the hextile_objects
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
                hextile_objects_list.Add(hextile_objects[row, col]);

        // Sort the list based on the height of each hextile
        hextile_objects_list.Sort(SortByHeight);

        // Land is separated in three major categories: low, medium and high elevation
        float high_elevation = 5.0f / 100.0f;
        float medium_elevation = 25.0f / 100.0f;
        int land_hextile_objects = (int)((float)((100 - ui_manager.GetWaterInfo()) * (hextile_objects.GetLength(0) * hextile_objects.GetLength(1))) / 100);
        int high_hextile_objects = (int)(land_hextile_objects * high_elevation);
        int medium_hextile_objects = (int)(land_hextile_objects * medium_elevation);
        int low_hextile_objects = land_hextile_objects - high_hextile_objects - medium_hextile_objects;

        // Apply geography
        int count = 0;
        for (int tile = hextile_objects_list.Count - 1; tile >= 0; tile--)
        {
            int row = hextile_objects_list[tile].GetComponent<HextileManager>().hextile_row;
            int col = hextile_objects_list[tile].GetComponent<HextileManager>().hextile_col;

            if (count < high_hextile_objects)
            {
                hextile_objects[row, col].GetComponent<HextileGeography>().elevation = HextileGeography.Elevation.Mountains;
                hextile_objects[row, col].GetComponent<HextileGeography>().property = HextileGeography.Property.Grassland;
            }
            else if (count < high_hextile_objects + medium_hextile_objects)
            {
                hextile_objects[row, col].GetComponent<HextileGeography>().elevation = HextileGeography.Elevation.Hills;
                hextile_objects[row, col].GetComponent<HextileGeography>().property = HextileGeography.Property.Grassland;
            }
            else if (count < high_hextile_objects + medium_hextile_objects + low_hextile_objects)
            {
                hextile_objects[row, col].GetComponent<HextileGeography>().elevation = HextileGeography.Elevation.Level;
                hextile_objects[row, col].GetComponent<HextileGeography>().property = HextileGeography.Property.Grassland;
            }
            else
            {
                hextile_objects[row, col].GetComponent<HextileGeography>().elevation = HextileGeography.Elevation.Water;
                hextile_objects[row, col].GetComponent<HextileGeography>().property = HextileGeography.Property.Ocean;
            }

            count++;
        }

        /*
         * Part 2: Eliminate isolated hextile_objects
         */
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
        {
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
            {
                // If the tile is water it will have the value true
                bool is_water = (hextile_objects[row, col].GetComponent<HextileGeography>().elevation == HextileGeography.Elevation.Water) ? true : false;

                // Gather the surrounding hextile_objects (in array: left, right, up_left, up_right, down_left, down_right)
                GameObject[] surr_hextile_objects = new GameObject[6];

                // Left
                surr_hextile_objects[0] = (col == 0) ? hextile_objects[row, hextile_objects.GetLength(1) - 1] : hextile_objects[row, col - 1];

                // Right
                surr_hextile_objects[1] = (col == hextile_objects.GetLength(1) - 1) ? hextile_objects[row, 0] : hextile_objects[row, col + 1];

                // Up side
                if (row != hextile_objects.GetLength(0) - 1)
                {
                    if (row % 2 == 0)
                    {
                        // Up left
                        surr_hextile_objects[2] = (col == 0) ? hextile_objects[row + 1, hextile_objects.GetLength(1) - 1] : hextile_objects[row + 1, col - 1];

                        // Up right
                        surr_hextile_objects[3] = hextile_objects[row + 1, col];
                    }
                    else
                    {
                        // Up left
                        surr_hextile_objects[2] = hextile_objects[row + 1, col];

                        // Up right
                        surr_hextile_objects[3] = (col == hextile_objects.GetLength(1) - 1) ? hextile_objects[row + 1, 0] : hextile_objects[row + 1, col + 1];
                    }
                }

                // Down side
                if (row != 0)
                {
                    if (row % 2 == 0)
                    {
                        // Down left
                        surr_hextile_objects[4] = (col == 0) ? hextile_objects[row - 1, hextile_objects.GetLength(1) - 1] : hextile_objects[row - 1, col - 1];

                        // Down right
                        surr_hextile_objects[5] = hextile_objects[row - 1, col];
                    }
                    else
                    {
                        // Down left
                        surr_hextile_objects[4] = hextile_objects[row - 1, col];

                        // Down right
                        surr_hextile_objects[5] = (col == hextile_objects.GetLength(1) - 1) ? hextile_objects[row - 1, 0] : hextile_objects[row - 1, col + 1];
                    }
                }

                // If this remains true after the inspection
                bool is_isolated = true;

                for (int index = 0; index < surr_hextile_objects.Length; index++)
                {
                    if (surr_hextile_objects[index] != null)
                    {
                        if (is_water && (surr_hextile_objects[index].GetComponent<HextileGeography>().elevation == HextileGeography.Elevation.Water))
                            is_isolated = false;

                        if (!is_water && (surr_hextile_objects[index].GetComponent<HextileGeography>().elevation != HextileGeography.Elevation.Water))
                            is_isolated = false;
                    }
                }

                // Is the tile is isolated, find one non null hextile and copy the height and geo_type to the isolated tile
                if (is_isolated)
                {
                    for (int index = 0; index < surr_hextile_objects.Length; index++)
                    {
                        if (surr_hextile_objects[index] != null)
                        {
                            hextile_objects[row, col].GetComponent<HextileGeography>().elevation = surr_hextile_objects[index].GetComponent<HextileGeography>().elevation;
                            hextile_objects[row, col].GetComponent<HextileGeography>().property = surr_hextile_objects[index].GetComponent<HextileGeography>().property;
                            hextile_objects[row, col].GetComponent<HextileGeography>().height_01 = surr_hextile_objects[index].GetComponent<HextileGeography>().height_01;
                            break;
                        }
                    }
                }
            }
        }

        /*
         * Part 3: Make hextile_objects 3D
         */
        for (int row = 0; row < hextile_objects.GetLength(0); row++)
            for (int col = 0; col < hextile_objects.GetLength(1); col++)
                hextile_objects[row, col].GetComponent<HextileMesh>().CalculateMeshHeights();
    }

    private int SortByHeight(GameObject h1, GameObject h2)
    {
        return h1.GetComponent<HextileGeography>().height_01.CompareTo(h2.GetComponent<HextileGeography>().height_01);
    }

    public static GameObject GetRelativeHextile(int origin_row, int origin_col, string direction)
    {
        int new_row;
        int new_col;
        switch (direction)
        {
            case "right":
                new_row = origin_row;
                new_col = (origin_col == hextile_objects.GetLength(1) - 1) ? 0 : origin_col + 1;
                return hextile_objects[new_row, new_col];
            case "top_right":
                if (origin_row == hextile_objects.GetLength(0) - 1)
                    return null;
                new_row = origin_row + 1;
                new_col = (origin_row % 2 == 0) ? origin_col : (origin_col == hextile_objects.GetLength(1) - 1) ? 0 : origin_col + 1;
                return hextile_objects[new_row, new_col];
            case "top_left":
                if (origin_row == hextile_objects.GetLength(0) - 1)
                    return null;
                new_row = origin_row + 1;
                new_col = (origin_row % 2 == 0) ? (origin_col == 0) ? hextile_objects.GetLength(1) - 1 : origin_col - 1 : origin_col;
                return hextile_objects[new_row, new_col];
            case "left":
                new_row = origin_row;
                new_col = (origin_col == 0) ? hextile_objects.GetLength(1) - 1 : origin_col - 1;
                return hextile_objects[new_row, new_col];
            case "bottom_left":
                if (origin_row == 0)
                    return null;
                new_row = origin_row - 1;
                new_col = (origin_row % 2 == 0) ? (origin_col == 0) ? hextile_objects.GetLength(1) - 1 : origin_col - 1 : origin_col;
                return hextile_objects[new_row, new_col];
            case "bottom_right":
                if (origin_row == 0)
                    return null;
                new_row = origin_row - 1;
                new_col = (origin_row % 2 == 0) ? origin_col : (origin_col == hextile_objects.GetLength(1) - 1) ? 0 : origin_col + 1;
                return hextile_objects[new_row, new_col];
            default:
                return null;
        }
    }
}
