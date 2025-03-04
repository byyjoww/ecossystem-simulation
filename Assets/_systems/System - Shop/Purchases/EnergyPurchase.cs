﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "new Energy Purchase", menuName = "Scriptable Object/Shop/Energy Purchase")]
public class EnergyPurchase : ManualPurchase
{
    public EnergySystem energySystem;
    [Range(1f, 10f)] public int amountToFill;

    protected override bool PurchaseCondition()
    {
        return energySystem.CurrentEnergy != energySystem.MaxEnergy;
    }

    protected override string ConditionMessage { get { return "Purchased Failed. Already at max energy."; } }

    protected override void PurchaseSuccessful()
    {
        int amount = Mathf.Min(amountToFill, energySystem.MaxEnergy - energySystem.CurrentEnergy);
        for (int i = 0; i < amount; i++)
        {
            energySystem.RecoverEnergy();
        }
    }
}