using System;
using System.IO;
using UnityEngine;
using System.Collections;

public class RecordBothHandsToCSV : MonoBehaviour
{
    public Transform leftHand;     // Assign in Inspector
    public Transform rightHand;    // Assign in Inspector

    private string fileName;
    private StreamWriter writer;

    void Awake()
    {
        // Safe filename with timestamp
        fileName = "HandData_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";

        // Folder inside Assets
        string folderPath = Path.Combine(Application.dataPath, "HandInfo");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        // Full file path
        string filePath = Path.Combine(folderPath, fileName);

        // Open CSV file
        writer = new StreamWriter(filePath, false);

        // CSV header
        writer.WriteLine(
            "Time," +
            "L_PosX,L_PosY,L_PosZ,L_RotX,L_RotY,L_RotZ," +
            "R_PosX,R_PosY,R_PosZ,R_RotX,R_RotY,R_RotZ"
        );
        writer.Flush();

        Debug.Log("Recording to: " + filePath);
    }

    IEnumerator Start()
    {
        while (true)
        {
            WriteBothHands();
            yield return new WaitForSeconds(0.05f); // 20 Hz
        }
    }

    void WriteBothHands()
    {
        if (writer == null) return;

        // Handle missing hands safely
        Vector3 lPos = leftHand ? leftHand.position : Vector3.zero;
        Vector3 lRot = leftHand ? leftHand.eulerAngles : Vector3.zero;

        Vector3 rPos = rightHand ? rightHand.position : Vector3.zero;
        Vector3 rRot = rightHand ? rightHand.eulerAngles : Vector3.zero;

        writer.WriteLine(
            $"{Time.time:F4}," +
            $"{lPos.x:F4},{lPos.y:F4},{lPos.z:F4},{lRot.x:F4},{lRot.y:F4},{lRot.z:F4}," +
            $"{rPos.x:F4},{rPos.y:F4},{rPos.z:F4},{rRot.x:F4},{rRot.y:F4},{rRot.z:F4}"
        );

        writer.Flush();
    }

    void OnApplicationQuit()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
        }
    }
}