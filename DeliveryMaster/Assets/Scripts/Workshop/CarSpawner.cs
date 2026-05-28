using System;
using System.Collections.Generic;
using UnityEngine;

// Place all car GameObjects in the scene (inactive by default).
// Fill Variants in Inspector: each entry maps carId+colourId to a scene object.
// On Start, the correct one gets activated.
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

    private void Start()
    {
        string carId    = "sedan";
        string colourId = "sedan_white";

        if (ShopDataManager.Instance != null)
        {
            carId    = ShopDataManager.Instance.UserProfile.selectedCarId;
            colourId = ShopDataManager.Instance.UserProfile.selectedColourId;
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
    }
}
