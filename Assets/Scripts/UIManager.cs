using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour {

    // Control buttons references
    private UnityEngine.UI.Button initialize_btn;
    private UnityEngine.UI.Button step_btn;
    private UnityEngine.UI.Button finish_btn;
    private UnityEngine.UI.Button plates_btn;
    private UnityEngine.UI.Button geography_btn;
    private UnityEngine.UI.Button height_btn;

    // Option fields references
    private TMPro.TMP_InputField seed_option;
    private TMPro.TMP_Dropdown map_size_option;
    private TMPro.TMP_Dropdown water_level_option;
    private TMPro.TMP_Dropdown climate_option;
    private TMPro.TMP_Dropdown resources_option;
    private TMPro.TMP_Dropdown distribution_option;

    // Info labels references
    private TMPro.TextMeshProUGUI steps_info;

    // Option values
    public int seed_value = 1234;
    public int map_size_value = 0;  // 0-Small, 1-Medium, 2-Large
    public int water_level_value = 1;  // 0-Low, 2-Normal, 3-High
    public int climate_value = 0;
    public int resources_value = 1;
    public int distribution_value = 0;


    public void Start()
    {
        // Get the control buttons references
        initialize_btn = GameObject.Find("InitButton").GetComponent<UnityEngine.UI.Button>();
        step_btn = GameObject.Find("Step1Button").GetComponent<UnityEngine.UI.Button>();
        finish_btn = GameObject.Find("FinishGenButton").GetComponent<UnityEngine.UI.Button>();
        plates_btn = GameObject.Find("PlatesModeButton").GetComponent<UnityEngine.UI.Button>();
        geography_btn = GameObject.Find("GeographyModeButton").GetComponent<UnityEngine.UI.Button>();
        height_btn = GameObject.Find("HeightModeButton").GetComponent<UnityEngine.UI.Button>();

        // Get the option fields references
        seed_option = GameObject.Find("SeedField").GetComponent<TMPro.TMP_InputField>();
        map_size_option = GameObject.Find("MapSize").GetComponent<TMPro.TMP_Dropdown>();
        water_level_option = GameObject.Find("WaterLevel").GetComponent<TMPro.TMP_Dropdown>();
        climate_option = GameObject.Find("Climate").GetComponent<TMPro.TMP_Dropdown>();
        resources_option = GameObject.Find("ResourcesLevel").GetComponent<TMPro.TMP_Dropdown>();
        distribution_option = GameObject.Find("Distribution").GetComponent<TMPro.TMP_Dropdown>();

        // Get the info labels references
        steps_info = GameObject.Find("StepsLabel").GetComponent<TMPro.TextMeshProUGUI>();

        // Set the options on the gui
        seed_option.text = seed_value.ToString();
        map_size_option.value = map_size_value;
        water_level_option.value = water_level_value;
        climate_option.value = climate_value;
        resources_option.value = resources_value;
        distribution_option.value = distribution_value;
    }

    // BUTTONS

    public void DisableInitializeBtn()
    {
        initialize_btn.interactable = false;
    }
    
    public void EnableStepBtns()
    {
        step_btn.interactable = true;
        finish_btn.interactable = true;
    }

    public void DisableStepBtns()
    {
        step_btn.interactable = false;
        finish_btn.interactable = false;
    }

    public void EnableMapmodeBtn(string mapmode)
    {
        switch (mapmode)
        {
            case "plates":
                plates_btn.interactable = true;
                break;
            case "geography":
                geography_btn.interactable = true;
                break;
            case "height":
                height_btn.interactable = true;
                break;
            case "all":
                plates_btn.interactable = true;
                geography_btn.interactable = true;
                height_btn.interactable = true;
                break;
        }
    }

    public void DisableMapmodeBtn(string mapmode)
    {
        switch (mapmode)
        {
            case "plates":
                plates_btn.interactable = false;
                break;
            case "geography":
                geography_btn.interactable = false;
                break;
            case "height":
                height_btn.interactable = false;
                break;
            case "all":
                plates_btn.interactable = false;
                geography_btn.interactable = false;
                height_btn.interactable = false;
                break;
        }
    }
    
    // OPTIONS

    public void DisableOptions()
    {
        seed_option.interactable = false;
        map_size_option.interactable = false;
        water_level_option.interactable = false;
        climate_option.interactable = false;
        resources_option.interactable = false;
        distribution_option.interactable = false;
    }

    public void SetInfoText(string field, string info)
    {
        switch (field)
        {
            case "steps":
                steps_info.text = info;
                break;
        }
    }

    // OPTION VALUES

    public void UpdateOptionValues()
    {
        seed_value = int.Parse(seed_option.text);
        map_size_value = map_size_option.value;
        water_level_value = water_level_option.value;
        climate_value = climate_option.value;
        resources_value = resources_option.value;
        distribution_value = distribution_option.value;
    }

    public int[] GetMapSizeInfo()
    {
        switch (map_size_value)
        {
            case 0:  // Small size: Width-50, Height-35
                return new int[2] { 50, 35 };
            case 1:  // Normal size: Width-100, Height-70
                return new int[2] { 100, 70 };
            case 2:  // Large size: Width-200, Height-140
                return new int[2] { 200, 140 };
            default:  // Default is normal
                return new int[2] { 100, 70 };
        }
    }

    public int GetWaterInfo()
    {
        switch (water_level_value)
        {
            case 0:  // Low water level
                return 50;
            case 1:  // Medium water level
                return 70;
            case 2:  // High water level
                return 90;
            default:  // Default is medium
                return 70;
        }
    }

}
