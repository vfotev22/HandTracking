/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Extensions;
using Firebase.Database;

public class FirebaseInit : MonoBehaviour
{
    private FirebaseApp app;
    private DatabaseReference db;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                app = FirebaseApp.DefaultInstance;

                // MUST SET THIS FOR UNITY
                app.Options.DatabaseUrl = 
                    new System.Uri("https://bluetoothproject-d3d89-default-rtdb.firebaseio.com/");

                db = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase Database Ready");
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + task.Result);
            }
        });
    }
}
*/