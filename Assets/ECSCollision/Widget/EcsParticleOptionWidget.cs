using DOTS;
using EcsCollision;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
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


        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var parameter = DOTSMecro.GetSingleton<ParticleParameterComponent>(entityManager);
        var spawner = DOTSMecro.GetSingleton<ParticleSpawnerComponent>(entityManager);

        MaxAmountInputField.onEndEdit.AddListener(OnEndEditMaxAmount);
        MaxAmountInputField.text = spawner.MaxAmount.ToString();

        SPC_Scrollbar.value = spawner.SpawnPerSecond / spawner.MaxAmount;
        SPC_Scrollbar.onValueChanged.AddListener(OnChangedSPCScroll);

        RadiusScrollbar.value = parameter.ParticleRadius;
        RadiusScrollbar.onValueChanged.AddListener(OnChangedRadiusScroll);

        LiquidScrollbar.value = parameter.SimulateLiquid;
        LiquidScrollbar.onValueChanged.AddListener(OnChangedLiquidScroll);
    }

    // Update is called once per frame
    void Update()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (DOTSMecro.TrySingleton<ParticleParameterComponent>(entityManager, out var parameter))
        {
            var spawner = DOTSMecro.GetSingleton<ParticleSpawnerComponent>(entityManager);

            int duckAmount = 0;
            {
                var builder = new EntityQueryBuilder(Allocator.Temp);
                builder.WithAll<FluidSimlationComponent>();
                duckAmount = entityManager.CreateEntityQuery(builder).CalculateEntityCount();
            }

            ActiveAmountText.text = duckAmount.ToString();//spawner.MaxAmount.ToString();
            SPC_HandleText.text = spawner.SpawnPerSecond.ToString();
            RadiusHandleText.text = parameter.ParticleRadius.ToString("0.##");
            LiquidHandleText.text = parameter.SimulateLiquid.ToString("0.##");
        }
    }

    public void OnClickMainButton()
    {
        MainPanel.SetActive(!MainPanel.activeSelf);
    }
    public void OnEndEditMaxAmount(string vaule)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var spawner = DOTSMecro.GetSingletonRW<ParticleSpawnerComponent>(entityManager);
        spawner.ValueRW.MaxAmount = Mathf.Clamp(int.Parse(vaule), 0, int.MaxValue);
    }
    public void OnChangedSPCScroll(float vaule)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var spawner = DOTSMecro.GetSingletonRW<ParticleSpawnerComponent>(entityManager);
        spawner.ValueRW.SpawnPerSecond = Mathf.RoundToInt(spawner.ValueRO.MaxAmount * vaule);
    }
    public void OnChangedRadiusScroll(float vaule)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var parameter = DOTSMecro.GetSingletonRW<ParticleParameterComponent>(entityManager);
        parameter.ValueRW.ParticleRadius = vaule;

    }
    public void OnChangedLiquidScroll(float vaule)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var parameter = DOTSMecro.GetSingletonRW<ParticleParameterComponent>(entityManager);
        parameter.ValueRW.SimulateLiquid = vaule;
    }
}
