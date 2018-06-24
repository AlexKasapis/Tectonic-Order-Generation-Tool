using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct TectonicOrderHextile
{
    public int plate_id;
    public Geography geogr_id;
    public float height;
    public Vector2 direction;
    public GameObject hextile_object;
    public GameObject vector_object;
    public bool exposed_asthenosphere;
};

enum Geography
{
    Ocean, Inland_Sea, Grassland, Hills, Forest, Jungle, Snow, Desert, Mountains
}

public class TectonicOrder : MonoBehaviour {
    /*
     * Mesh Information
     */
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

    /*
     * Main array
     */
    TectonicOrderHextile[,] tiles_array;

    /*
     * Generation options - Available to the player
     */
    public int seed = 1234;
    public int hextile_rows = 50;
    public int hextile_cols = 50;

    /*
     * Generation options - Hidden from the player
     */
    public int initial_plates = 10; // Supports up to 20 plates
    // Hextile height stuff
    public float plate_min_center_height = 76;
    public float plate_max_center_height = 125;
    public float plate_height_deviation = 10; // If a plate has 100 center height, its tiles can take values from 90 to 110
    // Hextile direction stuff
    private float min_plate_movement_modifier = 40;
    private float max_plate_movement_modifier = 80; // Percentage ( -x% -> x% )
    private float min_axis_movement_force = 4;
    private float max_axis_movement_force = 10;

    /*
     * On-Screen information
     */
    private string view_mode = "plates"; // plates, geography, height
    private bool showing_vectors = false;
    private GameObject cam;
    private float cursor_x;
    private float cursor_y;
    private float hextile_eff_width = 8.6f;
    private float hextile_eff_height = 10f * 0.75f;
    private float odd_hextile_offset = 8.6f / 2;

    /*
     * Materials
     */
    private Material[] plate_mats;
    private Dictionary<Geography, Material> geogr_mats;
    private Material[] height_mats;
    

    private void Start()
    {
        // Setup the camera
        cam = GameObject.Find("__Camera");
        cam.transform.position = new Vector3(220, 500, 120);
        cam.transform.eulerAngles = new Vector3(90, 0, 0);

        cursor_x = Input.mousePosition.x;
        cursor_y = Input.mousePosition.y;

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

    private void Update()
    {
        float min_zoom = 50;
        float max_zoom = 1000;
        float min_speed = 0.1f;
        float max_speed = 2;

        // Camera control stuff
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float new_y = Input.GetAxis("Mouse ScrollWheel") * 1000;
            cam.transform.position = new Vector3(cam.transform.position.x, Mathf.Clamp(cam.transform.position.y - new_y, min_zoom, max_zoom), cam.transform.position.z);
        }

        if (Input.GetMouseButton(2))
        {
            float perc_zoom = (cam.transform.position.y - min_zoom) / (max_zoom - min_zoom);
            float speed_modifier = min_speed + perc_zoom * (max_speed - min_speed);
            float x_diff = cursor_x - Input.mousePosition.x;
            float z_diff = cursor_y - Input.mousePosition.y;
            Vector3 move_vector = new Vector3(x_diff * speed_modifier, 0, z_diff * speed_modifier);
            cam.transform.position += move_vector;
        }
        cursor_x = Input.mousePosition.x;
        cursor_y = Input.mousePosition.y;
    }

    /*
     * Sets the initial state for the map generation.
     */
    public void InitializeMap(bool wipe_data)
    {
        // Search hextile and vector objects and delete them
        if (wipe_data)
        {
            showing_vectors = false;
            for (int row = 0; row < hextile_rows; row++)
            {
                for (int col = 0; col < hextile_cols; col++)
                {
                    Destroy(tiles_array[row, col].hextile_object);
                    Destroy(tiles_array[row, col].vector_object);
                }
            }
        }

        // Set the seed for the random number generation
        Random.InitState(seed);

        /*
         * Emerging centers. Each plate is formed from a center, which is picked randomly. In the beggining,
         * each plate consists of only one hextile, its emerging center. From there, add all the adjacent hextiles
         * to the plate the emerging center belongs to. Keep doing that for every plate, increasing the radius of
         * the plates each loop by 1. If while adding new hextiles to the plate, there is a hextile that already
         * belongs to another plate, ignore that hextile. In the end, all hextiles on the map will belong in a single
         * plate.
         */
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
         * Tiles array. This is the array that holds all information about the hextiles. Each cell is a TectonicOrderHextile
         * object. Instantiate and fill this array with information about each hextile, except its direction vector. The direction
         * vectors are calculated in the end.
         */
        tiles_array = new TectonicOrderHextile[hextile_rows, hextile_rows];
        for (int row = 0; row < tiles_array.GetLength(0); row++)
        {
            for (int col = 0; col < tiles_array.GetLength(1); col++)
            {
                float h = Random.Range(center_heights[plate_tags[row, col]] - plate_height_deviation, center_heights[plate_tags[row, col]] + plate_height_deviation);
                TectonicOrderHextile hextile = new TectonicOrderHextile
                {
                    plate_id = plate_tags[row, col],
                    geogr_id = 0,
                    height = h,
                    hextile_object = CreateHextileObject(row, col, plate_tags[row, col], 0, h),
                    exposed_asthenosphere = false
                };

                tiles_array[row, col] = hextile;
            }
        }

        // Generate the directions for the current hextile map
        tiles_array = GenerateDirections(tiles_array);
    }

    private GameObject CreateHextileObject(int row, int col, int plate_id, Geography geogr_id, float height)
    {
        // Create a primitive GameObject with a name
        GameObject hextile_object = new GameObject
        {
            name = "Hextile:" + row + "," + col
        };

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

        TexturizeHextile(hextile_object, plate_id, geogr_id, height);

        return hextile_object;
    }

    private void TexturizeHextile(GameObject hextile_object, int plate_id, Geography geogr_id, float height)
    {
        // Add the textured Material, depending on the view mode
        switch (view_mode)
        {
            case "plates":
                hextile_object.GetComponent<MeshRenderer>().material = plate_mats[plate_id];
                break;
            case "geography":
                Geography id = (height <= 100) ? Geography.Ocean : Geography.Grassland;
                hextile_object.GetComponent<MeshRenderer>().material = geogr_mats[id];
                break;
            case "height":
                hextile_object.GetComponent<MeshRenderer>().material = height_mats[(int)(height - 1) / 25];
                break;
            default:
                Debug.Log("Invalid mode: " + view_mode + ". Defaulting to plates");
                hextile_object.GetComponent<MeshRenderer>().material = plate_mats[plate_id];
                break;
        }
    }

    public void ChangeViewMode(string mode)
    {
        view_mode = mode;

        for (int row = 0; row < tiles_array.GetLength(0); row++)
        {
            for (int col = 0; col < tiles_array.GetLength(1); col++)
            {
                TectonicOrderHextile data = tiles_array[row, col];
                TexturizeHextile(data.hextile_object, data.plate_id, data.geogr_id, data.height);
            }
        }
    }

    public void UpdateDirectionVectors(bool change_visibility)
    {
        if (change_visibility)
            showing_vectors = !showing_vectors;
        
        for (int row = 0; row < tiles_array.GetLength(0); row++)
        {
            for (int col = 0; col < tiles_array.GetLength(1); col++)
            {
                if (showing_vectors == false)
                {
                    if (tiles_array[row, col].vector_object)
                        tiles_array[row, col].vector_object.SetActive(false);
                }
                else
                {
                    if (!tiles_array[row, col].vector_object)
                    {
                        float x = (row % 2 == 0) ? col * hextile_eff_width + 4.3f : col * hextile_eff_width + odd_hextile_offset + 4.3f;
                        float z = row * hextile_eff_height + 5;
                        tiles_array[row, col].vector_object = Instantiate(Resources.Load("Prefabs/Vector"), new Vector3(x, 0.1f, z), Quaternion.identity) as GameObject;
                        tiles_array[row, col].vector_object.name = "Vector:" + row + "," + col;
                    }
                    else
                    {
                        tiles_array[row, col].vector_object.SetActive(true);
                    }
                    
                    GameObject line_object = tiles_array[row, col].vector_object.transform.Find("Line").gameObject;
                    line_object.transform.localScale = new Vector3(1, tiles_array[row, col].direction.sqrMagnitude / 200 * 3, 1);
                    line_object.transform.localPosition = new Vector3(tiles_array[row, col].direction.sqrMagnitude / 200 * 3, 0, 0);

                    GameObject tip_object = tiles_array[row, col].vector_object.transform.Find("Tip").gameObject;
                    tip_object.transform.localPosition = new Vector3(tiles_array[row, col].direction.sqrMagnitude / 100 * 3, 0, 0);

                    // For some reason the positive rotation is reversed
                    tiles_array[row, col].vector_object.transform.eulerAngles = new Vector3(0, -CalculateVectorAngle(tiles_array[row, col].direction), 0);
                }
            }
        }
    }

    public void DoSteps(int step_number)
    {
        /*
         * Every hextile interacts actively with an adjacent hextile (that is the hextile it's moving towards).
         * In order to calculate the interaction of the hextile correctly, we must know how the adjacent hextile will behave during the
         * current time frame. Thus, for each hextile we want to move, we must first move the hextile it is going to interact with, and
         * consequently move the hextile this hextile is going to interact with. It is convenient to use a recursive algorith for this 
         * approach. In order to avoid endless loops we must have a list of hextiles that we have already moved in this time frame.
         */

        for (int curr_step = 0; curr_step < step_number; curr_step++)
        {
            // Cells take the values: "r", "ru", "lu", "l", "ld", "rd", acronyms for right upper, left, left down, etc
            // Each cell corresponds to the hextile in question, and the value refers to which of the six directions the hextile will move towards
            string[,] plate_dirs = new string[hextile_rows, hextile_cols];

            // This array remembers the hextiles that have been already made their interaction
            // If a hextile has not been moved, the corresponding cell takes the value '0', contrary to the value '1' has it been moved
            // If a hextile has been deleted (in case of convergence) it takes the value '2'
            int[,] moved_flags = new int[hextile_rows, hextile_cols];

            // Initialize the values of both the arrays
            for (int row = 0; row < hextile_rows; row++)
            {
                for (int col = 0; col < hextile_cols; col++)
                {
                    moved_flags[row, col] = 0;

                    // Get the angle, and find which direction the hextile will move towards
                    int offset_angle = (int)CalculateVectorAngle(tiles_array[row, col].direction) + 30;
                    if (offset_angle >= 360)
                        offset_angle -= 360;
                    int sector = offset_angle / 60;
                    string[] codes = { "r", "ru", "lu", "l", "ld", "rd" };
                    plate_dirs[row, col] = codes[sector];
                }
            }

            // For each hextile run the move algorithm, if it has not already been moved
            for (int row = 0; row < hextile_rows; row++)
            {
                for (int col = 0; col < hextile_cols; col++)
                {
                    RecursiveInteraction(row, col, ref moved_flags, plate_dirs);
                }
            }

            // Manage empty and rogue hextiles
            CleanUp(plate_dirs);
        }

        // At the end, update the textures and vectors of each hextileq
        ChangeViewMode(view_mode);
        UpdateDirectionVectors(false);        
    }

    private void RecursiveInteraction(int row, int col, ref int[,] moved_flags, string[,] plate_dirs)
    {
        if (moved_flags[row, col] == 0)
        {
            moved_flags[row, col] = 1;
            int new_row = -1;
            int new_col = -1;
            switch (plate_dirs[row, col])
            {
                case "r":
                    new_row = row;
                    new_col = col + 1;
                    if (new_col >= hextile_cols)
                    {
                        new_col = 0;
                    }
                    break;
                case "ru":
                    if (row % 2 == 0)
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextile_rows)
                        {
                            new_row = row;
                        }
                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col + 1;
                        if (new_row >= hextile_rows)
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col >= hextile_cols)
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
                        if (new_row >= hextile_rows)
                        {
                            new_row = row;
                            new_col = col;
                        }
                        if (new_col < 0)
                        {
                            new_col = hextile_cols - 1;
                        }

                    }
                    else
                    {
                        new_row = row + 1;
                        new_col = col;
                        if (new_row >= hextile_rows)
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
                        new_col = hextile_cols - 1;
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
                            new_col = hextile_cols - 1;
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
                        if (new_col >= hextile_cols)
                        {
                            new_col = 0;
                        }
                    }
                    break;
                default:
                    Debug.Log("Invalid direction code: " + plate_dirs[row, col] + ". You done fucked up.");
                    break;
            }
            //Debug.Log(row + "," + col + " points to " + new_row + "," + new_col + " with " + plate_dirs[row, col]);
            RecursiveInteraction(new_row, new_col, ref moved_flags, plate_dirs);
            if (moved_flags[row, col] != 2)
            {
                MoveHextile(row, col, new_row, new_col, ref moved_flags, plate_dirs);
            }
        }
    }

    private void MoveHextile(int row, int col, int new_row, int new_col, ref int[,] moved_flags, string[,] plate_dirs)
    {        
        // If the hextile at the destination has moved from there, it's free real estate.
        if (tiles_array[new_row, new_col].exposed_asthenosphere == true)
        {
            // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
            tiles_array[new_row, new_col].plate_id = tiles_array[row, col].plate_id;
            tiles_array[new_row, new_col].height = tiles_array[row, col].height;
            tiles_array[new_row, new_col].direction = tiles_array[row, col].direction;
            tiles_array[new_row, new_col].exposed_asthenosphere = false;

            // Update the old hextile
            tiles_array[row, col].exposed_asthenosphere = true;

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
            while (plate_dirs[row, col] != move_codes[curr_code_pos])
            {
                curr_code_pos++;
            }
            int dest_code_pos = 0;
            while (plate_dirs[new_row, new_col] != move_codes[dest_code_pos])
            {
                dest_code_pos++;
            }

            int diff = Mathf.Abs(dest_code_pos - curr_code_pos);

            if (diff == 0 || diff == 1)
            {
                //// Fairly same direction -> Thrust: increase the magnitude of the destination vector and the height of both hextiles
                //tiles_array[row, col].height += 5;
                //tiles_array[new_row, new_col].height += 5;
                //tiles_array[new_row, new_col].direction *= new Vector2(1.1f, 1.1f);
            }
            else
            {
                /*
                 * Fairly opposide direction -> Convergence: 
                 * If the current hextile is higher than the destination, overlap it, increasing its height.
                 * If the destination hextile is higher, slide the current hextile under the destination, increasing the destination's height.
                 */
                if (tiles_array[row, col].height >= tiles_array[new_row, new_col].height)
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    tiles_array[new_row, new_col].plate_id = tiles_array[row, col].plate_id;
                    tiles_array[new_row, new_col].height = Mathf.Clamp(tiles_array[new_row, new_col].height + 5, 1, 200);
                    tiles_array[new_row, new_col].direction = tiles_array[row, col].direction;
                    tiles_array[new_row, new_col].exposed_asthenosphere = false;

                    // Update the old hextile
                    tiles_array[row, col].exposed_asthenosphere = true;

                    // Signal that the destination hextile has been eaten alive
                    moved_flags[new_row, new_col] = 2;
                }
                else
                {
                    // Update the changed destination hextile (plate_id, height, direction and exposed_asth)
                    tiles_array[new_row, new_col].height = Mathf.Clamp(tiles_array[new_row, new_col].height + 5, 1, 200);

                    // Update the old hextile
                    tiles_array[row, col].exposed_asthenosphere = true;
                }

            }
        }
    }

    private void CleanUp(string[,] plate_dirs)
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
        for (int row = 0; row < tiles_array.GetLength(0); row++)
        {
            for (int col = 0; col < tiles_array.GetLength(1); col++)
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
                adj_col = (col == 0) ? tiles_array.GetLength(1) - 1 : col - 1;
                if (plate_dirs[adj_row, adj_col] == "r")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Right hextile
                adj_row = row;
                adj_col = (col == tiles_array.GetLength(1) - 1) ? 0 : col + 1;
                if (plate_dirs[adj_row, adj_col] == "l")
                    candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));

                // Top left hextile
                if (row < tiles_array.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? tiles_array.GetLength(1) - 1 : adj_col;
                    if (plate_dirs[adj_row, adj_col] == "rb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Top right hextile
                if (row < tiles_array.GetLength(0) - 1)
                {
                    adj_row = row + 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == tiles_array.GetLength(1)) ? 0 : adj_col;
                    if (plate_dirs[adj_row, adj_col] == "lb")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom left hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col - 1 : col;
                    adj_col = (adj_col < 0) ? tiles_array.GetLength(1) - 1 : adj_col;
                    if (plate_dirs[adj_row, adj_col] == "ru")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                // Bottom right hextile
                if (row > 0)
                {
                    adj_row = row - 1;
                    adj_col = (row % 2 == 0) ? col : col + 1;
                    adj_col = (adj_col == tiles_array.GetLength(1)) ? 0 : adj_col;
                    if (plate_dirs[adj_row, adj_col] == "lu")
                        candidates.Add(new Tuple<int, int>(adj_row, adj_col));
                    adj_plates[tiles_array[adj_row, adj_col].plate_id].Add(new Tuple<int, int>(adj_row, adj_col));
                }

                if (tiles_array[row, col].exposed_asthenosphere)
                {
                    // Pick one in random
                    Tuple<int, int> adj_hextile;
                    if (candidates.Count > 0)
                        adj_hextile = candidates[Random.Range(0, candidates.Count - 1)];
                    else
                        adj_hextile = new Tuple<int, int>(row, col);
                    TectonicOrderHextile data = tiles_array[adj_hextile.Item1, adj_hextile.Item2];
                    tiles_array[row, col].direction = data.direction;
                    tiles_array[row, col].plate_id = data.plate_id;
                    tiles_array[row, col].exposed_asthenosphere = false;
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

                if (adj_plates[tiles_array[row, col].plate_id].Count <= adj_plates_num - 4)
                {
                    // Find a hextile from the adjacent hextiles with plate_id of the highest_plate_num, and assimilate the current hextile to this plate
                    Tuple<int, int> adj_hextile_loc = adj_plates[highest_plate_index][0];
                    TectonicOrderHextile hextile_data = tiles_array[adj_hextile_loc.Item1, adj_hextile_loc.Item2];
                    tiles_array[row, col].plate_id = hextile_data.plate_id;
                    tiles_array[row, col].direction = hextile_data.direction;
                }
            }
        }

        /*
         * 
         */
    }

    private float CalculateVectorAngle(Vector2 vector)
    {
        float angle;
        angle = Mathf.Atan2(Mathf.Abs(vector.x), Mathf.Abs(vector.y)) * Mathf.Rad2Deg;
        // Check which quadrant. 2nd, 3rd and 4th
        if (vector.y < 0 && vector.x > 0)
            angle = 180 - angle;
        else if (vector.y < 0 && vector.x < 0)
            angle += 180;
        else if (vector.y > 0 && vector.x < 0)
            angle = 360 - angle;
        return angle;
    }

    private TectonicOrderHextile[,] GenerateDirections(TectonicOrderHextile[,] tiles_array)
    {
        // Generate the edge vectors for each plate
        float[,] edge_vectors = new float[initial_plates, 4];
        for (int i = 0; i < initial_plates; i++)
        {
            float left_col = Random.Range(min_axis_movement_force, max_axis_movement_force) * Mathf.Sign(Random.Range(-1.0f, 1.0f));
            float right_col = Random.Range(min_axis_movement_force, max_axis_movement_force) * Mathf.Sign(Random.Range(-1.0f, 1.0f));
            float top_row = Random.Range(min_axis_movement_force, max_axis_movement_force) * Mathf.Sign(Random.Range(-1.0f, 1.0f));
            float bottom_row = Random.Range(min_axis_movement_force, max_axis_movement_force) * Mathf.Sign(Random.Range(-1.0f, 1.0f));

            edge_vectors[i, 0] = left_col;
            edge_vectors[i, 1] = right_col;
            edge_vectors[i, 2] = top_row;
            edge_vectors[i, 3] = bottom_row;
        }
        
        // Add the correct direction vector for each tile in the array
        for (int i = 0; i < tiles_array.GetLength(0); i++)
        {
            for (int j = 0; j < tiles_array.GetLength(1); j++)
            {
                float perc_row = i / (float)(tiles_array.GetLength(0) - 1);
                float perc_col = j / (float)(tiles_array.GetLength(1) - 1);

                float left_col = edge_vectors[tiles_array[i, j].plate_id, 0];
                float right_col = edge_vectors[tiles_array[i, j].plate_id, 1];
                float top_row = edge_vectors[tiles_array[i, j].plate_id, 2];
                float bottom_row = edge_vectors[tiles_array[i, j].plate_id, 3];

                float vector_row = top_row + perc_row * (bottom_row - top_row);
                float vector_col = left_col + perc_col * (right_col - left_col);

                tiles_array[i, j].direction = new Vector2(vector_row, vector_col);
            }
        }

        return tiles_array;
    }

}
