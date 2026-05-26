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
                coins = 0,
                ownedCarIds = new List<string> { "standard" },
                ownedColourIds = new List<string> { "standard_blue" },
                selectedCarId = "standard",
                selectedColourId = "standard_blue"
            };
            SaveUserProfile();
        }
    }

    public void SaveUserProfile()
    {
        File.WriteAllText(_savePath, JsonUtility.ToJson(UserProfile, true));
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public CarData GetCar(string carId) =>
        CarDatabase.cars.Find(c => c.id == carId);

    public ColourData GetColour(CarData car, string colourId) =>
        car.colours.Find(c => c.id == colourId);

    public bool OwnsCar(string carId) =>
        UserProfile.ownedCarIds.Contains(carId);

    public bool OwnsColour(string colourId) =>
        UserProfile.ownedColourIds.Contains(colourId);

    // ── Mutations ────────────────────────────────────────────────────────────

    public bool BuyCar(string carId)
    {
        CarData car = GetCar(carId);
        if (car == null || OwnsCar(carId)) return false;

        // TODO: restore coins check before release
        // if (UserProfile.coins < car.price) return false;
        // UserProfile.coins -= car.price;
        UserProfile.ownedCarIds.Add(carId);
        SaveUserProfile();
        return true;
    }

    public bool BuyColour(CarData car, string colourId)
    {
        ColourData colour = GetColour(car, colourId);
        if (colour == null || OwnsColour(colourId)) return false;

        // TODO: restore coins check before release
        // if (UserProfile.coins < colour.price) return false;
        // UserProfile.coins -= colour.price;
        UserProfile.ownedColourIds.Add(colourId);
        SaveUserProfile();
        return true;
    }

    // Marks this car+colour as the active selection for the next game session.
    public void SelectCar(string carId, string colourId)
    {
        if (!OwnsCar(carId) || !OwnsColour(colourId)) return;
        UserProfile.selectedCarId = carId;
        UserProfile.selectedColourId = colourId;
        SaveUserProfile();
    }

    // Dev helper – add coins directly (e.g. from MissionManager rewards)
    public void AddCoins(int amount)
    {
        UserProfile.coins += amount;
        SaveUserProfile();
    }
}
