/// VariablePowerEngine
/// ---------------------------------------------------
/// A module that allows the Power use and Isp of an engine to be varied via a part slider
///
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.Localization;

namespace NearFuturePropulsion
{
    public class VariablePowerEngine:PartModule
    {
        // Current power setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Power Level"), UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
        public float CurPowerSetting = 0f;

        [KSPField(isPersistant = false)]
        public float ConstantThrust = 10f;

        [KSPField(isPersistant = false)]
        public FloatCurve HeatCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve PowerCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve IspCurve = new FloatCurve();

        // Link all engines
        [KSPField(isPersistant = true)]
        public bool LinkAllEngines = false;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Power Input:", guiUnits = " Ec/s")]
        public float curPowerUse = 5f;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Estimated Isp:", guiUnits = " s")]
        public float CurIsp = 0f;

        private float lastPowerSetting = -1f;

        private Propellant ecPropellant;
        private Propellant fuelPropellant;

        private FloatCurve AtmoThrustCurve;
        private FloatCurve AtmoIspCurve;

        private FloatCurve SavedFloatCurve;

        private List<VariablePowerEngine> allVariableEngines;
        private ModuleEnginesFX engine;

        public string GetModuleTitle()
        {
            return "VariablePowerEngine";
        }
        public override string GetModuleDisplayName()
        {
            return Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_ModuleName");
        }

        public override string GetInfo()
        {
            return Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_PartInfo",
            PowerCurve.Evaluate(0f), PowerCurve.Evaluate(1f), IspCurve.Evaluate(0f), IspCurve.Evaluate(1f));

        }

        [KSPEvent(guiActive = true, guiName = "Link All Variable Engines", active = true)]
        public void LinkEngines()
        {
            for (int i = 0; i < allVariableEngines.Count; i++)
            {
                allVariableEngines[i].LinkAllEngines = true;
            }
            LinkAllEngines = true;
        }

        [KSPEvent(guiActive = true, guiName = "Unlink All Variable Engines", active = false)]
        public void UnlinkEngines()
        {
            for (int i = 0; i < allVariableEngines.Count; i++)
            {
                allVariableEngines[i].LinkAllEngines = false;
            }
            LinkAllEngines = false;
        }

        // Actions
        [KSPAction("Link Engines")]
        public void LinkEnginesAction(KSPActionParam param)
        {
            LinkEngines();
        }

        [KSPAction("Unlink Engines")]
        public void UnlinkEnginesAction(KSPActionParam param)
        {
            UnlinkEngines();
        }

        [KSPAction("Toggle Link Engines")]
        public void ToggleLinkEnginesAction(KSPActionParam param)
        {
            LinkAllEngines = !LinkAllEngines;
            for (int i = 0; i < allVariableEngines.Count; i++)
            {
                allVariableEngines[i].LinkAllEngines = !allVariableEngines[i].LinkAllEngines;
            }
        }


        // Finds vVariablePowerEngines on the ship
        private void SetupVariableEngines()
        {
            allVariableEngines = new List<VariablePowerEngine>();

            for (int j = 0; j < this.vessel.parts.Count; j++)
            {
                PartModuleList pml = this.vessel.parts[j].Modules;
                for (int i = 0; i < pml.Count; i++)
                {
                    PartModule curModule = pml.GetModule(i);
                    VariablePowerEngine candidate = curModule.GetComponent<VariablePowerEngine>();
                    if (candidate != null && candidate != this && !allVariableEngines.Contains(candidate))
                        allVariableEngines.Add(candidate);
                }
            }
        }

        // Finds ModuleEnginesFX on the part
        private void LoadEngineModules()
        {
            engine = part.GetComponent<ModuleEnginesFX>();
            SavedFloatCurve = engine.atmosphereCurve;
        }


        public override void OnStart(PartModule.StartState state)
        {
            LoadEngineModules();
            SetupPropellants();
            if (engine == null)
            {
                Utils.Log("VariablePowerEngine: Engine Module not good");
                return;
            }
            Fields["curPowerUse"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Field_curPowerUse");
            Fields["CurIsp"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Field_CurIsp");
            Fields["PowerLevel"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Field_PowerLevel");

            Events["LinkEngines"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Event_LinkEngines");
            Events["UnlinkEngines"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Event_UnlinkEngines");

            Actions["ToggleLinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Action_ToggleLinkEnginesAction");
            Actions["LinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Action_LinkEnginesAction");
            Actions["UnlinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariablePowerEngine_Action_UnlinkEnginesAction");


            if (state != StartState.Editor)
                SetupVariableEngines();

            ChangeIspAndPower(CurPowerSetting / 100f);
            CalculateCurves();
        }

        public override void OnUpdate()
        {
            if ((LinkAllEngines && Events["LinkEngines"].active) || (!LinkAllEngines && Events["UnlinkEngines"].active))
            {
                Events["LinkEngines"].active = !LinkAllEngines;
                Events["UnlinkEngines"].active = LinkAllEngines;
            }
        }


        public void FixedUpdate()
        {
            if (engine != null)
            {
                if (CurPowerSetting != lastPowerSetting)
                {
                    //Utils.Log("VariablePowerEngine: Changed Power to " + CurPowerSetting.ToString());
                    AdjustVariablePower();
                }
            }
        }

        private void SetupPropellants()
        {
            for (int i = 0; i < engine.propellants.Count; i++ )
            {
                if (engine.propellants[i].name != "ElectricCharge")
                {
                    fuelPropellant = engine.propellants[i];
                }
                else
                {
                    ecPropellant = engine.propellants[i];
                }
            }
            //Utils.Log("VariablePowerEngine: Isp Curve: " + IspCurve.Evaluate(0f) + " to " + IspCurve.Evaluate(1f));

            AdjustVariablePower();
        }
        private void AdjustVariablePower()
        {
            ChangeIspAndPower(CurPowerSetting / 100f);
            lastPowerSetting = CurPowerSetting;
            if (LinkAllEngines && allVariableEngines != null)
            {
                for (int i = 0; i< allVariableEngines.Count; i++)
                {
                    allVariableEngines[i].ChangeIspAndPowerLinked(this, CurPowerSetting / 100f);
                }
            }
        }

        private void CalculateCurves()
        {
            AtmoThrustCurve = new FloatCurve();
            AtmoThrustCurve.Add(0f, engine.maxThrust);
            AtmoThrustCurve.Add(1f, 0f);

            AtmoIspCurve = new FloatCurve();
            AtmoIspCurve.Add(0f, engine.atmosphereCurve.Evaluate(0f));

            float rate = FindFlowRate(engine.maxThrust, engine.atmosphereCurve.Evaluate(0f), fuelPropellant);

            AtmoIspCurve.Add(1f, FindIsp(0f, rate, fuelPropellant));
        }

        public void ChangeIspAndPower(float level)
        {
            curPowerUse = PowerCurve.Evaluate(level);

            RecalculateRatios(curPowerUse, IspCurve.Evaluate(level));

            //Utils.Log("VariablePowerEngine:" + engine.engineID);
            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, IspCurve.Evaluate(level));
            engine.atmosphereCurve.Add(1f, SavedFloatCurve.Evaluate(1f));
            engine.atmosphereCurve.Add(4f, SavedFloatCurve.Evaluate(4f));

            engine.heatProduction = HeatCurve.Evaluate(level);

            CurIsp = IspCurve.Evaluate(level);

            //Utils.Log("VariablePowerEngine: Changed Isp to " + engine.atmosphereCurve.Evaluate(0f).ToString());
           // Utils.Log("VariablePowerEngine: Changed power use to " +curPowerUse.ToString());
        }

        public void ChangeIspAndPowerLinked(VariablePowerEngine other, float level)
        {
            if (this != other && CurPowerSetting != level * 100f)
                CurPowerSetting = level * 100f;
        }

        private void RecalculateRatios(float desiredPower, float desiredisp)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((ConstantThrust) / (desiredisp * engine.g));
            float fuelFlowRate = (float)fuelRate;

            fuelRate = fuelRate / fuelDensity;


            float ecRate = desiredPower / (float)fuelRate;
            Debug.Log(ecRate);
            fuelPropellant.ratio = 1f;
            ecPropellant.ratio = ecRate;

            engine.maxFuelFlow = fuelFlowRate;
        }

        // finds the flow rate given thrust, isp and the propellant
        private float FindFlowRate(float thrust, float isp, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((thrust * 1000f) / (isp * Utils.GRAVITY)) / (fuelDensity * 1000f);
            return (float)fuelRate;
        }

        private float FindIsp(float thrust, float flowRate, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double isp = (((thrust * 1000f) / (Utils.GRAVITY)) / flowRate) / (fuelDensity * 1000f);
            return (float)isp;
        }
    }
}
