using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HextileGeography : MonoBehaviour {
    
    public enum Elevation {
        Water, Level, Hills, Mountains
    }

    public enum Property {
        Ocean, InlandSea, // Water
        Grassland, Desert, Marsh, Tundra, // No trees
        Forest, Jungle // Trees
    }

    // Average height on the center Point of a Hextile based on its Elevation type
    public Dictionary<Elevation, float> base_heights = new Dictionary<Elevation, float>()
    {
        { Elevation.Water, 0.0f },
        { Elevation.Level, 0.8f },
        { Elevation.Hills, 2.5f },
        { Elevation.Mountains, 7.0f }
    };

    // Modifier of the random fluctuation (during the point generation) that is based on the Elevation type of the Hextile
    public Dictionary<Elevation, float> elev_deviations = new Dictionary<Elevation, float>
    {
        { Elevation.Water, 0.0f },
        { Elevation.Level, 0.4f },
        { Elevation.Hills, 1.0f },
        { Elevation.Mountains, 2.0f }
    };

    // Elevation and Property of this Hextile
    public Elevation elevation;
    public Property property;

    // Morphological details of the hextile
    public Plate plate;
    public int height_01;
    public bool exposed_asthenosphere = false;

    public float GetDeviation() { return elev_deviations[elevation]; }

    public float GetBaseHeight() { return base_heights[elevation]; }

}
