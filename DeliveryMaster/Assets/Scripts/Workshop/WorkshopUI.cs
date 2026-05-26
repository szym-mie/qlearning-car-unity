using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorkshopUI : MonoBehaviour
{
    [Serializable]
    public class CarSlotUI
    {
        public Button button;
        public Image carImage;
        public TextMeshProUGUI nameText;
        public Image selectionOutline;
        public GameObject lockOverlay;  // lock icon, shown when car is not owned
        public GameObject pricePanel;   // shown when car is not owned
        public TextMeshProUGUI priceText;
        public Button buyButton;
    }

    [Header("Cars Panel")]
    [SerializeField] private CarSlotUI[] carSlots;
    [SerializeField] private Color outlineSelected = new Color(1f, 0.68f, 0f);
    [SerializeField] private Color outlineDefault  = new Color(0.35f, 0.35f, 0.35f);

    [Header("Car Details Panel")]
    [SerializeField] private Image carDetailImage;
    [SerializeField] private GameObject colourLockedOverlay;     // lock icon + "UNLOCK COLOUR" label
    [SerializeField] private TextMeshProUGUI unlockColourPriceText;
    [SerializeField] private Button buyColourButton;
    [SerializeField] private Button prevColourButton;
    [SerializeField] private Button nextColourButton;

    [Header("Parameter Bars")]
    // Each root Transform must have exactly 6 child Image objects (bar segments).
    [SerializeField] private Transform speedBarRoot;
    [SerializeField] private Transform accelerationBarRoot;
    [SerializeField] private Transform handlingBarRoot;
    [SerializeField] private Transform brakingBarRoot;
    [SerializeField] private Color barFilledColour = new Color(1f, 0.68f, 0f);
    [SerializeField] private Color barEmptyColour  = new Color(0.18f, 0.18f, 0.18f);

    [Header("Coins Display")]
    [SerializeField] private TextMeshProUGUI coinsText;

    private int _carIndex;    // index into CarDatabase.cars (0-2)
    private int _colourIndex; // index into car.colours (0-2)

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Start()
    {
        for (int i = 0; i < carSlots.Length; i++)
        {
            int idx = i;
            if (carSlots[i].button   != null) carSlots[i].button.onClick.AddListener(() => OnCarSlotClicked(idx));
            if (carSlots[i].buyButton != null) carSlots[i].buyButton.onClick.AddListener(() => OnBuyCarClicked(idx));
        }
        if (prevColourButton != null) prevColourButton.onClick.AddListener(() => NavigateColour(-1));
        if (nextColourButton != null) nextColourButton.onClick.AddListener(() => NavigateColour(+1));
        if (buyColourButton  != null) buyColourButton.onClick.AddListener(OnBuyColourClicked);
    }

    private void OnEnable()
    {
        if (ShopDataManager.Instance == null) return;

        var cars = ShopDataManager.Instance.CarDatabase.cars;
        string selCarId = ShopDataManager.Instance.UserProfile.selectedCarId;
        _carIndex = Mathf.Max(0, cars.FindIndex(c => c.id == selCarId));

        string selColourId = ShopDataManager.Instance.UserProfile.selectedColourId;
        _colourIndex = Mathf.Max(0, cars[_carIndex].colours.FindIndex(c => c.id == selColourId));

        Refresh();
    }

    // ── Interaction ──────────────────────────────────────────────────────────

    private void OnCarSlotClicked(int index)
    {
        var mgr = ShopDataManager.Instance;
        var car = mgr.CarDatabase.cars[index];
        if (!mgr.OwnsCar(car.id)) return;

        _carIndex = index;

        // restore the previously-selected colour for this car if we own it
        string savedColour = mgr.UserProfile.selectedColourId;
        int savedIdx = car.colours.FindIndex(c => c.id == savedColour);
        _colourIndex = savedIdx >= 0 ? savedIdx : 0;

        TryAutoSelect();
        Refresh();
    }

    private void NavigateColour(int direction)
    {
        var car = ShopDataManager.Instance.CarDatabase.cars[_carIndex];
        _colourIndex = (_colourIndex + direction + car.colours.Count) % car.colours.Count;
        TryAutoSelect();
        Refresh();
    }

    private void OnBuyCarClicked(int index)
    {
        var car = ShopDataManager.Instance.CarDatabase.cars[index];
        if (!ShopDataManager.Instance.BuyCar(car.id)) return;

        _carIndex = index;
        _colourIndex = 0;
        TryAutoSelect();
        Refresh();
    }

    private void OnBuyColourClicked()
    {
        var mgr = ShopDataManager.Instance;
        var car = mgr.CarDatabase.cars[_carIndex];
        var colour = car.colours[_colourIndex];
        if (!mgr.BuyColour(car, colour.id)) return;

        TryAutoSelect();
        Refresh();
    }

    // If the current car+colour is owned, persist it as the active selection.
    private void TryAutoSelect()
    {
        var mgr = ShopDataManager.Instance;
        var car = mgr.CarDatabase.cars[_carIndex];
        if (!mgr.OwnsCar(car.id)) return;
        var colour = car.colours[_colourIndex];
        if (mgr.OwnsColour(colour.id))
            mgr.SelectCar(car.id, colour.id);
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void Refresh()
    {
        RefreshCoins();
        RefreshCarList();
        RefreshCarDetails();
    }

    private void RefreshCoins()
    {
        if (coinsText != null)
            coinsText.text = $"$ {ShopDataManager.Instance.UserProfile.coins:N0}";
    }

    private void RefreshCarList()
    {
        var mgr = ShopDataManager.Instance;
        var cars = mgr.CarDatabase.cars;

        for (int i = 0; i < carSlots.Length && i < cars.Count; i++)
        {
            CarData car = cars[i];
            CarSlotUI slot = carSlots[i];
            bool owned = mgr.OwnsCar(car.id);

            slot.nameText.text = car.name;

            Sprite sprite = Resources.Load<Sprite>(car.image);
            if (sprite != null) slot.carImage.sprite = sprite;

            slot.selectionOutline.color = (i == _carIndex) ? outlineSelected : outlineDefault;

            Color imgColor = slot.carImage.color;
            imgColor.a = owned ? 1f : 0.35f;
            slot.carImage.color = imgColor;

            slot.lockOverlay.SetActive(!owned);
            slot.pricePanel.SetActive(!owned);
            if (!owned) slot.priceText.text = $"$ {car.price:N0}";
        }
    }

    private void RefreshCarDetails()
    {
        var mgr = ShopDataManager.Instance;
        CarData car = mgr.CarDatabase.cars[_carIndex];
        ColourData colour = car.colours[_colourIndex];
        bool carOwned    = mgr.OwnsCar(car.id);
        bool colourOwned = mgr.OwnsColour(colour.id);

        Sprite sprite = Resources.Load<Sprite>(colour.image);
        if (carDetailImage != null && sprite != null) carDetailImage.sprite = sprite;

        if (colourLockedOverlay != null) colourLockedOverlay.SetActive(!colourOwned);
        if (buyColourButton    != null) buyColourButton.gameObject.SetActive(!colourOwned && carOwned);
        if (!colourOwned && unlockColourPriceText != null)
            unlockColourPriceText.text = $"$ {colour.price:N0}";

        RefreshParameterBars(car.characteristics);
    }

    private void RefreshParameterBars(CarCharacteristics ch)
    {
        SetBar(speedBarRoot,        ch.speed);
        SetBar(accelerationBarRoot, ch.acceleration);
        SetBar(handlingBarRoot,     ch.handling);
        SetBar(brakingBarRoot,      ch.braking);
    }

    private void SetBar(Transform root, int value)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            Image img = root.GetChild(i).GetComponent<Image>();
            if (img != null) img.color = i < value ? barFilledColour : barEmptyColour;
        }
    }
}
