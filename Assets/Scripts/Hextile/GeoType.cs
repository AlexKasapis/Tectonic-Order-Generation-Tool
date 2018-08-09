using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GeoElevation {
    Water, Level, Hills, Mountains
}

public enum GeoProperty {
    Ocean, InlandSea, // Water
    Grassland, Desert, Marsh, Tundra, // No trees
    Forest, Jungle // Trees
}

public class GeoType {

    // The physical world height, based on the geo_elevation of each GeoType
    // Each cell corresponds to the index of the GeoElevation
    public static float[] base_heights =
    {
        -2, 2, 4, 10
    };

    // The random fluctuation (during the point generation) modifier based on the GeoElevation
    public static float[] elev_deviations =
    {
        0.6f, 0.9f, 1.2f, 3.0f
    };
    
    // Each different elevation type has its respective material
    public static Dictionary<GeoElevation, Material> elevation_mats;

    public GeoElevation geo_elevation;
    public GeoProperty geo_property;
    public float base_height;
    public float elev_deviation;

	public GeoType(GeoElevation geo_el, GeoProperty geo_pr)
    {
        geo_elevation = geo_el;
        geo_property = geo_pr;
        base_height = base_heights[(int)geo_elevation];
        elev_deviation = elev_deviations[(int)geo_elevation];
    }

    public static void LoadMaterials()
    {
        elevation_mats = new Dictionary<GeoElevation, Material>();
        for (int i = 0; i < GeoElevation.GetNames(typeof(GeoElevation)).Length; i++)
        {
            elevation_mats.Add((GeoElevation)i, Resources.Load("Materials/Geography/" + ((GeoElevation)i).ToString()) as Material);
        }
    }

}
