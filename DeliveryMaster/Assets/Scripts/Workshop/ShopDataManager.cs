using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ShopDataManager : MonoBehaviour
{
    public static ShopDataManager Instance { get; private set; }

    public CarDatabase CarDatabase { get; private set; }
    public UserProfile UserProfile { get; private set; }

    private string _savePath;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _savePath = Path.Combine(Application.persistentDataPath, "user_profile.json");
        Debug.Log("user_profile.json path: " + _savePath);
        LoadCarDatabase();
        LoadUserProfile();
    }

    private void LoadCarDatabase()
    {
        TextAsset json = Resources.Load<TextAsset>("cars");
        if (json == null)
        {
            Debug.LogError("ShopDataManager: cars.json not found in Resources/");
            return;
        }
        CarDatabase = JsonUtility.FromJson<CarDatabase>(json.text);
    }

    private void LoadUserProfile()
    {
        if (File.Exists(_savePath))
        {
            UserProfile = JsonUtility.FromJson<UserProfile>(File.ReadAllText(_savePath));
        }
        else
        {
            UserProfile = new UserProfile
            {
                account = 0,
                ownedCarIds = new List<string> { "sedan" },
                ownedCarColours = new List<OwnedCarColours>
                {
                    new OwnedCarColours
                    {
                        carId = "sedan",
                        colourIds = new List<string> { "sedan_white" }
                    }
                },
                selectedCarId = "sedan",
                selectedColourId = "sedan_white"
            };
            SaveUserProfile();
        }
    }

    public void SaveUserProfile()
    {
        File.WriteAllText(_savePath, JsonUtility.ToJson(UserProfile, true));
        AccountDisplay.Instance?.Refresh();
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public CarData GetCar(string carId) =>
        CarDatabase.cars.Find(c => c.id == carId);

    public ColourData GetColour(CarData car, string colourId) =>
        car.colours.Find(c => c.id == colourId);

    public bool OwnsCar(string carId) =>
        UserProfile.ownedCarIds.Contains(carId);

    public bool OwnsColour(string carId, string colourId)
    {
        var entry = UserProfile.ownedCarColours.Find(e => e.carId == carId);
        return entry != null && entry.colourIds.Contains(colourId);
    }

    public bool CanAfford(int price) => UserProfile.account >= price;

    // ── Mutations ────────────────────────────────────────────────────────────

    private void AddOwnedColour(string carId, string colourId)
    {
        var entry = UserProfile.ownedCarColours.Find(e => e.carId == carId);
        if (entry == null)
        {
            entry = new OwnedCarColours { carId = carId, colourIds = new List<string>() };
            UserProfile.ownedCarColours.Add(entry);
        }
        if (!entry.colourIds.Contains(colourId))
            entry.colourIds.Add(colourId);
    }

    public bool BuyCar(string carId)
    {
        CarData car = GetCar(carId);
        if (car == null || OwnsCar(carId) || UserProfile.account < car.price) return false;

        UserProfile.account -= car.price;
        UserProfile.ownedCarIds.Add(carId);

        foreach (var colour in car.colours)
            if (colour.price == 0)
                AddOwnedColour(carId, colour.id);

        SaveUserProfile();
        return true;
    }

    public bool BuyColour(CarData car, string colourId)
    {
        ColourData colour = GetColour(car, colourId);
        if (colour == null || OwnsColour(car.id, colourId) || UserProfile.account < colour.price) return false;

        UserProfile.account -= colour.price;
        AddOwnedColour(car.id, colourId);
        SaveUserProfile();
        return true;
    }

    public void SelectCar(string carId, string colourId)
    {
        if (!OwnsCar(carId) || !OwnsColour(carId, colourId)) return;
        UserProfile.selectedCarId = carId;
        UserProfile.selectedColourId = colourId;
        Debug.Log($"SelectCar saved: {carId} / {colourId}");
        SaveUserProfile();
    }

    public void AddToAccount(int amount)
    {
        UserProfile.account += amount;
        SaveUserProfile();
    }
}
