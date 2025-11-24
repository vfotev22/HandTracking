using UnityEngine;
using UnityEngine.XR;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System;

public class HateandSpit : MonoBehaviour
{
    public XRNode handNode = XRNode.RightHand;

    private DatabaseReference dbRef;
    private string handName;

    private Vector3 lastPos;
    private Quaternion lastRot;
    private bool hasLastPose = false;

    private float lastSendTime = 0f;
    private float sendInterval = 0.5f;
    private float movementThreshold = 0.01f;

    private string sessionID;

    private List<HandFrame> allFrames = new List<HandFrame>();

    [Serializable]
    public class HandFrame
    {
        public float time;
        public Vector3Serializable position;
        public QuaternionSerializable rotation;
    }

    [Serializable]
    public class FrameListWrapper
    {
        public HandFrame[] frames;
    }

    [Serializable]
    public class Vector3Serializable { public float x, y, z; }
    [Serializable]
    public class QuaternionSerializable { public float x, y, z, w; }

    [Serializable]
    public class HandData
    {
        public Vector3Serializable position;
        public QuaternionSerializable rotation;
    }

    void Start()
    {
        handName = handNode == XRNode.LeftHand ? "left" : "right";
        sessionID = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                // Create a custom Firebase app with database URL
                FirebaseApp app = FirebaseApp.Create(new AppOptions()
                {
                    DatabaseUrl = new Uri("https://bluetoothproject-d3d89-default-rtdb.firebaseio.com/")
                });

                dbRef = FirebaseDatabase.GetInstance(app).RootReference;
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
        if (!device.isValid) return;

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
        {
            // Store frame for final file
            allFrames.Add(new HandFrame
            {
                time = Time.time,
                position = new Vector3Serializable { x = pos.x, y = pos.y, z = pos.z },
                rotation = new QuaternionSerializable { x = rot.x, y = rot.y, z = rot.z, w = rot.w }
            });

            if (!hasLastPose)
            {
                lastPos = pos;
                lastRot = rot;
                hasLastPose = true;
            }

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
        if (dbRef == null) return;

        HandData data = new HandData
        {
            position = new Vector3Serializable { x = pos.x, y = pos.y, z = pos.z },
            rotation = new QuaternionSerializable { x = rot.x, y = rot.y, z = rot.z, w = rot.w }
        };

        string json = JsonUtility.ToJson(data);

        // Use Push() to append new frame instead of overwriting
        dbRef.Child("sessions").Child(sessionID)
            .Child(handName).Child("realtime")
            .Push().SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError("Failed to send realtime frame: " + task.Exception);
            });
    }

    void OnApplicationQuit()
    {
        SendFinalFile();
    }

    void OnDestroy()
    {
        // In case application quits unexpectedly in editor
        SendFinalFile();
    }

    void SendFinalFile()
    {
        if (dbRef == null || allFrames.Count == 0) return;

        FrameListWrapper wrapper = new FrameListWrapper { frames = allFrames.ToArray() };
        string json = JsonUtility.ToJson(wrapper);

        dbRef.Child("sessions").Child(sessionID)
            .Child(handName + "_final_json")
            .SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                    Debug.Log("Final file uploaded for " + handName);
                else
                    Debug.LogError("Failed to upload final file: " + task.Exception);
            });
    }
}