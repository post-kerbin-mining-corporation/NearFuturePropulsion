/// VariableIspEngine
/// ---------------------------------------------------
/// A module that allows the Isp and thrust of an engine to be varied via a GUI
///
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KSP.Localization;

namespace NearFuturePropulsion
{
    public class VariableISPEngine:PartModule
    {
        // Use the direct throttle method, where VESSEL throttle links directly to Isp
        [KSPField(isPersistant = false)]
        public bool UseDirectThrottle = false;

        // Link all engine variable throttle sliders
        [KSPField(isPersistant = true)]
        public bool LinkAllEngines = false;

        // Current thrust setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Efficiency") , UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
        public float CurThrustSetting = 0f;

        // Power use in Ec/s
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Power Input", guiUnits = " Ec/s")]
        public float EnergyUsage = 100f;

        // Currently selected engine ID
        [KSPField(isPersistant = true)]
        public int EngineModeID;

        // Currently Selected Engine Mode (string)
        [KSPField(isPersistant = false, guiActive = true, guiName = "Fuel Mode")]
        public string CurrentEngineID = "";

        // Debug UI Only Fields
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Estimated Isp:", guiUnits = " s")]
        public float CurIsp;
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Estimated Thrust:", guiUnits = " kN")]
        public float CurThrust;

        /// UI BUTTONS
        // Link all engines together
        [KSPEvent(guiActive = true, guiName = "Link All Variable Engines", active = true)]
        public void LinkEngines()
        {
            for (int i = 0; i < allVariableEngines.Count; i++)
            {
                allVariableEngines[i].LinkAllEngines = true;
            }
            LinkAllEngines = true;
        }
        // Break all engine links
        [KSPEvent(guiActive = true, guiName = "Unlink All Variable Engines", active = false)]
        public void UnlinkEngines()
        {

            for (int i = 0; i < allVariableEngines.Count; i++)
            {
                allVariableEngines[i].LinkAllEngines = false;
            }
            LinkAllEngines = false;
        }

        // UI ACTIONS
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

        public string GetModuleTitle()
        {
            return "Variable Isp Engine";
        }
        public override string GetModuleDisplayName()
        {
            return Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_ModuleName");
        }

        public override string GetInfo()
        {
          string toRet = "";

          toRet += Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_PartInfo", engineModes[0].name,
            engineModes[0].thrustRange.x.ToString("F1"), engineModes[0].thrustRange.y.ToString("F1"),
            engineModes[0].ispRange.x.ToString("F1"), engineModes[0].ispRange.y.ToString("F1"),
            engineModes[1].name,
            engineModes[1].thrustRange.x.ToString("F1"), engineModes[1].thrustRange.y.ToString("F1"),
            engineModes[1].ispRange.x.ToString("F1"), engineModes[1].ispRange.y.ToString("F1"));
            return toRet;
        }

        // List of modes avaialble
        private VariableEngineMode[] engineModes;

        // Access to components
        private MultiModeEngine multiEngine;
        private ModuleEnginesFX engine;
        private List<ModuleEnginesFX> engines;

        private Propellant ecPropellant;
        private Propellant fuelPropellant;


        float lastThrottle = -1f;
        float lastThrustSetting = -1f;
        List<VariableISPEngine> allVariableEngines;
        List<FloatCurve> savedFloatCurves = new List<FloatCurve>();


        // Class that stores data for a Variable Engine Mode
        [System.Serializable]
        public class VariableEngineMode
        {
            public string name = "";

            public Vector2 ispRange;
            public Vector3 thrustRange;


            public FloatCurve IspThrustCurve = new FloatCurve();
            public AnimationState[] throttleAnim;

            public VariableEngineMode(Part p, string n, FloatCurve ispThrustCurve, string anim, int animLayer )
            {
                name = n;

                IspThrustCurve = ispThrustCurve;

                ispRange = new Vector2(IspThrustCurve.minTime, IspThrustCurve.maxTime );
                thrustRange = new Vector2(IspThrustCurve.Evaluate(ispRange.x), IspThrustCurve.Evaluate(ispRange.y));

                Utils.Log(String.Format("VariableIspEngine: Loaded engine mode {0}: Isp {1}-{2}s, Thrust {3}-{4}", name, ispRange.x, ispRange.y, thrustRange.x, thrustRange.y));

                // Set up the animation
                throttleAnim = Utils.SetUpAnimation(anim, p);
                foreach (AnimationState t in throttleAnim)
                {

                    t.blendMode = AnimationBlendMode.Blend;

                    t.layer = animLayer;
                    t.enabled = true;
               }
            }
            public string ToString()
            {
                return String.Format("VariableIspEngine: {0}: Isp {1}-{2}s, Thrust {3}-{4}", name, ispRange.x, ispRange.y, thrustRange.x, thrustRange.y);
            }
            // Sets the progress of the animation
            public void SetAnimationThrottle(float throttle, float timeDelta)
            {

                for (int i = 0; i < throttleAnim.Length; i++)
                {
                    //Utils.Log(String.Format("{0} throttle set to {1}", name, throttle));
                    if (throttle >= 0f)
                    {
                        throttleAnim[i].layer = 1;
                        throttleAnim[i].weight = 1.0f;
                        throttleAnim[i].enabled = true;
                        throttleAnim[i].normalizedTime = throttle;
                    }
                    else
                    {
                        throttleAnim[i].normalizedTime = 0f;
                        throttleAnim[i].layer = 0;
                        throttleAnim[i].weight = 0.0f;

                    }

                }
            }

            // Returns Isp given a 0-1 throttle value
            public float GetIsp(float throttle)
            {
                return (ispRange.y - ispRange.x) * throttle + ispRange.x;
            }
            // Returns thrust given a 0-1 throttle value
            public float GetThrust(float throttle)
            {
              return IspThrustCurve.Evaluate(GetIsp(throttle));
            }
        }

        // Load engine mode data
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            ConfigNode[] varNodes = node.GetNodes("VARIABLEISPMODE");
            engineModes = new VariableEngineMode[2];
            for (int i=0; i < varNodes.Length; i++)
            {
              engineModes[i] = LoadEngineMode(varNodes[i]);
            }
            Utils.Log(engineModes.Length.ToString());
        }

        // Changes the engine's Isp and thrust according to the variable slider
        public void ChangeIspAndThrust(float level)
        {
            RecalculateRatios(engineModes[EngineModeID].GetThrust(level), engineModes[EngineModeID].GetIsp(level));

            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, engineModes[EngineModeID].GetIsp(level));
            engine.atmosphereCurve.Add(1f, savedFloatCurves[EngineModeID].Evaluate(1f));
            engine.atmosphereCurve.Add(4f, savedFloatCurves[EngineModeID].Evaluate(4f));

            engine.maxThrust = engineModes[EngineModeID].GetThrust(level);

            CurIsp = engine.atmosphereCurve.Evaluate(0f);
            CurThrust = engine.maxThrust;
            //Utils.Log("VariableIspEngine: Changed Isp toengine.atmosphereCurve.Evaluate(0f) " + engine.atmosphereCurve.Evaluate(0f).ToString());
            //Utils.Log("VariableIspEngine: Changed thrust to " + engine.maxThrust.ToString());
        }

        // Recalculate engine fuel ratios to maintain a proper EC consumption
        private void RecalculateRatios(float desiredthrust, float desiredisp)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((desiredthrust ) / (desiredisp * Utils.GRAVITY)) ;
            engine.maxFuelFlow = (float)fuelRate;
            fuelRate = fuelRate / fuelDensity;
            float ecRate = EnergyUsage / (float)fuelRate;

            fuelPropellant.ratio = 1f;
            ecPropellant.ratio = fuelPropellant.ratio * ecRate;
        }

        public override void OnStart(PartModule.StartState state)
        {


            if (engineModes == null || engineModes[0] == null)
            {
                ConfigNode node = GameDatabase.Instance.GetConfigs("PART").
                    Single(c => part.partInfo.name == c.name).config.
                    GetNodes("MODULE").Single(n => n.GetValue("name") == moduleName);
                Utils.Log(node.ToString());
                OnLoad(node);
            }

            Fields["CurrentEngineID"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Field_CurrentEngineID");
            Fields["EnergyUsage"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Field_EnergyUsage");
            Fields["CurThrustSetting"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Field_CurThrustSetting");
            Fields["CurIsp"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Field_CurIsp");
            Fields["CurThrust"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Field_CurThrust");

            Events["LinkEngines"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Event_LinkEngines");
            Events["UnlinkEngines"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Event_UnlinkEngines");

            Actions["ToggleLinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Action_ToggleLinkEnginesAction");
            Actions["LinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Action_LinkEnginesAction");
            Actions["UnlinkEnginesAction"].guiName = Localizer.Format("#LOC_NFPropulsion_ModuleVariableISPEngine_Action_UnlinkEnginesAction");

            if (state != StartState.Editor)
                SetupVariableEngines();

            LoadEngineModules();

            if (engines.Count == 0)
            {
                Utils.Log("VariableIspEngine: Engine modules not good");
                return;
            }

            SetMode();

            if (engine != null)
                Utils.Log("VariableIspEngine: Engine module check passed");

            // Choose throttle mode
            if (UseDirectThrottle)
            {
                ChangeIspAndThrust(engine.requestedThrottle);
                Utils.Log("VariableIspEngine: Using direct throttle method");
            }
            else
            {
                ChangeIspAndThrust(CurThrustSetting / 100f);
                Utils.Log("VariableIspEngine: Using tweakable method");
            }


            Utils.Log("VariableIspEngine: Setup complete");

        }

        // Loads an engine Mode from a confignode structure
        protected VariableEngineMode LoadEngineMode(ConfigNode node)
        {
            FloatCurve curve = Utils.GetValue(node, "IspThrustCurve", new FloatCurve());

            string modeName = node.GetValue("name");
            string throttleAnimationName = node.GetValue("throttleAnimation");
            int throttleAnimationLayer = int.Parse(node.GetValue("throttleAnimationLayer"));

            return new VariableEngineMode(this.part, modeName, curve, throttleAnimationName, throttleAnimationLayer);
            //engineModes[0] = new VariableEngineMode(this.part,Mode1Propellant,Mode1Name,Mode1ThrustMin,Mode1ThrustMax,Mode1IspMin,Mode1IspMax,Mode1Animation);
            //engineModes[1] = new VariableEngineMode(this.part,Mode2Propellant,Mode2Name, Mode2ThrustMin, Mode2ThrustMax, Mode2IspMin, Mode2IspMax,Mode2Animation);
        }

        // Finds multiengine and ModuleEnginesFX
        private void LoadEngineModules()
        {
            engines = new List<ModuleEnginesFX>();
            savedFloatCurves = new List<FloatCurve>();

            PartModuleList modules = part.Modules;

            foreach (PartModule mod in part.Modules)
            {
                if (mod.moduleName == "ModuleEnginesFX")
                {
                    engines.Add((ModuleEnginesFX)mod);

                    savedFloatCurves.Add(((ModuleEnginesFX)mod).atmosphereCurve);
                    //Utils.Log("VariableIspEngine: " +  ((ModuleEnginesFX)mod).runningEffectName);
                }
                if (mod.moduleName == "MultiModeEngine")
                    multiEngine = mod.GetComponent<MultiModeEngine>();
            }

        }

        // Sets the engine mode
        private void SetMode()
        {
            if (multiEngine.runningPrimary)
                EngineModeID = 0;
            else
                EngineModeID = 1;

            Utils.Log("VariableIspEngine: Changing mode to " + engineModes[EngineModeID].name);
            CurrentEngineID = engineModes[EngineModeID].name;
            engine = engines[EngineModeID];

            for (int i=0; i < engine.propellants.Count; i++)
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

            //Utils.Log("VariableIspEngine: Changed mode to " + engine.engineID);
            //Utils.Log("VariableIspEngine: Fuel: " + fuelPropellant.name);
            //Utils.Log("VariableIspEngine: Thrust Curve: " + ThrustCurve.Evaluate(0f) + " to " + ThrustCurve.Evaluate(1f));
            //Utils.Log("VariableIspEngine: Isp Curve: " + IspCurve.Evaluate(0f) + " to " + IspCurve.Evaluate(1f));

            AdjustVariableThrust();
        }

        // Locates all variable engies on a vessel
        private void SetupVariableEngines()
        {
            allVariableEngines = new List<VariableISPEngine>();

            List<Part> allParts = this.vessel.parts;
            foreach (Part pt in allParts)
            {

                PartModuleList pml = pt.Modules;
                for (int i = 0; i < pml.Count; i++)
                {
                    PartModule curModule = pml.GetModule(i);
                    VariableISPEngine candidate = curModule.GetComponent<VariableISPEngine>();

                    if (candidate != null && candidate != this && !allVariableEngines.Contains(candidate ) )
                        allVariableEngines.Add(candidate);
                }
            }
        }


        public void ChangeIspAndThrustLinked(VariableISPEngine other, float level)
        {
            if (this != other && CurThrustSetting != level*100f)
                CurThrustSetting = level * 100f;
        }

        public void Update()
        {
            if ((LinkAllEngines && Events["LinkEngines"].active) || (!LinkAllEngines && Events["UnlinkEngines"].active))
            {
                Events["LinkEngines"].active = !LinkAllEngines;
                Events["UnlinkEngines"].active = LinkAllEngines;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (engine != null && multiEngine.runningPrimary)
                {

                    engineModes[1].SetAnimationThrottle(-1f, TimeWarp.deltaTime * 3.0f);
                    engineModes[0].SetAnimationThrottle(engine.normalizedThrustOutput, TimeWarp.deltaTime);

                }
                else if (engine != null)
                {

                    engineModes[0].SetAnimationThrottle(-1f, TimeWarp.deltaTime * 3.0f);
                    engineModes[1].SetAnimationThrottle(engine.normalizedThrustOutput, TimeWarp.deltaTime);

                }
            }
        }

        public void FixedUpdate()
        {
            if (engine != null)
            {
                if ((multiEngine.runningPrimary && EngineModeID != 0) || (!multiEngine.runningPrimary && EngineModeID != 1))
                {
                    SetMode();
                }

                if (UseDirectThrottle)
                {
                    float throttleAmt = engine.requestedThrottle;
                     if (throttleAmt != lastThrottle)
                     {
                         ChangeIspAndThrust(throttleAmt);

                         lastThrottle = throttleAmt;
                     }
                     CurThrustSetting = engine.requestedThrottle * 100f;
                }
                else
                {

                    if (CurThrustSetting != lastThrustSetting)
                    {
                        Utils.Log("VariableIspEngine: Changed power to " + CurThrustSetting.ToString());
                        AdjustVariableThrust();
                    }

                }
            }

        }

        private void AdjustVariableThrust()
        {
            ChangeIspAndThrust(CurThrustSetting / 100f);
            lastThrustSetting = CurThrustSetting;
            if (LinkAllEngines && allVariableEngines != null)
            {
                for (int i=0; i < allVariableEngines.Count; i++)
                {
                    allVariableEngines[i].ChangeIspAndThrustLinked(this, CurThrustSetting / 100f);
                }
            }
        }

    }
}
