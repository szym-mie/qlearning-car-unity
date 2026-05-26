using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to an empty GameObject in the game scene.
// Configure all 9 car+colour variants in the Inspector, then assign a spawn point.
public class CarSpawner : MonoBehaviour
{
    [Serializable]
    public class CarVariant
    {
        public string carId;
        public string colourId;
        public GameObject prefab;
    }

    [SerializeField] private List<CarVariant> variants; // fill all 9 in Inspector
    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        string carId     = "standard";
        string colourId  = "standard_blue";

        if (ShopDataManager.Instance != null)
        {
            carId    = ShopDataManager.Instance.UserProfile.selectedCarId;
            colourId = ShopDataManager.Instance.UserProfile.selectedColourId;
        }

        CarVariant match = variants.Find(v => v.carId == carId && v.colourId == colourId);
        if (match?.prefab == null)
        {
            Debug.LogWarning($"CarSpawner: no prefab found for {carId} / {colourId}, using first available.");
            match = variants.Find(v => v.prefab != null);
        }

        if (match?.prefab != null)
        {
            Vector3    pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            Instantiate(match.prefab, pos, rot);
        }
    }
}
