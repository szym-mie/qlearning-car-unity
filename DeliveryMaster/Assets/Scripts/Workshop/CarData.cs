using System;
using System.Collections.Generic;

[Serializable]
public class CarDatabase
{
    public List<CarData> cars;
}

[Serializable]
public class CarData
{
    public string id;
    public string name;
    public int price;
    public string image;
    public CarCharacteristics characteristics;
    public List<ColourData> colours;
    public List<UpgradeData> upgrades;
}

[Serializable]
public class ColourData
{
    public string id;
    public string name;
    public string image;
    public int price;
}

[Serializable]
public class CarCharacteristics
{
    public int speed;
    public int acceleration;
    public int handling;
    public int braking;
}

[Serializable]
public class UpgradeData
{
    public string id;
    public string name;
    public string description;
    public int price;
}

[Serializable]
public class UserProfile
{
    public int account;
    public List<string> ownedCarIds;
    public List<OwnedCarColours> ownedCarColours;
    public string selectedCarId;
    public string selectedColourId;
}

[Serializable]
public class OwnedCarColours
{
    public string carId;
    public List<string> colourIds;
}
