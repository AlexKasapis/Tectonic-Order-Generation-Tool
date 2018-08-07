using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plate
{
    public int id;
    public Vector2 dir_vector;
    public string dir_code;


    public Plate(int id)
    {
        this.id = id;
        GenerateDirVector();
    }

    private void GenerateDirVector()
    {
        // Rotate the unit vector that is tangent to the x axis by a random amount of degrees
        Vector2 unit_vector = new Vector2(1, 0);
        float theta = Random.Range(0, 359) * Mathf.Deg2Rad;
        float x = unit_vector.x * Mathf.Cos(theta) - unit_vector.y * Mathf.Sin(theta);
        float y = unit_vector.x * Mathf.Sin(theta) + unit_vector.y * Mathf.Cos(theta);
        dir_vector = new Vector2(x, y);

        string[] codes = { "r", "ru", "lu", "l", "ld", "rd" };
        float offset_angle = CalculateVectorAngle(dir_vector) + 30;
        if (offset_angle >= 360)
            offset_angle = 360 - offset_angle;
        dir_code = codes[(int)(offset_angle / 60)];
    }

    public void ModifyDirVector(float angle, float magnitude_mod)
    {
        float x = dir_vector.x * Mathf.Cos(angle) - dir_vector.y * Mathf.Sin(angle);
        float y = dir_vector.x * Mathf.Sin(angle) + dir_vector.y * Mathf.Cos(angle);

        x *= magnitude_mod;
        y *= magnitude_mod;

        dir_vector = new Vector2(x, y);

        string[] codes = { "r", "ru", "lu", "l", "ld", "rd" };
        float offset_angle = CalculateVectorAngle(dir_vector) + 30;
        if (offset_angle >= 360)
            offset_angle = 360 - offset_angle;
        dir_code = codes[(int)(offset_angle / 60)];
    }

    public static float CalculateVectorAngle(Vector2 vector)
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
}
