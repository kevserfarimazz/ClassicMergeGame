using System;
using UnityEngine;
using UnityEngine.UI;

// HudBuilder'ın kurduğu ekran arayüzündeki dinamik parçalara (yazılar, ilerleme
// dolguları, rozetler) BoardView'ın erişebilmesi için tek noktada toplayan kap.
public class GameHud
{
    public Text levelBadge;
    public Image levelRing;

    public Text coinAmount;
    public Text energyAmount;
    public Text gemAmount;
    public GameObject gemCounter;

    public Text companionTimer;
    public Image companionFace;

    public TaskCard orderCard;
    public TaskCard questCard;
    public TaskCard areaCard;
    public TaskCard dailyCard;
    public GameObject dailyBadge;

    public Text dockTitle;
    public Text dockHint;

    public Transform toastLayer;
}

// Görev kartı şeridindeki tek bir kart: ilerleme dolgusu + "x/y" yazısı + ne
// olduğunu açıklayan küçük alt yazı.
public class TaskCard
{
    public GameObject root;
    public Image fill;
    public Text valueLabel;
    public Text subtitle;
}

// HudBuilder'a verilen buton geri çağırmaları — hepsi opsiyonel.
public class HudActions
{
    public Action onShop;
    public Action onBriefcase;
    public Action onSell;
    public Action onMap;
    public Action onPause;
    public Action onDailyClaim;
    public Action onPlusCoin;
    public Action onPlusEnergy;
    public Action onOrderInfo;
    public Action onQuestInfo;
    public Action onAreaInfo;
}
