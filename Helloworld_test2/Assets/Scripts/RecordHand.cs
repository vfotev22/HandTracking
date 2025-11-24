using System;
using System.IO;
using UnityEngine;
using System.Collections;

public class RecordBothHandsToCSV : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;

    private string filePath;

    void Start()
    {
        // Make folder
        string folderPath = Path.Combine(Application.dataPath, "HandInfo");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        // Unique filename with milliseconds
        string fileName = "HandData_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".csv";
        filePath = Path.Combine(folderPath, fileName);

        // Write header immediately, using closes the file
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            writer.WriteLine(
                "Time," +
                "L_PosX,L_PosY,L_PosZ,L_RotX,L_RotY,L_RotZ," +
                "R_PosX,R_PosY,R_PosZ,R_RotX,R_RotY,R_RotZ"
            );
        }

        Debug.Log("Recording to: " + filePath);

        // Start recording loop
        StartCoroutine(RecordLoop());
    }

    IEnumerator RecordLoop()
    {
        while (true)
        {
            WriteBothHands();
            yield return new WaitForSeconds(0.05f); // 20 Hz
        }
    }

    void WriteBothHands()
    {
        // Safely grab values
        Vector3 lPos = leftHand ? leftHand.position : Vector3.zero;
        Vector3 lRot = leftHand ? leftHand.eulerAngles : Vector3.zero;

        Vector3 rPos = rightHand ? rightHand.position : Vector3.zero;
        Vector3 rRot = rightHand ? rightHand.eulerAngles : Vector3.zero;

        // Write using() so the file never stays open
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine(
                $"{Time.time:F4}," +
                $"{lPos.x:F4},{lPos.y:F4},{lPos.z:F4},{lRot.x:F4},{lRot.y:F4},{lRot.z:F4}," +
                $"{rPos.x:F4},{rPos.y:F4},{rPos.z:F4},{rRot.x:F4},{rRot.y:F4},{rRot.z:F4}"
            );
        }
    }
}
