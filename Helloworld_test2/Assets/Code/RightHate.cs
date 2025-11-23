using UnityEngine;
using UnityEngine.XR;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;

public class RightHate : MonoBehaviour
{
    public XRNode handNode = XRNode.RightHand;
    
    private DatabaseReference dbRef;
    private string handName;

    private Vector3 lastPos;
    private Quaternion lastRot;

    private float lastSendTime = 0f;
    private float sendInterval = 0.5f;
    private float movementThreshold = 0.01f;

    private string sessionID;

    private List<HandFrame> allFrames = new List<HandFrame>();

    [Serializable]
    public class HandFrame
    {
        public float time;
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }

    void Start()
    {
        handName = handNode == XRNode.LeftHand ? "left" : "right";
        sessionID = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;

                app.Options.DatabaseUrl =
                    new Uri("https://bluetoothproject-d3d89-default-rtdb.firebaseio.com/");

                dbRef = FirebaseDatabase.DefaultInstance.RootReference;

                Debug.Log("Firebase initialized. Session: " + sessionID);
            }
            else
            {
                Debug.LogError("Firebase dependency error: " + task.Result);
            }
        });
    }

    void Update()
    {
        if (dbRef == null) return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
        {
            allFrames.Add(new HandFrame
            {
                time = Time.time,
                px = pos.x, py = pos.y, pz = pos.z,
                rx = rot.x, ry = rot.y, rz = rot.z, rw = rot.w
            });

            float movedDist = Vector3.Distance(pos, lastPos);
            float rotChanged = Quaternion.Angle(rot, lastRot);
            float timePassed = Time.time - lastSendTime;

            bool shouldSend =
                movedDist > movementThreshold ||
                rotChanged > 1f ||
                timePassed >= sendInterval;

            if (shouldSend)
            {
                SendRealtime(pos, rot);

                lastPos = pos;
                lastRot = rot;
                lastSendTime = Time.time;
            }
        }
    }

    void SendRealtime(Vector3 pos, Quaternion rot)
    {
        var data = new
        {
            position = new { x = pos.x, y = pos.y, z = pos.z },
            rotation = new { x = rot.x, y = rot.y, z = rot.z, w = rot.w }
        };

        string json = JsonUtility.ToJson(data);

        dbRef.Child("sessions").Child(sessionID)
            .Child(handName).Child("realtime")
            .SetRawJsonValueAsync(json);
    }

    void OnApplicationQuit()
    {
        SendFinalFile();
    }

    void SendFinalFile()
    {
        if (dbRef == null) return;

        string json = JsonUtility.ToJson(new FrameListWrapper { frames = allFrames.ToArray() });

        dbRef.Child("sessions").Child(sessionID)
            .Child(handName + "_final_json")
            .SetRawJsonValueAsync(json);

        Debug.Log("Final file uploaded for " + handName);
    }

    [Serializable]
    public class FrameListWrapper
    {
        public HandFrame[] frames;
    }
}

