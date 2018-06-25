using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Options : MonoBehaviour
{
    public static int seed = 1234;
    public static int map_size = 0;
    public static int water_level = 1;
    public static int climate = 0;
    public static int resources = 1;
    public static int distribution = 0;


    void Start()
    {
        // Set the options on the gui
        GameObject.Find("SeedField").GetComponent<TMPro.TMP_InputField>().text = seed.ToString();
        GameObject.Find("MapSize").GetComponent<TMPro.TMP_Dropdown>().value = map_size;
        GameObject.Find("WaterLevel").GetComponent<TMPro.TMP_Dropdown>().value = water_level;
        GameObject.Find("Climate").GetComponent<TMPro.TMP_Dropdown>().value = climate;
        GameObject.Find("ResourcesLevel").GetComponent<TMPro.TMP_Dropdown>().value = resources;
        GameObject.Find("Distribution").GetComponent<TMPro.TMP_Dropdown>().value = distribution;
    }

    public void ChangeOption(string option_name)
    {
        switch (option_name)
        {
            case "seed":
                seed = int.Parse(GameObject.Find("SeedField").GetComponent<TMPro.TMP_InputField>().text);
                break;
            case "map_size":
                map_size = GameObject.Find("MapSize").GetComponent<TMPro.TMP_Dropdown>().value;
                break;
            case "water_level":
                water_level = GameObject.Find("WaterLevel").GetComponent<TMPro.TMP_Dropdown>().value;
                break;
            case "climate":
                climate = GameObject.Find("Climate").GetComponent<TMPro.TMP_Dropdown>().value;
                break;
            case "resources":
                resources = GameObject.Find("ResourcesLevel").GetComponent<TMPro.TMP_Dropdown>().value;
                break;
            case "distribution":
                distribution = GameObject.Find("Distribution").GetComponent<TMPro.TMP_Dropdown>().value;
                break;
        }
    }
}
