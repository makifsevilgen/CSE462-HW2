using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using TMPro;
using UnityEngine.UI;

public class PointCloudAlignment : MonoBehaviour
{
    public Camera _camera;
    TextMeshProUGUI text;

    public GameObject ModelHandler;

    public string file1Path;
    public string file2Path;

    List<Vector3> _PointSetP;
    List<Vector3> _PointSetQ;

    List<Vector3> _TransformedPointSetP;
    List<Vector3> _AlignedPointSetP;

    //Matrix4x4 _RotationMatrix;
    //Vector3 _TranlationVector;

    public int _MaxIterations = 10;
    public float _DistanceThreshold = 0.1f;

    bool showOriginalPoints = false;
    bool showTransformedWithMovement = false;

    void Start()
    {
        LoadPointCloudData(file1Path, out _PointSetP);
        LoadPointCloudData(file2Path, out _PointSetQ);

        _camera.transform.position = _PointSetP[0] + new Vector3(0, 1, -33); 

        text = GameObject.FindGameObjectWithTag("Text").GetComponent<TextMeshProUGUI>();
    }

    public void OnClickRigid(){
        Tuple<Matrix4x4, Vector3> result = RigidTransform();
        
        text.SetText("R =\t" + result.Item1.GetRow(0) + "\n\t" 
                    + result.Item1.GetRow(1) + "\n\t"
                    + result.Item1.GetRow(2) + "\n\t"
                    + result.Item1.GetRow(3) + "\n"
                    + "T =\t" + result.Item2);
    }

    public void OnClickScale(){
        Tuple<Matrix4x4, Vector3> result = RigidTransform();
        
        text.SetText("Sx = 1\tSy = 1\tSz =\t1" + "\n"
                    + "R =\t" + result.Item1.GetRow(0) + "\n\t" 
                    + result.Item1.GetRow(1) + "\n\t"
                    + result.Item1.GetRow(2) + "\n\t"
                    + result.Item1.GetRow(3) + "\n"
                    + "T =\t" + result.Item2);
    }

    public void OnClickOriginal(){
        ToggleOriginalAndAlignedPoints();
    }

    public void OnClickTransformed(){
        ToggleTransformedDataWithMovement();
    }

    void LoadPointCloudData(string filePath, out List<Vector3> pointSet)
    {
        pointSet = new List<Vector3>();

        using (StreamReader sr = new(filePath))
        {
            int numPts = int.Parse(sr.ReadLine());

            for (int i = 0; i < numPts; i++)
            {
                string[] values = sr.ReadLine().Split(' ');
                float x = float.Parse(values[0]);
                float y = float.Parse(values[1]);
                float z = float.Parse(values[2]);
                pointSet.Add(new Vector3(x, y, z));
            }
        }
    }

    Tuple<Matrix4x4, Vector3> RigidTransform()
    {
        Tuple<Matrix4x4, Vector3> RandT = RunMyRANSAC();
        _TransformedPointSetP = GetTransformedPointSet(RandT.Item1, RandT.Item2);
        _AlignedPointSetP = GetAlignedPointSet(RandT.Item1, RandT.Item2);
        return RandT;
    }
    
    Tuple<Matrix4x4, Vector3> RunMyRANSAC()
    {
        Matrix4x4 rotationMatrix = Matrix4x4.identity;
        Vector3 translationVector = Vector3.zero;
        int maxInliers = 0;
        
        for (int i = 0; i < _MaxIterations; i++)
        {
            List<int> indexP = RandomNum(_PointSetP.Count, 3);
            List<int> indexQ = RandomNum(_PointSetQ.Count, 3);

            List<Vector3> randomSetP = (from ind in indexP select _PointSetP[ind]).ToList();
            List<Vector3> randomSetQ = (from ind in indexQ select _PointSetQ[ind]).ToList();

            List<Tuple<Matrix4x4, Vector3>> RandT = CalculateRandT(randomSetP, randomSetQ);

            List<Vector3> transformedPointSet1 = GetTransformedPointSet(RandT[0].Item1, RandT[0].Item2);
            List<Vector3> transformedPointSet2 = GetTransformedPointSet(RandT[1].Item1, RandT[1].Item2);
            List<Vector3> transformedPointSet3 = GetTransformedPointSet(RandT[2].Item1, RandT[2].Item2);

            int numOfInliers1 = GetNumOfInliers(transformedPointSet1);
            int numOfInliers2 = GetNumOfInliers(transformedPointSet2);
            int numOfInliers3 = GetNumOfInliers(transformedPointSet3);

            if (numOfInliers1 > maxInliers)
            {
                rotationMatrix = RandT[0].Item1;
                translationVector = RandT[0].Item2;
                maxInliers = numOfInliers1;
            }
            if (numOfInliers2 > maxInliers)
            {
                rotationMatrix = RandT[1].Item1;
                translationVector = RandT[1].Item2;
                maxInliers = numOfInliers2;
            }
            if (numOfInliers3 > maxInliers)
            {
                rotationMatrix = RandT[2].Item1;
                translationVector = RandT[2].Item2;
                maxInliers = numOfInliers3;
            }
        }
        return Tuple.Create(rotationMatrix, translationVector);
    }

    List<Tuple<Matrix4x4, Vector3>> CalculateRandT(List<Vector3> SetP, List<Vector3> SetQ)
    {
        return new List<Tuple<Matrix4x4, Vector3>>()
        {
            GetMatrixAndVector(SetP, SetQ, 1, 0),
            GetMatrixAndVector(SetP, SetQ, 2, 1),
            GetMatrixAndVector(SetP, SetQ, 2, 0)
        };
    }

    Tuple<Matrix4x4, Vector3> GetMatrixAndVector(List<Vector3> SetP, List<Vector3> SetQ, int num1, int num2)
    {
        Quaternion rotationQuaternion = Quaternion.FromToRotation(SetP[num1] - SetP[num2], SetQ[num1] - SetQ[num2]);

        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(rotationQuaternion);

        Vector3 translationVector = SetQ[num1] - rotationMatrix.MultiplyPoint(SetP[num1]);

        return Tuple.Create(rotationMatrix, translationVector);
    }

    List<Vector3> GetTransformedPointSet(Matrix4x4 rotationMatrix, Vector3 translationVector)
    {
        return _PointSetP.Select(point => rotationMatrix.MultiplyPoint3x4(point) + translationVector).ToList();
    }

    List<Vector3> GetAlignedPointSet(Matrix4x4 rotationMatrix, Vector3 translationVector)
    {
        return _TransformedPointSetP
                .Where(tPoint => _PointSetQ.Any(qPoint => Vector3.Distance(qPoint, tPoint) < _DistanceThreshold))
                .Select(tPoint => tPoint)
                .ToList();
    }

    int GetNumOfInliers(List<Vector3> transformedPointSet)
    {
        return _PointSetQ.Count(qPoint => transformedPointSet.Any(pPoint => Vector3.Distance(qPoint, pPoint) < _DistanceThreshold));
    }

    List<int> RandomNum(int maxNum, int numberCount)
    {
        /*
        List<int> randomNumbers = new();
        for (int i = 0; i < numberCount; i++) randomNumbers.Add(UnityEngine.Random.Range(0, maxNum));
        return randomNumbers;
        */

        return Enumerable.Range(0, numberCount)
                .Select(_ => UnityEngine.Random.Range(0, maxNum))
                .ToList();
    }

    void ToggleOriginalAndAlignedPoints()
    {
        showOriginalPoints = !showOriginalPoints;

        if (showOriginalPoints)
        {
            VisualizePointsAsSpheres(_PointSetP, "pSet", "p", "Original", Color.blue);
            VisualizePointsAsSpheres(_PointSetQ, "qSet", "q", "Original", Color.red);
            VisualizePointsAsSpheres(_AlignedPointSetP, "AlignedPointsSet", "AlignedP", "Original", Color.green);
        }
        else ClearPoints();
    }

    void ToggleTransformedDataWithMovement()
    {
        showTransformedWithMovement = !showTransformedWithMovement;

        if (showTransformedWithMovement)
        {
            VisualizePointsAsSpheres(_TransformedPointSetP, "TransformedPointSet", "TransformedP", "Transformed", Color.cyan);
            DrawLine(new GameObject("Lines"){tag = "Line"});
        }
        else ClearVisualization();
    }

    private void DrawLine(GameObject parentObject)
    {
        parentObject.transform.SetParent(ModelHandler.transform);
        for (int i = 0; i < _PointSetP.Count; i++)
        {
            GameObject lineObject = new("Line" + i);
            lineObject.tag = "Line";
            lineObject.transform.parent = parentObject.transform;

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.material = new Material(Shader.Find("Standard"));
            lineRenderer.material.color = Color.white;
            
            lineRenderer.SetPosition(0, _PointSetP[i]);
            lineRenderer.SetPosition(1, _TransformedPointSetP[i]);
        }
    }

    void VisualizePointsAsSpheres(List<Vector3> pointSet, string pointSetName, string pointName, string pointTag, Color color)
    {
        
        GameObject parent = new(pointSetName);
        parent.tag = pointTag;


        parent.transform.SetParent(ModelHandler.transform);

        GameObject[] points = new GameObject[pointSet.Count];

        for (int i = 0; i < pointSet.Count; i++)
        {
            points[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            points[i].tag = pointTag;
            points[i].name = pointName + i;
            points[i].transform.SetParent(parent.transform);
            points[i].transform.localPosition = pointSet[i];
            points[i].GetComponent<Renderer>().material.SetColor("_Color", color);
            points[i].transform.localScale = new Vector3(0.5f,0.5f,0.5f);
        }
    }

    void ClearPoints()
    {
        GameObject[] points = GameObject.FindGameObjectsWithTag("Original");
        foreach (GameObject point in points) Destroy(point);
    }

    void ClearVisualization()
    {
        GameObject[] points = GameObject.FindGameObjectsWithTag("Transformed");
        foreach (GameObject point in points) Destroy(point);

        GameObject[] lines = GameObject.FindGameObjectsWithTag("Line");
        foreach (GameObject line in lines) Destroy(line);
    }
}