using UnityEngine;

public class CylinderDrawer : MonoBehaviour
{
    public GameObject cylinderPrefab; // Assign your cylinder prefab in the Unity Editor

    void Start()
    {
        // Example usage:
        Vector3 point1 = new Vector3(0, 0, 0);
        Vector3 point2 = new Vector3(5, 5, 5);
        DrawCylinder(point1, point2);
    }

    void DrawCylinder(Vector3 point1, Vector3 point2)
    {
        // Calculate the midpoint between the two points
        Vector3 midpoint = (point1 + point2) / 2;

        // Calculate the distance between the two points
        float distance = Vector3.Distance(point1, point2);

        // Create a cylinder mesh
        GameObject cylinder = CreateCylinder(distance);

        // Orient the cylinder to point from point1 to point2
        cylinder.transform.rotation = Quaternion.LookRotation(point2 - point1);

        // Position the cylinder at the midpoint
        cylinder.transform.position = midpoint;

        // Add the cylinder to the game scene
        // Assuming you have a parent GameObject to organize your cylinders
        cylinder.transform.parent = transform;
    }

    GameObject CreateCylinder(float height)
    {
        // Instantiate your cylinder prefab
        GameObject cylinder = Instantiate(cylinderPrefab);

        // Adjust the scale to set the height of the cylinder
        cylinder.transform.localScale = new Vector3(1, height / 2, 1);

        return cylinder;
    }
}