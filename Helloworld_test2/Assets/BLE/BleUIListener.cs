using UnityEngine;
using TMPro;
using Android.BLE;
using Android.BLE.Commands;
using UnityEngine.Android;
using System;

public class BleUIListener : MonoBehaviour
{
    public TMP_Text logText;

    private BleAdapter adapter;
    private bool isConnecting = false;
    private bool isConnected = false;

    private bool suppressDebug = false;
    private string logPath;


    private const string NUS_SERVICE = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
    private const string NUS_RX_UUID = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";
    private const string NUS_TX_UUID = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";

    private const string TARGET_MAC = "28:CD:C1:14:B8:3C";


    private void Log(string msg)
    {
        if (!suppressDebug)
        {
            if (logText) logText.text = msg; // Only show BLE messages before connection
        }

        // Always goes to Unity console only if debug is not suppressed
        if (!suppressDebug)
            Debug.Log(msg);
    }



    private static string NormalizeMac(string mac)
    {
        if (string.IsNullOrEmpty(mac)) return mac;
        mac = mac.Replace(":", "").ToUpperInvariant();
        if (mac.Length == 12)
            mac = string.Join(":", 
                System.Text.RegularExpressions.Regex.Split(mac, @"(?<=\G..)(?!$)")
            );

        return mac;
    }

    private System.Collections.IEnumerator Start()
    {
        yield return RequestPermissions();

        adapter = FindObjectOfType<BleAdapter>();
        if (!adapter)
        {
            Debug.LogError("BleAdapter not found");
            yield break;
        }

        adapter.OnErrorReceived += OnBleError;

        BleManager.Instance.Initialize();
        yield return new WaitForSeconds(0.2f);

        Log("Scanning for IMU...");
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

    private System.Collections.IEnumerator RequestPermissions()
    {
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");

        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");

        if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
            Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

        float timeout = 0;
        while (timeout < 3f &&
               (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") ||
                !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT")))
        {
            timeout += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDeviceFound(string address, string name)
    {
        bool isTarget =
            (!string.IsNullOrEmpty(name) && name.ToUpper().Contains("PICO")) ||
            string.Equals(NormalizeMac(address), NormalizeMac(TARGET_MAC), StringComparison.OrdinalIgnoreCase);

        if (!isTarget || isConnecting) return;

        isConnecting = true;
        Log("Found device: " + name);

        BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
    }

    private void OnScanFinished()
    {
        if (isConnected) return;

        Log("Rescanning...");
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

    private void OnDeviceConnected(string address)
    {
        isConnecting = false;
        isConnected = true;

        Log("Connected: " + address);

        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logPath = System.IO.Path.Combine(
            Application.persistentDataPath,
            $"IMU_Log_{date}.csv"
        );
        System.IO.File.WriteAllText(logPath, "timestamp,ax,ay,az,gx,gy,gz\n");


        StartCoroutine(SubscribeAfter(address));
    }

    private System.Collections.IEnumerator SubscribeAfter(string address)
    {
        yield return new WaitForSeconds(0.5f);

        bool firstAttemptFailed = false;

        try
        {
            BleManager.Instance.QueueCommand(
                new SubscribeToCharacteristic(
                    address,
                    NUS_SERVICE,
                    NUS_TX_UUID,
                    OnIMUDataReceived,
                    true
                )
            );
        }
        catch
        {
            firstAttemptFailed = true;
        }

        if (firstAttemptFailed)
        {
            yield return new WaitForSeconds(0.5f);

            try
            {
                BleManager.Instance.QueueCommand(
                    new SubscribeToCharacteristic(
                        address,
                        NUS_SERVICE,
                        NUS_TX_UUID,
                        OnIMUDataReceived,
                        true
                    )
                );
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to subscribe twice: " + ex.Message);
                yield break;
            }
        }

        Log("Subscribed. Waiting for notifications...");
        
        suppressDebug = true;

        BleManager.Instance.QueueCommand(
            new WriteToCharacteristic(
                address,
                NUS_SERVICE,
                NUS_RX_UUID,
                System.Text.Encoding.UTF8.GetBytes("hi")
            )
        );
    }

    private void OnBleError(string error)
    {
        isConnecting = false;
        isConnected = false;

        Log("BLE error: " + error);

        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

    private void OnIMUDataReceived(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 12)
        {
            Debug.Log("Unexpected packet length: " + bytes?.Length);
            return;
        }

        short ax_i = BitConverter.ToInt16(bytes, 0);
        short ay_i = BitConverter.ToInt16(bytes, 2);
        short az_i = BitConverter.ToInt16(bytes, 4);
        short gx_i = BitConverter.ToInt16(bytes, 6);
        short gy_i = BitConverter.ToInt16(bytes, 8);
        short gz_i = BitConverter.ToInt16(bytes, 10);

        float ax = ax_i / 1000f;
        float ay = ay_i / 1000f;
        float az = az_i / 1000f;

        float gx = gx_i / 100f;
        float gy = gy_i / 100f;
        float gz = gz_i / 100f;

        //Clean Terminal Output
        Debug.Log($"ax={ax:F3} ay={ay:F3} az={az:F3} | gx={gx:F2} gy={gy:F2} gz={gz:F2}");
    
        //Clean UI output
        if (logText)
        {
            logText.text =
                $"ax={ax:F3}  ay={ay:F3}  az={az:F3}\n" +
                $"gx={gx:F2}  gy={gy:F2}  gz={gz:F2}";
        }

        //Save to file
        if (!string.IsNullOrEmpty(logPath))
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"{time},{ax},{ay},{az},{gx},{gy},{gz}\n";
            System.IO.File.AppendAllText(logPath, line);
        }
    
    }
}
