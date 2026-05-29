using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [Serializable]
    public class CarVariant
    {
        public string carId;
        public string colourId;
        public GameObject carObject;
    }

    [SerializeField] private List<CarVariant> variants;
    [SerializeField] private CameraRig cameraRig;

    private void Start()
    {
        string carId    = "sedan";
        string colourId = "sedan_white";

        if (ShopDataManager.Instance != null)
        {
            carId    = ShopDataManager.Instance.UserProfile.selectedCarId;
            colourId = ShopDataManager.Instance.UserProfile.selectedColourId;
        }
        else
        {
            string path = Path.Combine(Application.persistentDataPath, "user_profile.json");
            if (File.Exists(path))
            {
                UserProfile profile = JsonUtility.FromJson<UserProfile>(File.ReadAllText(path));
                carId    = profile.selectedCarId;
                colourId = profile.selectedColourId;
            }
        }

        CarVariant match = variants.Find(v => v.carId == carId && v.colourId == colourId);

        if (match == null)
        {
            Debug.LogWarning($"CarSpawner: no variant for {carId}/{colourId}, using first available.");
            match = variants.Find(v => v.carObject != null);
        }

        foreach (var v in variants)
            if (v.carObject != null)
                v.carObject.SetActive(v == match);

        if (cameraRig != null && match?.carObject != null)
        {
            cameraRig.target = match.carObject.transform;
            Debug.Log($"CarSpawner: camera target set to {match.carObject.name}");
        }
        else
        {
            Debug.LogWarning($"CarSpawner: cameraRig={cameraRig}, match={match?.carObject?.name}");
        }
    }
}
