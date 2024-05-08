using EcsCollision;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class EcsParticleOptionWidget : MonoBehaviour
{
    public Button MainButton;
    public GameObject MainPanel;
    public TMP_Text ActiveAmountText;
    public TMP_InputField MaxAmountInputField;
    public Scrollbar SPC_Scrollbar;
    public TMP_Text SPC_HandleText;
    public Scrollbar RadiusScrollbar;
    public TMP_Text RadiusHandleText;
    public Scrollbar LiquidScrollbar;
    public TMP_Text LiquidHandleText;

    public bool IntiActive = false;

    // Start is called before the first frame update
    void Start()
    {
        MainButton.onClick.AddListener(OnClickMainButton);
        MainPanel.SetActive(IntiActive);

        if (ParticleSpawner.instance == null)
        {
            this.enabled = false;
            return;
        }    

        MaxAmountInputField.onEndEdit.AddListener(OnEndEditMaxAmount);
        MaxAmountInputField.text = ParticleSpawner.instance.MaxAmount.ToString();

        SPC_Scrollbar.value = ParticleSpawner.instance.SpawnPerSecond / ParticleSpawner.instance.MaxAmount;
        SPC_Scrollbar.onValueChanged.AddListener(OnChangedSPCScroll);

        RadiusScrollbar.value = ParticleParameter.instance.particleRadius * 0.1f;
        RadiusScrollbar.onValueChanged.AddListener(OnChangedRadiusScroll);

        LiquidScrollbar.value = ParticleParameter.instance.SimulateLiquid;
        LiquidScrollbar.onValueChanged.AddListener (OnChangedLiquidScroll);
    }

    // Update is called once per frame
    void Update()
    {
        //ParticleSpawner.instance.MaxAmount
        ActiveAmountText.text = ParticleSpawner.instance.SpawnAmount.ToString();
        SPC_HandleText.text = ParticleSpawner.instance.SpawnAmountForSecond.ToString();
        RadiusHandleText.text = ParticleParameter.instance.particleRadius.ToString("0.##");
        LiquidHandleText.text = ParticleParameter.instance.SimulateLiquid.ToString("0.##");
    }

    public void OnClickMainButton()
    {
        MainPanel.SetActive(!MainPanel.activeSelf);
    }
    public void OnEndEditMaxAmount(string vaule)
    {
        ParticleSpawner.instance.MaxAmount = Mathf.Clamp(int.Parse(vaule), 0, int.MaxValue);
    }
    public void OnChangedSPCScroll(float vaule)
    {
        ParticleSpawner.instance.SpawnPerSecond = Mathf.RoundToInt(ParticleSpawner.instance.MaxAmount * vaule);
    }
    public void OnChangedRadiusScroll(float vaule)
    {
        ParticleParameter.instance.particleRadius = vaule * 10;
    }
    public void OnChangedLiquidScroll(float vaule)
    {
        ParticleParameter.instance.SimulateLiquid = vaule;
        ParticleParameter.instance.NeedUpdate = true;
    }
}
