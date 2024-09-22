using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Economy;
using UnityEngine.UI;


//For Beautiful Editor UwU
#if UNITY_EDITOR
using UnityEditor;
#endif
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]


public class PatreonLoader : UdonSharpBehaviour
{
    const string DebugPrefix = "<Color=#19EFF3>[Patreon Loader]</color>"; // Cute {debug.log} prefix <3
    #region Public Variables

    [Header("Patreon Link")]
    [SerializeField] private VRCUrl PatreonUrl;

    [Space(30)]

    [Header("AutoReload")]

    [Tooltip("Enable this if you want reload OverTime"), SerializeField]
    private bool AutoReload;

    [Tooltip("select the loop time"), Range(10f, 120f), SerializeField]
    private float ReloadDelay = 60;

    [Space(30)]

    [Header("PatreonBoard")]

    [SerializeField]
    private ScrollRect scrollRect;
    private bool moveway = true;
    [SerializeField]
    private float scrollWait = 3;
    private float timer;
    [Range(0, 0.5f), SerializeField] private float TextSpeed = 0.05f;
    [Tooltip("The textmeshpro who will hold your patreon names! \n \n Need be the UI TextMeshPro"), SerializeField]
    private TextMeshProUGUI PatreonBoard;

    [Tooltip("Names of the patreon tiers \n ex: Tier 1,Tier 2,Supporter+,etc..."), SerializeField]
    private string[] TierNames;

    [Tooltip("Automatically set all alpha colors to max!"), SerializeField]
    private bool setAlphaToMax = true;

    [Tooltip("Choose the colors for the patreons names in the board!"), SerializeField]
    private Color[] ChooseColors;

    [Tooltip("Don't recommended if AutoSize in the PatreonBoard is enabled"), SerializeField]
    private bool useThisSize;

    [Tooltip("Choose the size of the tier names!"), SerializeField]
    private float[] TierSize;

    [Tooltip("Choose the size of the Patreon names. Recommended to be slightly smaller than TierSize!"), SerializeField]
    private float patreonNameSize;

    [Space(30)]

    [Header("Creator Economy")]

    public UdonProduct[] udonProducts;
    public int[] equivalentTier;
    private string productowners;
    private bool loaded;
    private int loadedProducts;

    [Space(30)]

    [Header("Patreon Benefits")]

    [Tooltip("Specify the tiers to be excluded from receiving benefits. Enter the index of the tier names array. For example, if 'TipJar' is element 0 in TierNames, enter 0 here to exclude it from receiving benefits"), SerializeField]
    private int[] excludeTier;

    [Tooltip("select udonbehaviours to receive the custom event and only the patreon will execute that")]
    public UdonBehaviour[] PatreonBenefits;

    [Tooltip("This is the string customevent who will send to another udonbehaviours")]
    public string CustomEventName;

    [NonSerialized] public bool IsPatreon = false;
    [NonSerialized] public int Patreontier = -1; // -1 = is not patreon. Start with everyone not being patreon
    #endregion

    #region Private Variables
    private string[] TierColors; // String To Store ChooseColors. Because we need convert colors to string to put in the FinalText

    private string PatreonPage; // The First Result From The StringDownloader Without Any Filtering

    private string[] PatreonTiers; // Array containg the all tiers found in the string

    private string[] PatreonNames; // Array containg all names found in the string

    private string FinalText; //The Text Formated With Titles And Colors setted up

    private string localname; // the local player displayname


    #endregion

    #region Patreon Loader Source

    private void Start()
    {
        Debug.Log($"{DebugPrefix} Created By <Color=#3D72FF>RyuukaVR</color>");
        #region Check Errors
        //Check For Missing Essential Components
        if (PatreonUrl == null)
        {
            Debug.LogError($"{DebugPrefix} PatreonURL is null or empty. Please initialize it.");
            return;
        }
        if (ChooseColors == null || ChooseColors.Length == 0)
        {
            Debug.LogError($"{DebugPrefix} ChooseColors is null or empty. Please initialize it.");
            return;
        }
        if (TierNames == null || TierNames.Length == 0)
        {
            Debug.LogError($"{DebugPrefix} TierNames is null or empty. Please initialize it.");
            return;
        }

        if (TierNames.Length != ChooseColors.Length)
        {
            Debug.LogError($"{DebugPrefix} Tiername and ChooseColors cannot be different. Please fix");
            return;
        }

        if (PatreonBoard == null)
        {
            Debug.LogError($"{DebugPrefix} PatreonBoard is null or empty.Please Initialize it. ");
            return;
        }

        if (PatreonBenefits == null)
        {
            Debug.LogError($"{DebugPrefix} PatreonBenefits is null please initialize");
            return;
        }
        #endregion

        productowners = "<b><br>Creator Economy Supporters<br>";
        PatreonBoard.gameObject.SetActive(false);

        if (setAlphaToMax)
        {
            for (int i = 0; i < ChooseColors.Length; i++)
            {
                ChooseColors[i].a = 1.0f;
            }
        }
        localname = Networking.LocalPlayer.displayName;
        TierColors = new string[ChooseColors.Length]; //Set the TierColors to ChooseColors Lenght
        PatreonDownload();
    }

    public override void OnPurchasesLoaded(IProduct[] products, VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        Debug.Log($"{DebugPrefix} PurchasesLoaded called");
        Debug.Log($"{DebugPrefix} purchases lenght {products.Length} ");

        for (int i = 0; i < udonProducts.Length; i++)
        {
            if (Store.DoesPlayerOwnProduct(player, udonProducts[i]))
            {
                Debug.Log($"{DebugPrefix}<Color=green>[CreatorEconomy]</color> Player have the udon product");
                Patreontier = equivalentTier[i];
                IsPatreon = true;
                GiveBenefits(equivalentTier[i]);
                break;
            }
            else
            {
                Debug.Log($"{DebugPrefix}<Color=green>[CreatorEconomy]</color> Player dont have a product");
            }
        }
        loaded = true;
    }

    public void PatreonDownload()
    {
        VRCStringDownloader.LoadUrl(PatreonUrl, (IUdonEventReceiver)this);
        if (AutoReload)
        {
            //Autoreload = true Repeat This Process with ReloadDelay
            SendCustomEventDelayedSeconds(nameof(PatreonDownload), ReloadDelay);
        }

    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        FinalText = "";
        PatreonPage = result.Result;
        PatreonTiers = PatreonPage.Split(new string[] { ":" }, StringSplitOptions.None);
        PatreonNames = String.Join("*", PatreonTiers).Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);

        //Call This To Change the colors
        ConvertColorsToHex();

        for (int i = 0; i < PatreonTiers.Length; i++)
        {
            if (PatreonTiers[i] != null && TierColors[i] != null && TierNames[i] != null)
            {
                if (useThisSize == true)
                {
                    string[] patreons = PatreonTiers[i].Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    FinalText = FinalText + $"<b><size={TierSize[i]}><{TierColors[i]}>{TierNames[i]}</b><br><size={patreonNameSize}>{string.Join(" ", patreons)}<br><br>";
                }
                else
                {
                    string[] patreons = PatreonTiers[i].Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    FinalText = FinalText + $"<b><{TierColors[i]}>{TierNames[i]}</b><br>{string.Join(" ", patreons)}<br><br>";
                }

            }
        }
        //Set The TMP To The Setted Up Text
        PatreonBoard.text = FinalText;
        Debug.Log($"{DebugPrefix} Final Text in <Color=#Red>Patreon Loaded</color> is {FinalText}");

        //Check If The DisplayName = PatreonName And Set The Patreon Tier
        foreach (string patreonname in PatreonNames)
        {
            if (patreonname == localname)
            {
                IsPatreon = true;
                for (int i = 0; i < PatreonTiers.Length; i++)
                {
                    if (PatreonTiers[i].Contains(patreonname))
                    {
                        Patreontier = i;
                        break;
                    }
                }
                break;
            }
        }

        if (IsPatreon) GiveBenefits(Patreontier);

        for (int i = 0; i < udonProducts.Length; i++)
        {
            var udonProduct = udonProducts[i];
            if (udonProduct != null)
            {
                Store.ListProductOwners((IUdonEventReceiver)this, udonProduct);
            }
        }

        PatreonBoard.gameObject.SetActive(true);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogError($"{DebugPrefix} Load string error {result.Error}");
    }

    #endregion

    #region Colors Change
    //Functions To ConvertColors RGBA To Hex and store in TierColors String
    private void ConvertColorsToHex()
    {
        for (int i = 0; i < ChooseColors.Length; i++)
        {
            TierColors[i] = ColorToHex(ChooseColors[i]);
        }
    }

    private string ColorToHex(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);
        int a = Mathf.RoundToInt(color.a * 255);

        return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", r, g, b, a);
    }
    #endregion

    #region Creator Economy Source

    public override void OnListProductOwners(IProduct product, string[] owners)
    {
        if (product != null)
        {
            Debug.Log($"{DebugPrefix} <Color=green>[CreatorEconomy]</color> Loaded product owners {product.ID} {product.Name} ({owners.Length} Owners)");
            foreach (var owner in owners)
            {
                if (owner != null)
                {
                    productowners += owner + " ";
                }
            }
        }
        loadedProducts++;
        if (loadedProducts >= udonProducts.Length) UpdateProductOwners();
    }

    private void UpdateProductOwners()
    {
        PatreonBoard.text = "";
        PatreonBoard.text = FinalText + productowners;
        Debug.Log($"{DebugPrefix} UpdateProductOwners productowners {productowners} /// FinalText {FinalText} ///");
        Debug.Log($"{DebugPrefix} SHOULD APPEAR IN THE BOARD >>>>> {PatreonBoard.text}");
    }

    public override void OnPurchaseConfirmed(IProduct product, VRCPlayerApi player, bool purchasedNow)
    {
        if (!loaded && !purchasedNow) return;

        bool isStoreProduct = false;
        for (int i = 0; i < udonProducts.Length; i++)
        {
            if (product.ID == udonProducts[i].ID)
            {
                isStoreProduct = true;
            }
        }

        if (isStoreProduct)
        {
            productowners += player.displayName;
            for (int i = 0; i < udonProducts.Length; i++)
            {
                GiveBenefits(equivalentTier[i]);
            }
            UpdateProductOwners();
        }
    }

    #endregion

    private void GiveBenefits(int patreontier)
    {
        if (excludeTier != null && Array.IndexOf(excludeTier, patreontier) != -1)
        {
            Debug.Log($"{DebugPrefix} Patreontier {patreontier} is in the exclusion list. Benefits will not be granted.");
            return;
        }

        for (int i = 0; i < PatreonBenefits.Length; i++)
        {
            if (PatreonBenefits[i] != null)
            {
                PatreonBenefits[i].SendCustomEvent(CustomEventName);
            }
        }
    }



    #region AutoScrool function

    private void Update()
    {
        if (scrollRect == null) return;

        ScrollVertical();
    }
    void ScrollVertical()
    {
        if (scrollRect.verticalNormalizedPosition > 0 && scrollRect.verticalNormalizedPosition < 1)
        {
            if (timer >= 0)
            {
                timer -= Time.deltaTime;
                return;
            }
            if (moveway) scrollRect.verticalNormalizedPosition -= TextSpeed * Time.deltaTime;
            else scrollRect.verticalNormalizedPosition += TextSpeed * Time.deltaTime;
        }
        else
        {
            moveway = !moveway;
            timer = scrollWait;
            if (scrollRect.verticalNormalizedPosition >= 1) scrollRect.verticalNormalizedPosition = 0.999f;
            else scrollRect.verticalNormalizedPosition = 0.001f;
        }
    }

    #endregion
}
#if UNITY_EDITOR
[CustomEditor(typeof(PatreonLoader))]
class YTSearchEditor : Editor
{
    #region Variables
    private Texture2D discordLogo;
    private Texture2D patreonLogo;
    private Texture2D RyuLogo;
    private bool showInternal = true;
    private SerializedProperty CustomEventSTR;

    #endregion

    private void OnEnable()
    {
        discordLogo = Resources.Load<Texture2D>("DiscordIcon");
        patreonLogo = Resources.Load<Texture2D>("PatreonIcon");
        RyuLogo = Resources.Load<Texture2D>("RyuukaLogo278");
    }

    public override void OnInspectorGUI()
    {
        HeaderGui();
    }

    private void HeaderGui()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            GUILayout.Box(RyuLogo, GUIStyle.none);
            GUILayout.FlexibleSpace();
        }

        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(5));

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(patreonLogo, GUILayout.Width(60), GUILayout.Height(35)))
            {
                Application.OpenURL("https://www.patreon.com/RyuukaVR");
            }

            GUILayout.Space(10);

            if (GUILayout.Button(discordLogo, GUILayout.Width(60), GUILayout.Height(35)))
            {
                Application.OpenURL("https://discord.gg/g3kjx5EuTw");
            }
            GUILayout.FlexibleSpace();
        }

        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(5));
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.VerticalScope())
            {
                showInternal = EditorGUILayout.Foldout(showInternal, "    Internal", true, EditorStyles.boldLabel);
                if (showInternal)
                {
                    DrawDefaultInspector();
                }
            }
        }
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(5));
    }
}
#endif